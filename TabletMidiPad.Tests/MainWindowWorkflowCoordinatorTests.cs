using System;
using System.Collections.Generic;
using TabletMidiPad.Services;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class MainWindowWorkflowCoordinatorTests
{
    [Fact]
    public void GetEffectiveExpressKeyCount_ReturnsMaxConfiguredOrDetected()
    {
        var coordinator = new MainWindowWorkflowCoordinator();

        int result = coordinator.GetEffectiveExpressKeyCount(4, 8);

        Assert.Equal(8, result);
    }

    [Fact]
    public void ValidateAndSave_ReturnsFailureForOutOfBoundsZone()
    {
        var coordinator = new MainWindowWorkflowCoordinator();
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
        var coordinator = new MainWindowWorkflowCoordinator();
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
        var coordinator = new MainWindowWorkflowCoordinator();

        WacomServiceMenuState menu = coordinator.BuildWacomServiceMenuState(
            new WacomServiceStatus("Service: running", CanStart: false));

        Assert.Equal("Service: running", menu.Header);
        Assert.False(menu.StartEnabled);
    }
}
