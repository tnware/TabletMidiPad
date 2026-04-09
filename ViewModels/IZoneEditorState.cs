using System.Collections.ObjectModel;

namespace TabletMidiPad.ViewModels;

internal interface IZoneEditorState
{
    ObservableCollection<int> MidiChannels { get; }

    ObservableCollection<string> ReleaseBehaviorOptions { get; }

    ObservableCollection<string> ZoneOutputModeOptions { get; }

    ObservableCollection<string> ScaleBreathScaleOptions { get; }

    ObservableCollection<ZoneListItemViewModel> Zones { get; }

    string MaxZoneRowsText { get; set; }

    string MaxZonesPerRowText { get; set; }

    string ZoneSelectionSummaryPrimaryText { get; set; }

    string ZoneSelectionSummarySecondaryText { get; set; }

    bool ZoneDetailsEnabled { get; set; }

    string ZoneName { get; set; }

    bool ZoneEnabled { get; set; }

    int ZoneMidiChannel { get; set; }

    string ZoneCcX { get; set; }

    string ZoneCcY { get; set; }

    string ZoneCcPressure { get; set; }

    string ZoneLeft { get; set; }

    string ZoneTop { get; set; }

    string ZoneWidth { get; set; }

    string ZoneHeight { get; set; }

    bool ZoneSendX { get; set; }

    bool ZoneSendY { get; set; }

    bool ZoneInvertX { get; set; }

    bool ZoneInvertY { get; set; }

    bool ZoneSendPressure { get; set; }

    string ZoneReleaseBehavior { get; set; }

    string ZoneOutputMode { get; set; }

    string ScaleBreathRootText { get; set; }

    string ScaleBreathScale { get; set; }

    string ScaleBreathOctaveText { get; set; }

    string ScaleBreathHoleCountText { get; set; }

    string ScaleBreathRowSplitText { get; set; }

    bool ScaleBreathSendCc { get; set; }

    string ScaleGridRootText { get; set; }

    string ScaleGridScale { get; set; }

    string ScaleGridOctaveText { get; set; }

    string ScaleGridRowsText { get; set; }

    string ScaleGridColumnsText { get; set; }

    bool ScaleGridSendCc { get; set; }

    bool IsCcPadMode { get; }

    bool IsScaleBreathMode { get; }

    bool IsScaleGridMode { get; }
}
