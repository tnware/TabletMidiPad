using System.Collections.Generic;
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
    public void PressureCurve_MatchesExpectedMapping(string curve, int inputPressure, int expected)
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

        var zones = new[]
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
    public void ReleaseBehavior_ResetAll_SendsXYAndPressureZeros()
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

        var zones = new[]
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
}

