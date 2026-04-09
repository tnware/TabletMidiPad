using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services.Settings;

/// <summary>
/// Default settings/preferences orchestration for <see cref="Views.MainWindow"/>.
/// </summary>
internal sealed class SettingsWorkflow : ISettingsWorkflow
{
    private readonly ISettingsWorkflowHost _host;
    private readonly ISettingsWorkflowCoordinator _workflowCoordinator;
    private readonly IPreferencesDialogService _preferencesDialogService;
    private readonly IZoneConfigurationService _zoneConfigurationService;
    private readonly IRuntimeConfigService _runtimeConfig;
    private readonly IMidiEngine _midiEngine;
    private readonly IPadInputController _padInputSession;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsWorkflow"/> class.
    /// </summary>
    public SettingsWorkflow(
        ISettingsWorkflowHost host,
        ISettingsWorkflowCoordinator workflowCoordinator,
        IPreferencesDialogService preferencesDialogService,
        IZoneConfigurationService zoneConfigurationService,
        IRuntimeConfigService runtimeConfig,
        IMidiEngine midiEngine,
        IPadInputController padInputSession)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _workflowCoordinator = workflowCoordinator ?? throw new ArgumentNullException(nameof(workflowCoordinator));
        _preferencesDialogService = preferencesDialogService ?? throw new ArgumentNullException(nameof(preferencesDialogService));
        _zoneConfigurationService = zoneConfigurationService ?? throw new ArgumentNullException(nameof(zoneConfigurationService));
        _runtimeConfig = runtimeConfig ?? throw new ArgumentNullException(nameof(runtimeConfig));
        _midiEngine = midiEngine ?? throw new ArgumentNullException(nameof(midiEngine));
        _padInputSession = padInputSession ?? throw new ArgumentNullException(nameof(padInputSession));
    }

    /// <inheritdoc />
    public void ApplySettings()
    {
        try
        {
            _host.CommitCurrentZoneEdits();
            WorkflowSaveResult persistResult = ValidateAndPersistCurrentConfig();
            if (!persistResult.Success)
            {
                _host.SetSettingsResult(persistResult.ErrorMessage ?? "Settings could not be applied.", SettingsResultKind.Error);
                return;
            }

            _host.UpdateUiForCurrentConfig();
            _host.RefreshPadZoneOverlay();
            _host.SetSettingsResult("Settings applied and saved.", SettingsResultKind.Success);
        }
        catch (Exception ex)
        {
            _host.SetSettingsResult(ex.Message, SettingsResultKind.Error);
        }
    }

    /// <inheritdoc />
    public async Task RunPreferencesFlow()
    {
        int effectiveExpressKeyCount = _workflowCoordinator.GetEffectiveExpressKeyCount(
            _host.PadState.ExpressKeyCount,
            _runtimeConfig.Current.ExpressKeys.Count);
        ExpressKeyConfig.EnsureCount(_runtimeConfig.Current.ExpressKeys, effectiveExpressKeyCount);
        PreferencesDialogResult? result = _preferencesDialogService.ShowDialog(
            _host.OwnerWindow,
            _runtimeConfig.Current,
            effectiveExpressKeyCount);
        if (result is null)
        {
            return;
        }

        _host.CommitCurrentZoneEdits();

        // Pass the current MIDI readiness so the coordinator only restarts when routing actually changed.
        IntPtr midiReadiness = _midiEngine.IsReady ? new IntPtr(1) : IntPtr.Zero;
        PreferencesWorkflowResult applyResult = _workflowCoordinator.ApplyPreferences(_runtimeConfig.Current, result, midiReadiness);
        if (!applyResult.SaveSucceeded)
        {
            _host.SetSettingsResult(applyResult.ErrorMessage ?? "Preferences could not be applied.", SettingsResultKind.Error);
            return;
        }

        WorkflowSaveResult persistResult = ValidateAndPersistCurrentConfig();
        if (!persistResult.Success)
        {
            _host.SetSettingsResult(persistResult.ErrorMessage ?? "Preferences could not be saved.", SettingsResultKind.Error);
            return;
        }

        if (applyResult.ShouldRestartMidi)
        {
            _host.SetSettingsResult("Preferences saved. Reconnecting MIDI\u2026", SettingsResultKind.Success);
            _host.SetMidiReconnectStatus(
                panelVisible: true,
                progressVisible: true,
                severity: MidiStatusSeverity.Normal,
                statusText: "Reconnecting MIDI...");
            try
            {
                await _midiEngine.RestartAsync(
                    _runtimeConfig.Current,
                    onProgress: text => _host.SetMidiReconnectStatus(
                        panelVisible: true,
                        progressVisible: true,
                        severity: MidiStatusSeverity.Normal,
                        statusText: text));
                _midiEngine.ClearCachedValues();
                _host.SetMidiReconnectStatus(
                    panelVisible: false,
                    progressVisible: true,
                    severity: MidiStatusSeverity.Normal,
                    statusText: string.Empty);
            }
            catch (Exception ex)
            {
                _host.SetMidiReconnectStatus(
                    panelVisible: true,
                    progressVisible: false,
                    severity: MidiStatusSeverity.Error,
                    statusText: "MIDI reconnect failed");
                _host.SetSettingsResult(ex.Message, SettingsResultKind.Error);
                return;
            }
        }

        _host.UpdateUiForCurrentConfig();
        _host.RefreshPadZoneOverlay();

        if (applyResult.ShouldRebindInputBackend)
        {
            _padInputSession.RebindInputBackend();
        }

        _host.SyncToolbar();
        _host.SetSettingsResult("Preferences applied and saved.", SettingsResultKind.Success);
    }

    /// <inheritdoc />
    public void ResetToDefaultsAndApply()
    {
        _runtimeConfig.ResetToDefaults();
        _zoneConfigurationService.AutoTileZones(_runtimeConfig.Current);
        _host.LoadConfigIntoUi();
        ApplySettings();
    }

    private WorkflowSaveResult ValidateAndPersistCurrentConfig()
    {
        WorkflowSaveResult validation = _workflowCoordinator.ValidateAndSave(_runtimeConfig.Current);
        if (!validation.Success)
        {
            return validation;
        }

        try
        {
            _runtimeConfig.SaveCurrent();
            return new WorkflowSaveResult(true, null);
        }
        catch (Exception ex)
        {
            return new WorkflowSaveResult(false, ex.Message);
        }
    }
}
