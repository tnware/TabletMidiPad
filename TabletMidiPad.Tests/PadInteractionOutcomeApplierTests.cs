using System.Windows;
using TabletMidiPad.Core.Interaction;
using TabletMidiPad.ViewModels;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class PadInteractionOutcomeApplierTests
{
    [Fact]
    public void ApplySendsCcAndNoteThroughMidiEngine()
    {
        var engine = new StubMidiEngine();
        var sut = new PadInteractionOutcomeApplier(engine);
        var vm = new MainViewModel();
        var config = new AppConfig();
        config.Zones.Clear();
        config.Zones.Add(ZoneConfig.CreateDefault());
        var state = new InteractionState();
        var result = new InteractionResult(
            ActiveZoneIndex: 0,
            ActiveZoneLabel: "z",
            ValueLabel: "v",
            ModifiersLabel: "m",
            MidiMessages: new List<MidiMessage>
            {
                MidiMessage.Cc(1, 10, 64, false),
                MidiMessage.Note(1, 60, true, 100, true),
                MidiMessage.Note(1, 60, false, 0, false),
            });

        sut.Apply(result, state, config, vm);

        Assert.Single(engine.CcCalls);
        Assert.Equal((1, 10, 64, false), engine.CcCalls[0]);
        Assert.Equal(2, engine.NoteCalls.Count);
        Assert.Equal((1, 60, true, 100, true), engine.NoteCalls[0]);
        Assert.Equal((1, 60, false, 0, false), engine.NoteCalls[1]);
        Assert.Equal("z", vm.ActiveZoneText);
        Assert.Equal("v", vm.ValueText);
        Assert.Equal("m", vm.ModifiersText);
    }

    [Fact]
    public void ApplyBuildsLastZoneMarkersFromState()
    {
        var engine = new StubMidiEngine();
        var sut = new PadInteractionOutcomeApplier(engine);
        var vm = new MainViewModel();
        var config = new AppConfig();
        config.Zones.Clear();
        var z0 = new ZoneConfig { Name = "A" };
        var z1 = new ZoneConfig { Name = "B" };
        config.Zones.Add(z0);
        config.Zones.Add(z1);
        var state = new InteractionState();
        state.LastLocalByZone[0] = (0.1, 0.2);
        state.LastLocalByZone[1] = (0.3, 0.4);
        var result = new InteractionResult(-1, string.Empty, string.Empty, string.Empty, new List<MidiMessage>());

        sut.Apply(result, state, config, vm);

        Assert.Equal(2, sut.LastZoneMarkers.Count);
        Assert.Equal(new Point(0.1, 0.2), sut.LastZoneMarkers[z0]);
        Assert.Equal(new Point(0.3, 0.4), sut.LastZoneMarkers[z1]);
    }

    [Fact]
    public void ConstructorNullEngineThrows()
    {
        Assert.Throws<ArgumentNullException>(() => new PadInteractionOutcomeApplier(null!));
    }

    private sealed class StubMidiEngine : IMidiEngine
    {
        public List<(int ch, int cc, int val, bool force)> CcCalls { get; } = [];

        public List<(int ch, int note, bool on, int vel, bool force)> NoteCalls { get; } = [];

        public bool IsReady => true;

        public void Initialize(AppConfig config)
        {
        }

        public Task InitializeAsync(AppConfig config, Action<string>? onProgress = null)
            => Task.CompletedTask;

        public void Restart(AppConfig config)
        {
        }

        public Task RestartAsync(AppConfig config, Action<string>? onProgress = null) => Task.CompletedTask;

        public void Shutdown(AppConfig config)
        {
        }

        public void SendCcIfChanged(int channel, int cc, int value, bool force = false)
            => CcCalls.Add((channel, cc, value, force));

        public void SendNoteIfChanged(int channel, int note, bool on, int velocity, bool force = false)
            => NoteCalls.Add((channel, note, on, velocity, force));

        public void ClearCachedValues()
        {
        }
    }
}
