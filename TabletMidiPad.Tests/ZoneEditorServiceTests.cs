using TabletMidiPad.ViewModels;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class ZoneEditorServiceTests
{
    [Fact]
    public void CommitCurrentZoneEdits_WritesValidatedValuesFromViewModel()
    {
        var parser = new ConfigValueParser();
        var sut = new ZoneEditorService(parser);
        var config = new AppConfig();
        config.Zones.Add(ZoneConfig.CreateDefault());

        var vm = new TestZoneEditorState
        {
            ZoneName = "Z1",
            ZoneEnabled = true,
            ZoneMidiChannel = 2,
            ZoneCcX = "10",
            ZoneCcY = "11",
            ZoneCcPressure = "12",
            ZoneLeft = "0",
            ZoneTop = "0",
            ZoneWidth = "0.5",
            ZoneHeight = "0.5",
            ZoneSendX = true,
            ZoneSendY = false,
            ZoneInvertX = false,
            ZoneInvertY = true,
            ZoneSendPressure = true,
            ZoneReleaseBehavior = ZoneReleaseBehaviors.Hold,
        };

        sut.CommitCurrentZoneEdits(config, vm, 0, "1", "2");

        ZoneConfig z = config.Zones[0];
        Assert.Equal("Z1", z.Name);
        Assert.True(z.Enabled);
        Assert.Equal(2, z.MidiChannel);
        Assert.Equal(10, z.CcX);
        Assert.Equal(11, z.CcY);
        Assert.Equal(12, z.CcPressure);
        Assert.Equal(0.5, z.Width);
        Assert.Equal(0.5, z.Height);
        Assert.Equal(1, config.MaxZoneRows);
        Assert.Equal(2, config.MaxZonesPerRow);
        Assert.Equal(ZoneReleaseBehaviors.Hold, z.ReleaseBehavior);
    }

    [Fact]
    public void LoadSelectedZoneIntoEditor_LoadsMidiChannelAndReleaseBehavior()
    {
        var parser = new ConfigValueParser();
        var sut = new ZoneEditorService(parser);
        var config = new AppConfig();
        config.Zones.Clear();
        config.Zones.Add(new ZoneConfig
        {
            Name = "Z",
            Enabled = true,
            MidiChannel = 7,
            ReleaseBehavior = ZoneReleaseBehaviors.Hold,
        });

        var vm = new TestZoneEditorState();
        sut.LoadSelectedZoneIntoEditor(vm, config, 0);

        Assert.Equal(7, vm.ZoneMidiChannel);
        Assert.Equal(ZoneReleaseBehaviors.Hold, vm.ZoneReleaseBehavior);
        Assert.True(vm.ZoneDetailsEnabled);
    }

    [Fact]
    public void LoadSelectedZoneIntoEditor_NoSelection_ClearsEditor()
    {
        var parser = new ConfigValueParser();
        var sut = new ZoneEditorService(parser);
        var config = AppConfig.Default;
        var vm = new TestZoneEditorState { ZoneMidiChannel = 5, ZoneDetailsEnabled = true };

        sut.LoadSelectedZoneIntoEditor(vm, config, -1);

        Assert.False(vm.ZoneDetailsEnabled);
        Assert.Equal(1, vm.ZoneMidiChannel);
    }

    private sealed class TestZoneEditorState : IZoneEditorState
    {
        public System.Collections.ObjectModel.ObservableCollection<int> MidiChannels { get; } = new();

        public System.Collections.ObjectModel.ObservableCollection<string> ReleaseBehaviorOptions { get; } = new();

        public System.Collections.ObjectModel.ObservableCollection<ZoneListItemViewModel> Zones { get; } = new();

        public string MaxZoneRowsText { get; set; } = string.Empty;

        public string MaxZonesPerRowText { get; set; } = string.Empty;

        public string ZoneSelectionSummaryPrimaryText { get; set; } = string.Empty;

        public string ZoneSelectionSummarySecondaryText { get; set; } = string.Empty;

        public bool ZoneDetailsEnabled { get; set; }

        public string ZoneName { get; set; } = string.Empty;

        public bool ZoneEnabled { get; set; }

        public int ZoneMidiChannel { get; set; } = 1;

        public string ZoneCcX { get; set; } = string.Empty;

        public string ZoneCcY { get; set; } = string.Empty;

        public string ZoneCcPressure { get; set; } = string.Empty;

        public string ZoneLeft { get; set; } = string.Empty;

        public string ZoneTop { get; set; } = string.Empty;

        public string ZoneWidth { get; set; } = string.Empty;

        public string ZoneHeight { get; set; } = string.Empty;

        public bool ZoneSendX { get; set; }

        public bool ZoneSendY { get; set; }

        public bool ZoneInvertX { get; set; }

        public bool ZoneInvertY { get; set; }

        public bool ZoneSendPressure { get; set; }

        public string ZoneReleaseBehavior { get; set; } = string.Empty;
    }
}
