using System;

namespace TabletMidiPad.Services;

/// <summary>
/// Default workflow coordinator for main-window preference/apply decision logic.
/// </summary>
internal sealed class MainWindowWorkflowCoordinator : IMainWindowWorkflowCoordinator
{
    /// <inheritdoc />
    public int GetEffectiveExpressKeyCount(int detectedExpressKeyCount, int configuredExpressKeyCount)
    {
        return Math.Max(detectedExpressKeyCount, configuredExpressKeyCount);
    }

    /// <inheritdoc />
    public WorkflowSaveResult ValidateAndSave(AppConfig config)
    {
        if (config.Zones.Count == 0)
        {
            return new WorkflowSaveResult(false, "At least one zone is required.");
        }

        if (config.MaxZoneRows < 1)
        {
            return new WorkflowSaveResult(false, "Max rows must be at least 1.");
        }

        if (config.MaxZonesPerRow < 1)
        {
            return new WorkflowSaveResult(false, "Max zones per row must be at least 1.");
        }

        foreach (ZoneConfig zone in config.Zones)
        {
            if (zone.Width <= 0 || zone.Height <= 0)
            {
                return new WorkflowSaveResult(false, $"Zone '{zone.Name}' must have width and height greater than 0.");
            }

            if (zone.Left + zone.Width > 1.0 || zone.Top + zone.Height > 1.0)
            {
                return new WorkflowSaveResult(false, $"Zone '{zone.Name}' must fit within the pad bounds.");
            }
        }

        return new WorkflowSaveResult(true, null);
    }

    /// <inheritdoc />
    public WorkflowSaveResult ApplySettingsAndPersist(AppConfig config, IConfigStore store)
    {
        WorkflowSaveResult validation = ValidateAndSave(config);
        if (!validation.Success)
        {
            return validation;
        }

        return PersistConfigToDisk(config, store);
    }

    /// <inheritdoc />
    public WorkflowSaveResult PersistConfigToDisk(AppConfig config, IConfigStore store)
    {
        try
        {
            store.Save(config);
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
        config.ExpressKeys = result.ExpressKeys;

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

    /// <inheritdoc />
    public WacomServiceMenuState BuildWacomServiceMenuState(WacomServiceStatus status)
    {
        return new WacomServiceMenuState(
            Header: status.StatusText,
            StartEnabled: status.CanStart);
    }
}
