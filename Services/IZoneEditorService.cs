using System.Collections.ObjectModel;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services;

/// <summary>
/// Projection interface for a zone editor's UI state.
/// </summary>
internal interface IZoneEditorState
{
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
    bool ZoneDetailsEnabled { get; set; }
    string ZoneSelectionSummaryPrimaryText { get; set; }
    string ZoneSelectionSummarySecondaryText { get; set; }
}

/// <summary>
/// Handles zone editor projection and commit logic.
/// </summary>
internal interface IZoneEditorService
{
    void RefreshZoneList(ObservableCollection<ZoneListItemViewModel> zones, AppConfig config);

    void LoadSelectedZoneIntoEditor(IZoneEditorState state, AppConfig config, int selectedZoneIndex);

    void CommitCurrentZoneEdits(
        AppConfig config,
        IZoneEditorState state,
        int selectedZoneIndex,
        string? maxZoneRowsText,
        string? maxZonesPerRowText);

    void SetZoneSelectionSummary(IZoneEditorState state, ZoneConfig? zone);
}
