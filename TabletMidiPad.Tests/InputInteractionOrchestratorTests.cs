using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using TabletMidiPad.Core.Interaction;
using TabletMidiPad.Services;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class InputInteractionOrchestratorTests
{
    [Fact]
    public void ProcessTick_DelegatesToPadInteractionEngine()
    {
        var expected = new InteractionResult(
            ActiveZoneIndex: 0,
            ActiveZoneLabel: "A",
            ValueLabel: "V",
            ModifiersLabel: "M",
            MidiMessages: new List<MidiMessage>());
        var engine = new StubPadInteractionEngine(expected);
        var fallback = new StubInputFallbackService();
        var sut = new InputInteractionOrchestrator(engine, fallback);

        var config = BuildConfig();
        var zones = BuildZones();
        var state = new InteractionState();
        var input = new InteractionInput(0.5, 0.5, 40, true, false, 7, InteractionBackend.Wintab);

        InteractionResult actual = sut.ProcessTick(config, zones, state, input);

        Assert.Equal(expected, actual);
        Assert.Same(config, engine.LastConfig);
        Assert.Same(zones, engine.LastZones);
        Assert.Same(state, engine.LastState);
        Assert.Equal(input, engine.LastInput);
    }

    [Fact]
    public void ProcessRelease_UsesUnknownBackendAndProvidedButtons()
    {
        var expected = new InteractionResult(
            ActiveZoneIndex: -1,
            ActiveZoneLabel: "none",
            ValueLabel: "-",
            ModifiersLabel: "none",
            MidiMessages: new List<MidiMessage>());
        var engine = new StubPadInteractionEngine(expected);
        var fallback = new StubInputFallbackService();
        var sut = new InputInteractionOrchestrator(engine, fallback);

        var config = BuildConfig();
        var zones = BuildZones();
        var state = new InteractionState();

        InteractionResult actual = sut.ProcessRelease(config, zones, state, 0x0022u);

        Assert.Equal(expected, actual);
        Assert.False(engine.LastInput.IsDown);
        Assert.False(engine.LastInput.IsHovering);
        Assert.Equal(0x0022u, engine.LastInput.PenButtons);
        Assert.Equal(InteractionBackend.Unknown, engine.LastInput.Backend);
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        var fallback = new StubInputFallbackService();
        var engine = new StubPadInteractionEngine(new InteractionResult(-1, "-", "-", "-", new List<MidiMessage>()));
        Assert.Throws<System.ArgumentNullException>(() => new InputInteractionOrchestrator(null!, fallback));
        Assert.Throws<System.ArgumentNullException>(() => new InputInteractionOrchestrator(engine, null!));
    }

    private static PadInteractionConfig BuildConfig()
    {
        return new PadInteractionConfig(
            SendPressureGlobally: true,
            AllowZoneSwitchWhilePressed: true,
            MousePressureValue: 100,
            PositionDeadZone: 0.0,
            PositionSmoothing: 0.0,
            PressureCurve: "Linear",
            LowerPenButtonAction: PenButtonActions.None,
            LowerPenButtonMidiChannel: 1,
            LowerPenButtonCc: 64,
            LowerPenButtonNote: 60,
            UpperPenButtonAction: PenButtonActions.None,
            UpperPenButtonMidiChannel: 1,
            UpperPenButtonCc: 65,
            UpperPenButtonNote: 61,
            ExpressKeys: new List<ExpressKeyConfigSnapshot>());
    }

    private static IReadOnlyList<ZoneSnapshot> BuildZones()
    {
        return new List<ZoneSnapshot>
        {
            new(
                Name: "Z",
                Enabled: true,
                MidiChannel: 1,
                CcX: 10,
                CcY: 11,
                CcPressure: 12,
                SendX: true,
                SendY: true,
                InvertX: false,
                InvertY: false,
                SendPressure: true,
                ReleaseBehavior: ZoneReleaseBehaviors.NoSendOnRelease,
                Left: 0,
                Top: 0,
                Width: 1,
                Height: 1),
        };
    }

    private sealed class StubPadInteractionEngine : IPadInteractionEngine
    {
        private readonly InteractionResult _result;

        public StubPadInteractionEngine(InteractionResult result)
        {
            _result = result;
        }

        public PadInteractionConfig? LastConfig { get; private set; }

        public IReadOnlyList<ZoneSnapshot>? LastZones { get; private set; }

        public InteractionState? LastState { get; private set; }

        public InteractionInput LastInput { get; private set; }

        public InteractionResult Process(
            PadInteractionConfig config,
            IReadOnlyList<ZoneSnapshot> zones,
            InteractionState state,
            InteractionInput input)
        {
            LastConfig = config;
            LastZones = zones;
            LastState = state;
            LastInput = input;
            return _result;
        }
    }

    private sealed class StubInputFallbackService : IInputFallbackService
    {
        public bool TryBuildStylusSample(
            StylusEventArgs e,
            IInputElement relativeTo,
            double padWidth,
            double padHeight,
            int mousePressureFallback,
            out InputFallbackSample sample)
        {
            sample = default;
            return false;
        }

        public bool TryBuildMouseSample(
            MouseEventArgs e,
            IInputElement relativeTo,
            double padWidth,
            double padHeight,
            int mousePressureFallback,
            out InputFallbackSample sample)
        {
            sample = default;
            return false;
        }
    }
}
