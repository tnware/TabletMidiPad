using Xunit;

namespace TabletMidiPad.Tests;

public sealed class SettingsWorkflowCoordinatorTests
{
    [Fact]
    public void GetEffectiveExpressKeyCountReturnsMaxConfiguredOrDetected()
    {
        SettingsWorkflowCoordinator coordinator = CreateCoordinator();

        int result = coordinator.GetEffectiveExpressKeyCount(4, 8);

        Assert.Equal(8, result);
    }

    [Fact]
    public void ValidateAndSaveReturnsFailureForOutOfBoundsZone()
    {
        SettingsWorkflowCoordinator coordinator = CreateCoordinator();
        AppConfig config = AppConfig.Default;
        config.Zones =
        [
            new()
            {
                Name = "Bad",
                Width = 0.9,
                Height = 0.9,
                Left = 0.2,
                Top = 0.2
            }
        ];

        WorkflowSaveResult result = coordinator.ValidateAndSave(config);

        Assert.False(result.Success);
        Assert.Contains("must fit within the pad bounds", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyPreferencesDetectsRoutingAndInputBackendChanges()
    {
        SettingsWorkflowCoordinator coordinator = CreateCoordinator();
        AppConfig config = AppConfig.Default;
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
            ExpressKeys = [],
            ZoneOverlayStyle = ZoneOverlayStyle.Plain
        };

        PreferencesWorkflowResult apply = coordinator.ApplyPreferences(config, result, IntPtr.Zero);

        Assert.True(apply.SaveSucceeded);
        Assert.True(apply.ShouldRestartMidi);
        Assert.True(apply.ShouldRebindInputBackend);
    }

    [Fact]
    public void ApplyPreferencesWhenZonesInvalidReturnsSaveFailed()
    {
        SettingsWorkflowCoordinator coordinator = CreateCoordinator();
        AppConfig config = AppConfig.Default;
        config.Zones = [];

        PreferencesWorkflowResult apply = coordinator.ApplyPreferences(config, PreferencesMatching(config), IntPtr.Zero);

        Assert.False(apply.SaveSucceeded);
        Assert.False(string.IsNullOrEmpty(apply.ErrorMessage));
        Assert.False(apply.ShouldRestartMidi);
        Assert.False(apply.ShouldRebindInputBackend);
    }

    [Fact]
    public void ApplyPreferencesWhenUnchangedWithOpenMidiDoesNotFlagRestartOrRebind()
    {
        SettingsWorkflowCoordinator coordinator = CreateCoordinator();
        AppConfig config = AppConfig.Default;

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
            ExpressKeys = [.. config.ExpressKeys],
            ZoneOverlayStyle = config.ZoneOverlayStyle,
        };
    }

    private static SettingsWorkflowCoordinator CreateCoordinator() => new(new ZoneConfigurationService());
}
