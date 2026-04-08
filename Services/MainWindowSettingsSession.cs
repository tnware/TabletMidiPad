using System;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services;

/// <summary>
/// Default settings/preferences orchestration for <see cref="Views.MainWindow"/>.
/// </summary>
internal sealed class MainWindowSettingsSession : IMainWindowSettingsSession
{
    private readonly IMainWindowSettingsSessionHost _host;
    private readonly IMainWindowWorkflowCoordinator _workflowCoordinator;
    private readonly IPreferencesDialogService _preferencesDialogService;
    private readonly IZoneConfigurationService _zoneConfigurationService;
    private readonly IConfigStore _configStore;
    private readonly IMidiEngine _midiEngine;
    private readonly IMainWindowPadInputSession _padInputSession;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowSettingsSession"/> class.
    /// </summary>
    public MainWindowSettingsSession(
        IMainWindowSettingsSessionHost host,
        IMainWindowWorkflowCoordinator workflowCoordinator,
        IPreferencesDialogService preferencesDialogService,
        IZoneConfigurationService zoneConfigurationService,
        IConfigStore configStore,
        IMidiEngine midiEngine,
        IMainWindowPadInputSession padInputSession)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _workflowCoordinator = workflowCoordinator ?? throw new ArgumentNullException(nameof(workflowCoordinator));
        _preferencesDialogService = preferencesDialogService ?? throw new ArgumentNullException(nameof(preferencesDialogService));
        _zoneConfigurationService = zoneConfigurationService ?? throw new ArgumentNullException(nameof(zoneConfigurationService));
        _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        _midiEngine = midiEngine ?? throw new ArgumentNullException(nameof(midiEngine));
        _padInputSession = padInputSession ?? throw new ArgumentNullException(nameof(padInputSession));
    }

    /// <inheritdoc />
    public void ApplySettings()
    {
        try
        {
            _host.CommitCurrentZoneEdits();
            WorkflowSaveResult persistResult = _workflowCoordinator.ApplySettingsAndPersist(_host.Config, _configStore);
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
    public void RunPreferencesFlow()
    {
        int effectiveExpressKeyCount = _workflowCoordinator.GetEffectiveExpressKeyCount(
            _host.PadState.ExpressKeyCount,
            _host.Config.ExpressKeys.Count);
        ExpressKeyConfig.EnsureCount(_host.Config.ExpressKeys, effectiveExpressKeyCount);
        PreferencesDialogResult? result = _preferencesDialogService.ShowDialog(
            _host.OwnerWindow,
            _host.Config,
            effectiveExpressKeyCount);
        if (result is null)
        {
            return;
        }

        _host.CommitCurrentZoneEdits();
        PreferencesWorkflowResult applyResult = _workflowCoordinator.ApplyPreferences(_host.Config, result, IntPtr.Zero);
        if (!applyResult.SaveSucceeded)
        {
            _host.SetSettingsResult(applyResult.ErrorMessage ?? "Preferences could not be applied.", SettingsResultKind.Error);
            return;
        }

        WorkflowSaveResult persistResult = _workflowCoordinator.PersistConfigToDisk(_host.Config, _configStore);
        if (!persistResult.Success)
        {
            _host.SetSettingsResult(persistResult.ErrorMessage ?? "Preferences could not be saved.", SettingsResultKind.Error);
            return;
        }

        if (applyResult.ShouldRestartMidi)
        {
            RestartMidi();
        }
        else
        {
            _host.UpdateUiForCurrentConfig();
            _host.RefreshPadZoneOverlay();
        }

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
        _host.ReplaceConfig(AppConfig.Default);
        _zoneConfigurationService.AutoTileZones(_host.Config);
        _host.LoadConfigIntoUi(_host.Config);
        ApplySettings();
    }

    private void RestartMidi()
    {
        _midiEngine.Restart(_host.Config);
        _midiEngine.ClearCachedValues();
        _host.UpdateUiForCurrentConfig();
        _host.RefreshPadZoneOverlay();
    }
}
