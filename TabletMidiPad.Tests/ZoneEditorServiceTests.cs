using TabletMidiPad.ViewModels;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class ZoneEditorServiceTests
{
    [Fact]
    public void CommitCurrentZoneEditsWritesValidatedValuesFromViewModel()
    {
        var parser = new ConfigValueParser();
        var sut = new ZoneEditorService(parser, new PageNavigationService());
        var config = new AppConfig();
        config.Pages[0].Zones.Clear();
        config.Pages[0].Zones.Add(ZoneConfig.CreateDefault());

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
            ZoneOutputMode = ZoneOutputModes.ScaleBreath,
            ScaleBreathRootText = "2",
            ScaleBreathScale = ScaleBreathScaleNames.NaturalMinor,
            ScaleBreathOctaveText = "3",
            ScaleBreathHoleCountText = "12",
            ScaleBreathRowSplitText = "0.6",
            ScaleBreathSendCc = false,
            ScaleGridRootText = "7",
            ScaleGridScale = ScaleBreathScaleNames.MinorPentatonic,
            ScaleGridOctaveText = "5",
            ScaleGridRowsText = "6",
            ScaleGridColumnsText = "8",
            ScaleGridSendCc = false,
        };

        sut.CommitCurrentZoneEdits(config, vm, 0, "1", "2");

        ZoneConfig z = config.Pages[0].Zones[0];
        Assert.Equal("Z1", z.Name);
        Assert.True(z.Enabled);
        Assert.Equal(2, z.MidiChannel);
        Assert.Equal(10, z.CcX);
        Assert.Equal(11, z.CcY);
        Assert.Equal(12, z.CcPressure);
        Assert.Equal(0.5, z.Width);
        Assert.Equal(0.5, z.Height);
        Assert.Equal(1, config.Pages[0].MaxZoneRows);
        Assert.Equal(2, config.Pages[0].MaxZonesPerRow);
        Assert.Equal(ZoneReleaseBehaviors.Hold, z.ReleaseBehavior);
        Assert.Equal(ZoneOutputModes.ScaleBreath, z.OutputMode);
        Assert.Equal(2, z.ScaleBreathRoot);
        Assert.Equal(ScaleBreathScaleNames.NaturalMinor, z.ScaleBreathScale);
        Assert.Equal(3, z.ScaleBreathOctave);
        Assert.Equal(12, z.ScaleBreathHoleCount);
        Assert.Equal(0.6, z.ScaleBreathRowSplit);
        Assert.False(z.ScaleBreathSendCc);
        Assert.Equal(7, z.ScaleGridRoot);
        Assert.Equal(ScaleBreathScaleNames.MinorPentatonic, z.ScaleGridScale);
        Assert.Equal(5, z.ScaleGridOctave);
        Assert.Equal(6, z.ScaleGridRows);
        Assert.Equal(8, z.ScaleGridColumns);
        Assert.False(z.ScaleGridSendCc);
    }

    [Fact]
    public void LoadSelectedZoneIntoEditorLoadsMidiChannelAndReleaseBehavior()
    {
        var parser = new ConfigValueParser();
        var sut = new ZoneEditorService(parser, new PageNavigationService());
        var config = new AppConfig();
        config.Pages[0].Zones.Clear();
        config.Pages[0].Zones.Add(new ZoneConfig
        {
            Name = "Z",
            Enabled = true,
            MidiChannel = 7,
            ReleaseBehavior = ZoneReleaseBehaviors.Hold,
            OutputMode = ZoneOutputModes.ScaleBreath,
            ScaleGridRoot = 9,
            ScaleGridScale = ScaleBreathScaleNames.MajorPentatonic,
            ScaleGridOctave = 2,
            ScaleGridRows = 7,
            ScaleGridColumns = 5,
            ScaleGridSendCc = false,
        });

        var vm = new TestZoneEditorState();
        sut.LoadSelectedZoneIntoEditor(vm, config, 0);

        Assert.Equal(7, vm.ZoneMidiChannel);
        Assert.Equal(ZoneReleaseBehaviors.Hold, vm.ZoneReleaseBehavior);
        Assert.Equal(ZoneOutputModes.ScaleBreath, vm.ZoneOutputMode);
        Assert.Equal("9", vm.ScaleGridRootText);
        Assert.Equal(ScaleBreathScaleNames.MajorPentatonic, vm.ScaleGridScale);
        Assert.Equal("2", vm.ScaleGridOctaveText);
        Assert.Equal("7", vm.ScaleGridRowsText);
        Assert.Equal("5", vm.ScaleGridColumnsText);
        Assert.False(vm.ScaleGridSendCc);
        Assert.True(vm.ZoneDetailsEnabled);
    }

    [Fact]
    public void LoadSelectedZoneIntoEditorNoSelectionClearsEditor()
    {
        var parser = new ConfigValueParser();
        var sut = new ZoneEditorService(parser, new PageNavigationService());
        AppConfig config = AppConfig.Default;
        var vm = new TestZoneEditorState { ZoneMidiChannel = 5, ZoneDetailsEnabled = true };

        sut.LoadSelectedZoneIntoEditor(vm, config, -1);

        Assert.False(vm.ZoneDetailsEnabled);
        Assert.Equal(1, vm.ZoneMidiChannel);
        Assert.Equal("0", vm.ScaleGridRootText);
        Assert.Equal(ScaleBreathScaleNames.Major, vm.ScaleGridScale);
        Assert.Equal("4", vm.ScaleGridOctaveText);
        Assert.Equal("4", vm.ScaleGridRowsText);
        Assert.Equal("4", vm.ScaleGridColumnsText);
        Assert.True(vm.ScaleGridSendCc);
    }

    private sealed class TestZoneEditorState : IZoneEditorState
    {
        public System.Collections.ObjectModel.ObservableCollection<int> MidiChannels { get; } = [];

        public System.Collections.ObjectModel.ObservableCollection<string> ReleaseBehaviorOptions { get; } = [];

        public System.Collections.ObjectModel.ObservableCollection<string> ZoneOutputModeOptions { get; } = [];

        public System.Collections.ObjectModel.ObservableCollection<string> ScaleBreathScaleOptions { get; } = [];

        public System.Collections.ObjectModel.ObservableCollection<ZoneListItemViewModel> Zones { get; } = [];

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

        public string ZoneOutputMode { get; set; } = ZoneOutputModes.CcPad;

        public string ScaleBreathRootText { get; set; } = "0";

        public string ScaleBreathScale { get; set; } = ScaleBreathScaleNames.Major;

        public string ScaleBreathOctaveText { get; set; } = "4";

        public string ScaleBreathHoleCountText { get; set; } = "10";

        public string ScaleBreathRowSplitText { get; set; } = "0.5";

        public bool ScaleBreathSendCc { get; set; } = true;

        public string ScaleGridRootText { get; set; } = "0";

        public string ScaleGridScale { get; set; } = ScaleBreathScaleNames.Major;

        public string ScaleGridOctaveText { get; set; } = "4";

        public string ScaleGridRowsText { get; set; } = "4";

        public string ScaleGridColumnsText { get; set; } = "4";

        public bool ScaleGridSendCc { get; set; } = true;

        public bool IsCcPadMode => string.Equals(ZoneOutputMode, ZoneOutputModes.CcPad, StringComparison.Ordinal);

        public bool IsScaleBreathMode => string.Equals(ZoneOutputMode, ZoneOutputModes.ScaleBreath, StringComparison.Ordinal);

        public bool IsScaleGridMode => string.Equals(ZoneOutputMode, ZoneOutputModes.ScaleGrid, StringComparison.Ordinal);
    }
}
