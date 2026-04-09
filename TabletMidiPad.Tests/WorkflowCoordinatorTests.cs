using System;
using System.Collections.Generic;
using TabletMidiPad.Services;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class WorkflowCoordinatorTests
{
    [Fact]
    public void GetEffectiveExpressKeyCount_ReturnsMaxConfiguredOrDetected()
    {
        var coordinator = new WorkflowCoordinator();

        int result = coordinator.GetEffectiveExpressKeyCount(4, 8);

        Assert.Equal(8, result);
    }

    [Fact]
    public void ValidateAndSave_ReturnsFailureForOutOfBoundsZone()
    {
        var coordinator = new WorkflowCoordinator();
        var config = AppConfig.Default;
        config.Zones = new List<ZoneConfig>
        {
            new()
            {
                Name = "Bad",
                Width = 0.9,
                Height = 0.9,
                Left = 0.2,
                Top = 0.2
            }
        };

        WorkflowSaveResult result = coordinator.ValidateAndSave(config);

        Assert.False(result.Success);
        Assert.Contains("must fit within the pad bounds", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyPreferences_DetectsRoutingAndInputBackendChanges()
    {
        var coordinator = new WorkflowCoordinator();
        var config = AppConfig.Default;
        config.AppPortName = "Old App";
        config.DawPortName = "Old Daw";
        config.LoopbackAssociationId = "{aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa}";
        config.InputBackendMode = "Auto";

        var result = new PreferencesDialogResult
        {
            AppPortName = "New App",
            DawPortName = "New Daw",
            AssociationId = "{bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb}",
            MousePressureValue = 100,
            PositionDeadZone = 0.02,
            PositionSmoothing = 0.1,
            PressureCurve = "Linear",
            InputBackendMode = "Wintab Only",
            LowerPenButtonAction = PenButtonActions.None,
            LowerPenButtonMidiChannel = 1,
            LowerPenButtonCc = 64,
            LowerPenButtonNote = 60,
            UpperPenButtonAction = PenButtonActions.None,
            UpperPenButtonMidiChannel = 1,
            UpperPenButtonCc = 65,
            UpperPenButtonNote = 61,
            SendPressureGlobally = true,
            AllowZoneSwitchWhilePressed = true,
            ExpressKeys = new List<ExpressKeyConfig>()
        };

        PreferencesWorkflowResult apply = coordinator.ApplyPreferences(config, result, IntPtr.Zero);

        Assert.True(apply.SaveSucceeded);
        Assert.True(apply.ShouldRestartMidi);
        Assert.True(apply.ShouldRebindInputBackend);
    }

    [Fact]
    public void BuildWacomServiceMenuState_MapsFieldsDirectly()
    {
        var coordinator = new WorkflowCoordinator();

        WacomServiceMenuState menu = coordinator.BuildWacomServiceMenuState(
            new WacomServiceStatus("Service: running", CanStart: false, IsServiceRunning: true));

        Assert.Equal("Service: running", menu.Header);
        Assert.False(menu.StartEnabled);
    }

    [Fact]
    public void ApplyPreferencesWhenZonesInvalidReturnsSaveFailed()
    {
        var coordinator = new WorkflowCoordinator();
        var config = AppConfig.Default;
        config.Zones = new List<ZoneConfig>();

        PreferencesWorkflowResult apply = coordinator.ApplyPreferences(config, PreferencesMatching(config), IntPtr.Zero);

        Assert.False(apply.SaveSucceeded);
        Assert.False(string.IsNullOrEmpty(apply.ErrorMessage));
        Assert.False(apply.ShouldRestartMidi);
        Assert.False(apply.ShouldRebindInputBackend);
    }

    [Fact]
    public void ApplyPreferencesWhenUnchangedWithOpenMidiDoesNotFlagRestartOrRebind()
    {
        var coordinator = new WorkflowCoordinator();
        var config = AppConfig.Default;

        PreferencesWorkflowResult apply = coordinator.ApplyPreferences(
            config,
            PreferencesMatching(config),
            new IntPtr(1));

        Assert.True(apply.SaveSucceeded);
        Assert.False(apply.ShouldRestartMidi);
        Assert.False(apply.ShouldRebindInputBackend);
    }

    private static PreferencesDialogResult PreferencesMatching(AppConfig config)
    {
        return new PreferencesDialogResult
        {
            DawPortName = config.DawPortName,
            AppPortName = config.AppPortName,
            AssociationId = config.LoopbackAssociationId,
            MousePressureValue = config.MousePressureValue,
            PositionDeadZone = config.PositionDeadZone,
            PositionSmoothing = config.PositionSmoothing,
            PressureCurve = config.PressureCurve,
            InputBackendMode = config.InputBackendMode,
            LowerPenButtonAction = config.LowerPenButtonAction,
            LowerPenButtonMidiChannel = config.LowerPenButtonMidiChannel,
            LowerPenButtonCc = config.LowerPenButtonCc,
            LowerPenButtonNote = config.LowerPenButtonNote,
            UpperPenButtonAction = config.UpperPenButtonAction,
            UpperPenButtonMidiChannel = config.UpperPenButtonMidiChannel,
            UpperPenButtonCc = config.UpperPenButtonCc,
            UpperPenButtonNote = config.UpperPenButtonNote,
            SendPressureGlobally = config.SendPressureGlobally,
            AllowZoneSwitchWhilePressed = config.AllowZoneSwitchWhilePressed,
            ExpressKeys = new List<ExpressKeyConfig>(config.ExpressKeys),
        };
    }

    [Fact]
    public void BuildToolbarState_MapsAutoInputAndCurveLabels()
    {
        var coordinator = new WorkflowCoordinator();
        var config = new AppConfig
        {
            InputBackendMode = "Auto",
            PressureCurve = "Logarithmic"
        };

        ToolbarState state = coordinator.BuildToolbarState(config);

        Assert.Equal("Pressure (Log)", state.ToolbarPressureLabelText);
        Assert.Equal("Logarithmic", state.SelectedPressureCurve);
        Assert.Equal("Input (Auto)", state.ToolbarInputBackendLabelText);
        Assert.Equal("Auto", state.SelectedInputBackendMode);
        Assert.False(state.IsPressureCurveLinearChecked);
        Assert.True(state.IsPressureCurveLogarithmicChecked);
        Assert.False(state.IsPressureCurveExponentialChecked);
        Assert.True(state.IsInputBackendAutoChecked);
        Assert.False(state.IsInputBackendWintabChecked);
        Assert.False(state.IsInputBackendMouseChecked);
    }

    [Fact]
    public void BuildToolbarState_NormalizesInputBackendAliases_AndMapsCheckmarks()
    {
        var coordinator = new WorkflowCoordinator();
        var config = new AppConfig
        {
            InputBackendMode = "Wintab",
            PressureCurve = "Exponential",
        };

        ToolbarState state = coordinator.BuildToolbarState(config);

        Assert.Equal("Input (Wintab)", state.ToolbarInputBackendLabelText);
        Assert.Equal("Wintab", state.SelectedInputBackendMode);
        Assert.True(state.IsInputBackendWintabChecked);
        Assert.False(state.IsInputBackendAutoChecked);
        Assert.True(state.IsPressureCurveExponentialChecked);
    }

    [Fact]
    public void BuildConnectedState_PreservesSummaryAndBaselineTexts()
    {
        var coordinator = new WorkflowCoordinator();
        var config = new AppConfig
        {
            DawPortName = "DAW X",
            AppPortName = "APP X",
            MaxZoneRows = 3,
            MaxZonesPerRow = 4,
            AllowZoneSwitchWhilePressed = false
        };
        config.Zones.Add(ZoneConfig.CreateDefault());

        ConnectedState state = coordinator.BuildConnectedState(config);

        Assert.Equal("Connected", state.StatusText);
        Assert.Equal("Send to 'DAW X' via 'APP X'", state.RoutingText);
        Assert.Contains("lock zone until release", state.ConfigSummaryText);
        Assert.Equal("Active zone: none", state.ActiveZoneText);
        Assert.Equal("X: -, Y: -, Pressure: -", state.ValueText);
        Assert.Equal("Pen buttons: -", state.PenButtonsText);
        Assert.Equal("Modifiers: none", state.ModifiersText);
    }
}
