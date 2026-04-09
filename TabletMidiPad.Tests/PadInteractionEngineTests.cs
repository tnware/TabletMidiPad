using TabletMidiPad.Core.Interaction;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class PadInteractionEngineTests
{
    [Theory]
    [InlineData("Linear", 0, 0)]
    [InlineData("Linear", 127, 127)]
    [InlineData("Logarithmic", 64, 90)] // sqrt(64/127)*127 ~= 90
    [InlineData("Exponential", 64, 32)] // (64/127)^2*127 ~= 32
    public void PressureCurveMatchesExpectedMapping(string curve, int inputPressure, int expected)
    {
        var engine = new PadInteractionEngine();
        var config = new PadInteractionConfig(
            SendPressureGlobally: true,
            AllowZoneSwitchWhilePressed: true,
            MousePressureValue: 127,
            PositionDeadZone: 0.0,
            PositionSmoothing: 0.0,
            PressureCurve: curve,
            LowerPenButtonAction: InteractionConstants.PenButtonActions.None,
            LowerPenButtonMidiChannel: 1,
            LowerPenButtonCc: 64,
            LowerPenButtonNote: 60,
            UpperPenButtonAction: InteractionConstants.PenButtonActions.None,
            UpperPenButtonMidiChannel: 1,
            UpperPenButtonCc: 65,
            UpperPenButtonNote: 61,
            ExpressKeys: new List<ExpressKeyConfigSnapshot>());

        ZoneSnapshot[] zones = new[]
        {
            new ZoneSnapshot(
                Name: "Z",
                Enabled: true,
                MidiChannel: 1,
                CcX: 10,
                CcY: 11,
                CcPressure: 12,
                SendX: false,
                SendY: false,
                InvertX: false,
                InvertY: false,
                SendPressure: true,
                ReleaseBehavior: InteractionConstants.ZoneReleaseBehaviors.NoSendOnRelease,
                Left: 0.0,
                Top: 0.0,
                Width: 1.0,
                Height: 1.0),
        };

        var state = new InteractionState();
        InteractionResult result = engine.Process(
            config,
            zones,
            state,
            new InteractionInput(
                XNorm: 0.5,
                YNorm: 0.5,
                Pressure: inputPressure,
                IsDown: true,
                IsHovering: false,
                PenButtons: 0,
                Backend: InteractionBackend.Wintab));

        Assert.Single(result.MidiMessages);
        Assert.Equal(MidiMessageKind.ControlChange, result.MidiMessages[0].Kind);
        Assert.Equal(expected, result.MidiMessages[0].Value);
    }

    [Fact]
    public void ReleaseBehaviorResetAllSendsXYAndPressureZeros()
    {
        var engine = new PadInteractionEngine();
        var config = new PadInteractionConfig(
            SendPressureGlobally: true,
            AllowZoneSwitchWhilePressed: true,
            MousePressureValue: 127,
            PositionDeadZone: 0.0,
            PositionSmoothing: 0.0,
            PressureCurve: "Linear",
            LowerPenButtonAction: InteractionConstants.PenButtonActions.None,
            LowerPenButtonMidiChannel: 1,
            LowerPenButtonCc: 64,
            LowerPenButtonNote: 60,
            UpperPenButtonAction: InteractionConstants.PenButtonActions.None,
            UpperPenButtonMidiChannel: 1,
            UpperPenButtonCc: 65,
            UpperPenButtonNote: 61,
            ExpressKeys: new List<ExpressKeyConfigSnapshot>());

        ZoneSnapshot[] zones = new[]
        {
            new ZoneSnapshot(
                Name: "Z",
                Enabled: true,
                MidiChannel: 2,
                CcX: 20,
                CcY: 21,
                CcPressure: 22,
                SendX: true,
                SendY: true,
                InvertX: false,
                InvertY: false,
                SendPressure: true,
                ReleaseBehavior: InteractionConstants.ZoneReleaseBehaviors.ResetAll,
                Left: 0.0,
                Top: 0.0,
                Width: 1.0,
                Height: 1.0),
        };

        var state = new InteractionState();

        _ = engine.Process(
            config,
            zones,
            state,
            new InteractionInput(
                XNorm: 0.25,
                YNorm: 0.75,
                Pressure: 100,
                IsDown: true,
                IsHovering: false,
                PenButtons: 0,
                Backend: InteractionBackend.Wintab));

        InteractionResult released = engine.Process(
            config,
            zones,
            state,
            new InteractionInput(
                XNorm: 0.25,
                YNorm: 0.75,
                Pressure: 0,
                IsDown: false,
                IsHovering: false,
                PenButtons: 0,
                Backend: InteractionBackend.Wintab));

        Assert.Equal(3, released.MidiMessages.Count);
        Assert.Contains(released.MidiMessages, m => m.Kind == MidiMessageKind.ControlChange && m.Channel == 2 && m.Number == 20 && m.Value == 0 && m.Force);
        Assert.Contains(released.MidiMessages, m => m.Kind == MidiMessageKind.ControlChange && m.Channel == 2 && m.Number == 21 && m.Value == 0 && m.Force);
        Assert.Contains(released.MidiMessages, m => m.Kind == MidiMessageKind.ControlChange && m.Channel == 2 && m.Number == 22 && m.Value == 0 && m.Force);
    }

    [Fact]
    public void PenButtonCcMomentaryEmitsOnPressAndOffOnRelease()
    {
        var engine = new PadInteractionEngine();
        var config = new PadInteractionConfig(
            SendPressureGlobally: false,
            AllowZoneSwitchWhilePressed: true,
            MousePressureValue: 127,
            PositionDeadZone: 0.0,
            PositionSmoothing: 0.0,
            PressureCurve: "Linear",
            LowerPenButtonAction: InteractionConstants.PenButtonActions.CcMomentary,
            LowerPenButtonMidiChannel: 3,
            LowerPenButtonCc: 64,
            LowerPenButtonNote: 60,
            UpperPenButtonAction: InteractionConstants.PenButtonActions.None,
            UpperPenButtonMidiChannel: 1,
            UpperPenButtonCc: 65,
            UpperPenButtonNote: 61,
            ExpressKeys: new List<ExpressKeyConfigSnapshot>());

        ZoneSnapshot[] zones = new[]
        {
            new ZoneSnapshot(
                Name: "Z",
                Enabled: true,
                MidiChannel: 1,
                CcX: 10,
                CcY: 11,
                CcPressure: 12,
                SendX: false,
                SendY: false,
                InvertX: false,
                InvertY: false,
                SendPressure: false,
                ReleaseBehavior: InteractionConstants.ZoneReleaseBehaviors.NoSendOnRelease,
                Left: 0.0,
                Top: 0.0,
                Width: 1.0,
                Height: 1.0),
        };

        var state = new InteractionState();

        // Press lower pen button: bit 1 (0x0002).
        InteractionResult pressed = engine.Process(
            config,
            zones,
            state,
            new InteractionInput(
                XNorm: 0.5,
                YNorm: 0.5,
                Pressure: 0,
                IsDown: true,
                IsHovering: false,
                PenButtons: 0x0002,
                Backend: InteractionBackend.Wintab));

        Assert.Contains(pressed.MidiMessages, m =>
            m.Kind == MidiMessageKind.ControlChange &&
            m.Channel == 3 &&
            m.Number == 64 &&
            m.Value == 127 &&
            m.Force);

        // Release lower pen button while still down.
        InteractionResult released = engine.Process(
            config,
            zones,
            state,
            new InteractionInput(
                XNorm: 0.5,
                YNorm: 0.5,
                Pressure: 0,
                IsDown: true,
                IsHovering: false,
                PenButtons: 0x0000,
                Backend: InteractionBackend.Wintab));

        Assert.Contains(released.MidiMessages, m =>
            m.Kind == MidiMessageKind.ControlChange &&
            m.Channel == 3 &&
            m.Number == 64 &&
            m.Value == 0 &&
            m.Force);
    }

    [Fact]
    public void ScaleBreathEmitsNoteOnAndLegatoSwitchWhenCrossingHole()
    {
        var engine = new PadInteractionEngine();
        var config = new PadInteractionConfig(
            SendPressureGlobally: true,
            AllowZoneSwitchWhilePressed: true,
            MousePressureValue: 127,
            PositionDeadZone: 0.0,
            PositionSmoothing: 0.0,
            PressureCurve: "Linear",
            LowerPenButtonAction: InteractionConstants.PenButtonActions.None,
            LowerPenButtonMidiChannel: 1,
            LowerPenButtonCc: 64,
            LowerPenButtonNote: 60,
            UpperPenButtonAction: InteractionConstants.PenButtonActions.None,
            UpperPenButtonMidiChannel: 1,
            UpperPenButtonCc: 65,
            UpperPenButtonNote: 61,
            ExpressKeys: new List<ExpressKeyConfigSnapshot>());

        ZoneSnapshot[] zones = new[]
        {
            new ZoneSnapshot(
                Name: "S",
                Enabled: true,
                MidiChannel: 2,
                CcX: 20,
                CcY: 21,
                CcPressure: 22,
                SendX: false,
                SendY: false,
                InvertX: false,
                InvertY: false,
                SendPressure: false,
                ReleaseBehavior: InteractionConstants.ZoneReleaseBehaviors.NoSendOnRelease,
                Left: 0.0,
                Top: 0.0,
                Width: 1.0,
                Height: 1.0)
            {
                OutputMode = InteractionConstants.ZoneOutputModes.ScaleBreath,
                ScaleBreathRoot = 0,
                ScaleBreathScale = InteractionConstants.ScaleBreathScales.Major,
                ScaleBreathOctave = 4,
                ScaleBreathHoleCount = 10,
                ScaleBreathRowSplit = 0.5
            },
        };

        var state = new InteractionState();

        InteractionResult first = engine.Process(
            config,
            zones,
            state,
            new InteractionInput(
                XNorm: 0.02,
                YNorm: 0.25,
                Pressure: 100,
                IsDown: true,
                IsHovering: false,
                PenButtons: 0,
                Backend: InteractionBackend.Wintab));

        MidiMessage on = Assert.Single(first.MidiMessages);
        Assert.Equal(MidiMessageKind.Note, on.Kind);
        Assert.Equal(2, on.Channel);
        Assert.True(on.Value > 0);

        InteractionResult second = engine.Process(
            config,
            zones,
            state,
            new InteractionInput(
                XNorm: 0.55,
                YNorm: 0.25,
                Pressure: 100,
                IsDown: true,
                IsHovering: false,
                PenButtons: 0,
                Backend: InteractionBackend.Wintab));

        Assert.Equal(2, second.MidiMessages.Count);
        Assert.Equal(MidiMessageKind.Note, second.MidiMessages[0].Kind);
        Assert.Equal(0, second.MidiMessages[0].Value);
        Assert.Equal(MidiMessageKind.Note, second.MidiMessages[1].Kind);
        Assert.True(second.MidiMessages[1].Value > 0);
    }

    [Fact]
    public void ScaleBreathReleaseAlwaysEmitsNoteOff()
    {
        var engine = new PadInteractionEngine();
        var config = new PadInteractionConfig(
            SendPressureGlobally: true,
            AllowZoneSwitchWhilePressed: true,
            MousePressureValue: 127,
            PositionDeadZone: 0.0,
            PositionSmoothing: 0.0,
            PressureCurve: "Linear",
            LowerPenButtonAction: InteractionConstants.PenButtonActions.None,
            LowerPenButtonMidiChannel: 1,
            LowerPenButtonCc: 64,
            LowerPenButtonNote: 60,
            UpperPenButtonAction: InteractionConstants.PenButtonActions.None,
            UpperPenButtonMidiChannel: 1,
            UpperPenButtonCc: 65,
            UpperPenButtonNote: 61,
            ExpressKeys: new List<ExpressKeyConfigSnapshot>());

        ZoneSnapshot[] zones = new[]
        {
            new ZoneSnapshot(
                Name: "S",
                Enabled: true,
                MidiChannel: 3,
                CcX: 20,
                CcY: 21,
                CcPressure: 22,
                SendX: true,
                SendY: true,
                InvertX: false,
                InvertY: false,
                SendPressure: true,
                ReleaseBehavior: InteractionConstants.ZoneReleaseBehaviors.Hold,
                Left: 0.0,
                Top: 0.0,
                Width: 1.0,
                Height: 1.0)
            {
                OutputMode = InteractionConstants.ZoneOutputModes.ScaleBreath
            },
        };

        var state = new InteractionState();
        _ = engine.Process(
            config,
            zones,
            state,
            new InteractionInput(0.3, 0.8, 90, true, false, 0, InteractionBackend.Wintab));

        InteractionResult release = engine.Process(
            config,
            zones,
            state,
            new InteractionInput(0.3, 0.8, 0, false, false, 0, InteractionBackend.Wintab));

        Assert.Contains(release.MidiMessages, m =>
            m.Kind == MidiMessageKind.Note &&
            m.Channel == 3 &&
            m.Value == 0 &&
            m.Force);
    }

    [Fact]
    public void ScaleBreathCanDisableAllCcModulation()
    {
        var engine = new PadInteractionEngine();
        var config = new PadInteractionConfig(
            SendPressureGlobally: true,
            AllowZoneSwitchWhilePressed: true,
            MousePressureValue: 127,
            PositionDeadZone: 0.0,
            PositionSmoothing: 0.0,
            PressureCurve: "Linear",
            LowerPenButtonAction: InteractionConstants.PenButtonActions.None,
            LowerPenButtonMidiChannel: 1,
            LowerPenButtonCc: 64,
            LowerPenButtonNote: 60,
            UpperPenButtonAction: InteractionConstants.PenButtonActions.None,
            UpperPenButtonMidiChannel: 1,
            UpperPenButtonCc: 65,
            UpperPenButtonNote: 61,
            ExpressKeys: new List<ExpressKeyConfigSnapshot>());

        ZoneSnapshot[] zones = new[]
        {
            new ZoneSnapshot(
                Name: "S",
                Enabled: true,
                MidiChannel: 1,
                CcX: 20,
                CcY: 21,
                CcPressure: 22,
                SendX: true,
                SendY: true,
                InvertX: false,
                InvertY: false,
                SendPressure: true,
                ReleaseBehavior: InteractionConstants.ZoneReleaseBehaviors.NoSendOnRelease,
                Left: 0.0,
                Top: 0.0,
                Width: 1.0,
                Height: 1.0)
            {
                OutputMode = InteractionConstants.ZoneOutputModes.ScaleBreath,
                ScaleBreathSendCc = false
            },
        };

        var state = new InteractionState();
        InteractionResult result = engine.Process(
            config,
            zones,
            state,
            new InteractionInput(0.2, 0.2, 127, true, false, 0, InteractionBackend.MouseFallback));

        MidiMessage note = Assert.Single(result.MidiMessages);
        Assert.Equal(MidiMessageKind.Note, note.Kind);
    }

    [Fact]
    public void ScaleGridMapsCellsToScaleNotesAndCanDisableCc()
    {
        var engine = new PadInteractionEngine();
        var config = new PadInteractionConfig(
            SendPressureGlobally: true,
            AllowZoneSwitchWhilePressed: true,
            MousePressureValue: 127,
            PositionDeadZone: 0.0,
            PositionSmoothing: 0.0,
            PressureCurve: "Linear",
            LowerPenButtonAction: InteractionConstants.PenButtonActions.None,
            LowerPenButtonMidiChannel: 1,
            LowerPenButtonCc: 64,
            LowerPenButtonNote: 60,
            UpperPenButtonAction: InteractionConstants.PenButtonActions.None,
            UpperPenButtonMidiChannel: 1,
            UpperPenButtonCc: 65,
            UpperPenButtonNote: 61,
            ExpressKeys: new List<ExpressKeyConfigSnapshot>());

        ZoneSnapshot[] zones = new[]
        {
            new ZoneSnapshot(
                Name: "G",
                Enabled: true,
                MidiChannel: 4,
                CcX: 20,
                CcY: 21,
                CcPressure: 22,
                SendX: true,
                SendY: true,
                InvertX: false,
                InvertY: false,
                SendPressure: true,
                ReleaseBehavior: InteractionConstants.ZoneReleaseBehaviors.NoSendOnRelease,
                Left: 0.0,
                Top: 0.0,
                Width: 1.0,
                Height: 1.0)
            {
                OutputMode = InteractionConstants.ZoneOutputModes.ScaleGrid,
                ScaleGridRoot = 0,
                ScaleGridScale = InteractionConstants.ScaleBreathScales.Major,
                ScaleGridOctave = 4,
                ScaleGridRows = 2,
                ScaleGridColumns = 2,
                ScaleGridSendCc = false
            },
        };

        var state = new InteractionState();
        InteractionResult first = engine.Process(
            config,
            zones,
            state,
            new InteractionInput(0.1, 0.8, 127, true, false, 0, InteractionBackend.MouseFallback));

        MidiMessage firstOn = Assert.Single(first.MidiMessages);
        Assert.Equal(MidiMessageKind.Note, firstOn.Kind);
        Assert.Equal(4, firstOn.Channel);
        Assert.Equal(60, firstOn.Number);
        Assert.Equal(127, firstOn.Value);

        InteractionResult second = engine.Process(
            config,
            zones,
            state,
            new InteractionInput(0.1, 0.2, 127, true, false, 0, InteractionBackend.MouseFallback));

        Assert.Equal(2, second.MidiMessages.Count);
        Assert.Equal(MidiMessageKind.Note, second.MidiMessages[0].Kind);
        Assert.Equal(60, second.MidiMessages[0].Number);
        Assert.Equal(0, second.MidiMessages[0].Value);
        Assert.Equal(MidiMessageKind.Note, second.MidiMessages[1].Kind);
        Assert.Equal(64, second.MidiMessages[1].Number);
        Assert.Equal(127, second.MidiMessages[1].Value);
    }
}
