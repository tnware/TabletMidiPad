namespace TabletMidiPad.Services.Settings;

/// <summary>
/// Default settings/preferences decision logic for apply/save workflows.
/// </summary>
internal sealed class SettingsWorkflowCoordinator : ISettingsWorkflowCoordinator
{
    private readonly IZoneConfigurationService _zoneConfigurationService;

    public SettingsWorkflowCoordinator(IZoneConfigurationService zoneConfigurationService)
    {
        _zoneConfigurationService = zoneConfigurationService ?? throw new ArgumentNullException(nameof(zoneConfigurationService));
    }

    /// <inheritdoc />
    public int GetEffectiveExpressKeyCount(int detectedExpressKeyCount, int configuredExpressKeyCount)
    {
        return Math.Max(detectedExpressKeyCount, configuredExpressKeyCount);
    }

    /// <inheritdoc />
    public WorkflowSaveResult ValidateAndSave(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        try
        {
            _zoneConfigurationService.ValidateConfig(config);
            return new WorkflowSaveResult(true, null);
        }
        catch (Exception ex)
        {
            return new WorkflowSaveResult(false, ex.Message);
        }
    }

    /// <inheritdoc />
    public PreferencesWorkflowResult ApplyPreferences(
        AppConfig config,
        PreferencesDialogResult result,
        IntPtr midiOutHandle)
    {
        string previousAppPortName = config.AppPortName;
        string previousDawPortName = config.DawPortName;
        string previousAssociationId = config.LoopbackAssociationId;
        string previousInputBackendMode = config.InputBackendMode;

        config.DawPortName = result.DawPortName;
        config.AppPortName = result.AppPortName;
        config.LoopbackAssociationId = result.AssociationId;
        config.MousePressureValue = result.MousePressureValue;
        config.PositionDeadZone = result.PositionDeadZone;
        config.PositionSmoothing = result.PositionSmoothing;
        config.PressureCurve = result.PressureCurve;
        config.InputBackendMode = result.InputBackendMode;
        config.LowerPenButtonAction = result.LowerPenButtonAction;
        config.LowerPenButtonMidiChannel = result.LowerPenButtonMidiChannel;
        config.LowerPenButtonCc = result.LowerPenButtonCc;
        config.LowerPenButtonNote = result.LowerPenButtonNote;
        config.UpperPenButtonAction = result.UpperPenButtonAction;
        config.UpperPenButtonMidiChannel = result.UpperPenButtonMidiChannel;
        config.UpperPenButtonCc = result.UpperPenButtonCc;
        config.UpperPenButtonNote = result.UpperPenButtonNote;
        config.SendPressureGlobally = result.SendPressureGlobally;
        config.AllowZoneSwitchWhilePressed = result.AllowZoneSwitchWhilePressed;
        config.AllowPageSwitchWhilePressed = result.AllowPageSwitchWhilePressed;
        config.WrapPageNavigation = result.WrapPageNavigation;
        config.ExpressKeys = result.ExpressKeys;
        config.ZoneOverlayStyle = result.ZoneOverlayStyle;

        WorkflowSaveResult validation = ValidateAndSave(config);
        if (!validation.Success)
        {
            return new PreferencesWorkflowResult(false, validation.ErrorMessage, false, false);
        }

        bool routingChanged =
            !string.Equals(previousAppPortName, config.AppPortName, StringComparison.Ordinal) ||
            !string.Equals(previousDawPortName, config.DawPortName, StringComparison.Ordinal) ||
            !string.Equals(previousAssociationId, config.LoopbackAssociationId, StringComparison.Ordinal);
        bool inputBackendChanged =
            !string.Equals(previousInputBackendMode, config.InputBackendMode, StringComparison.Ordinal);

        return new PreferencesWorkflowResult(
            SaveSucceeded: true,
            ErrorMessage: null,
            ShouldRestartMidi: routingChanged || midiOutHandle == IntPtr.Zero,
            ShouldRebindInputBackend: inputBackendChanged);
    }
}
