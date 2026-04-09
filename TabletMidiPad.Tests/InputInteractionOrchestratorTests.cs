using TabletMidiPad.Core.Interaction;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class InputInteractionOrchestratorTests
{
    [Fact]
    public void ProcessTickDelegatesToPadInteractionEngine()
    {
        var expected = new InteractionResult(
            ActiveZoneIndex: 0,
            ActiveZoneLabel: "A",
            ValueLabel: "V",
            ModifiersLabel: "M",
            MidiMessages: new List<MidiMessage>());
        var engine = new StubPadInteractionEngine(expected);
        var sut = new InputInteractionOrchestrator(engine);

        PadInteractionConfig config = BuildConfig();
        IReadOnlyList<ZoneSnapshot> zones = BuildZones();
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
    public void ProcessReleaseUsesUnknownBackendAndProvidedButtons()
    {
        var expected = new InteractionResult(
            ActiveZoneIndex: -1,
            ActiveZoneLabel: "none",
            ValueLabel: "-",
            ModifiersLabel: "none",
            MidiMessages: new List<MidiMessage>());
        var engine = new StubPadInteractionEngine(expected);
        var sut = new InputInteractionOrchestrator(engine);

        PadInteractionConfig config = BuildConfig();
        IReadOnlyList<ZoneSnapshot> zones = BuildZones();
        var state = new InteractionState();

        InteractionResult actual = sut.ProcessRelease(config, zones, state, 0x0022u);

        Assert.Equal(expected, actual);
        Assert.False(engine.LastInput.IsDown);
        Assert.False(engine.LastInput.IsHovering);
        Assert.Equal(0x0022u, engine.LastInput.PenButtons);
        Assert.Equal(InteractionBackend.Unknown, engine.LastInput.Backend);
    }

    [Fact]
    public void ConstructorNullEngineThrows()
    {
        Assert.Throws<ArgumentNullException>(() => new InputInteractionOrchestrator(null!));
    }

    [Fact]
    public void BuildPadInteractionConfigNullAppConfigThrows()
    {
        var sut = new InputInteractionOrchestrator(new StubPadInteractionEngine(BuildEmptyResult()));

        Assert.Throws<ArgumentNullException>(() => sut.BuildPadInteractionConfig(null!));
    }

    [Fact]
    public void BuildPadInteractionConfigMapsAppConfig()
    {
        var sut = new InputInteractionOrchestrator(new StubPadInteractionEngine(BuildEmptyResult()));
        var app = new AppConfig
        {
            SendPressureGlobally = false,
            AllowZoneSwitchWhilePressed = false,
            MousePressureValue = 99,
            PositionDeadZone = 0.1,
            PositionSmoothing = 0.2,
            PressureCurve = "Exponential",
            LowerPenButtonAction = PenButtonActions.LockX,
            LowerPenButtonMidiChannel = 2,
            LowerPenButtonCc = 10,
            LowerPenButtonNote = 20,
            UpperPenButtonAction = PenButtonActions.CcToggle,
            UpperPenButtonMidiChannel = 3,
            UpperPenButtonCc = 11,
            UpperPenButtonNote = 21,
            ExpressKeys =
            {
                new ExpressKeyConfig { Action = PenButtonActions.NoteMomentary, MidiChannel = 5, Cc = 30, Note = 40 },
            },
        };

        PadInteractionConfig built = sut.BuildPadInteractionConfig(app);

        Assert.False(built.SendPressureGlobally);
        Assert.False(built.AllowZoneSwitchWhilePressed);
        Assert.Equal(99, built.MousePressureValue);
        Assert.Equal(0.1, built.PositionDeadZone);
        Assert.Equal(0.2, built.PositionSmoothing);
        Assert.Equal("Exponential", built.PressureCurve);
        Assert.Equal(PenButtonActions.LockX, built.LowerPenButtonAction);
        Assert.Equal(2, built.LowerPenButtonMidiChannel);
        Assert.Equal(10, built.LowerPenButtonCc);
        Assert.Equal(20, built.LowerPenButtonNote);
        Assert.Equal(PenButtonActions.CcToggle, built.UpperPenButtonAction);
        Assert.Equal(3, built.UpperPenButtonMidiChannel);
        Assert.Equal(11, built.UpperPenButtonCc);
        Assert.Equal(21, built.UpperPenButtonNote);
        Assert.Single(built.ExpressKeys);
        Assert.Equal(PenButtonActions.NoteMomentary, built.ExpressKeys[0].Action);
        Assert.Equal(5, built.ExpressKeys[0].MidiChannel);
        Assert.Equal(30, built.ExpressKeys[0].Cc);
        Assert.Equal(40, built.ExpressKeys[0].Note);
    }

    [Fact]
    public void BuildZoneSnapshotsNullPageThrows()
    {
        var sut = new InputInteractionOrchestrator(new StubPadInteractionEngine(BuildEmptyResult()));

        Assert.Throws<ArgumentNullException>(() => sut.BuildZoneSnapshots(null!));
    }

    [Fact]
    public void BuildZoneSnapshotsMapsZones()
    {
        var sut = new InputInteractionOrchestrator(new StubPadInteractionEngine(BuildEmptyResult()));
        var page = new PageConfig
        {
            Zones =
            [
                new ZoneConfig
                {
                    Name = "A",
                    Enabled = false,
                    MidiChannel = 7,
                    CcX = 1,
                    CcY = 2,
                    CcPressure = 3,
                    SendX = false,
                    SendY = true,
                    InvertX = true,
                    InvertY = false,
                    SendPressure = false,
                    ReleaseBehavior = ZoneReleaseBehaviors.Hold,
                    OutputMode = ZoneOutputModes.ScaleBreath,
                    ScaleBreathRoot = 7,
                    ScaleBreathScale = ScaleBreathScaleNames.MinorPentatonic,
                    ScaleBreathOctave = 3,
                    ScaleBreathHoleCount = 9,
                    ScaleBreathRowSplit = 0.4,
                    ScaleBreathSendCc = false,
                    ScaleGridRoot = 5,
                    ScaleGridScale = ScaleBreathScaleNames.MajorPentatonic,
                    ScaleGridOctave = 2,
                    ScaleGridRows = 6,
                    ScaleGridColumns = 7,
                    ScaleGridSendCc = false,
                    Left = 0.1,
                    Top = 0.2,
                    Width = 0.3,
                    Height = 0.4,
                },
            ],
        };

        IReadOnlyList<ZoneSnapshot> zones = sut.BuildZoneSnapshots(page);

        ZoneSnapshot z = Assert.Single(zones);
        Assert.Equal("A", z.Name);
        Assert.False(z.Enabled);
        Assert.Equal(7, z.MidiChannel);
        Assert.Equal(1, z.CcX);
        Assert.Equal(2, z.CcY);
        Assert.Equal(3, z.CcPressure);
        Assert.False(z.SendX);
        Assert.True(z.SendY);
        Assert.True(z.InvertX);
        Assert.False(z.InvertY);
        Assert.False(z.SendPressure);
        Assert.Equal(ZoneReleaseBehaviors.Hold, z.ReleaseBehavior);
        Assert.Equal(ZoneOutputModes.ScaleBreath, z.OutputMode);
        Assert.Equal(7, z.ScaleBreathRoot);
        Assert.Equal(ScaleBreathScaleNames.MinorPentatonic, z.ScaleBreathScale);
        Assert.Equal(3, z.ScaleBreathOctave);
        Assert.Equal(9, z.ScaleBreathHoleCount);
        Assert.Equal(0.4, z.ScaleBreathRowSplit);
        Assert.False(z.ScaleBreathSendCc);
        Assert.Equal(5, z.ScaleGridRoot);
        Assert.Equal(ScaleBreathScaleNames.MajorPentatonic, z.ScaleGridScale);
        Assert.Equal(2, z.ScaleGridOctave);
        Assert.Equal(6, z.ScaleGridRows);
        Assert.Equal(7, z.ScaleGridColumns);
        Assert.False(z.ScaleGridSendCc);
        Assert.Equal(0.1, z.Left);
        Assert.Equal(0.2, z.Top);
        Assert.Equal(0.3, z.Width);
        Assert.Equal(0.4, z.Height);
    }

    private static InteractionResult BuildEmptyResult()
    {
        return new InteractionResult(
            ActiveZoneIndex: -1,
            ActiveZoneLabel: string.Empty,
            ValueLabel: string.Empty,
            ModifiersLabel: string.Empty,
            MidiMessages: new List<MidiMessage>());
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
}
