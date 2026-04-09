using System.Collections.ObjectModel;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services;

/// <summary>
/// Handles zone editor projection and commit logic.
/// </summary>
internal interface IZoneEditorService
{
    void RefreshZoneList(ObservableCollection<ZoneListItemViewModel> zones, AppConfig config);

    void LoadSelectedZoneIntoEditor(IZoneEditorState viewModel, AppConfig config, int selectedZoneIndex);

    void CommitCurrentZoneEdits(
        AppConfig config,
        IZoneEditorState viewModel,
        int selectedZoneIndex,
        string? maxZoneRowsText,
        string? maxZonesPerRowText);

    void SetZoneSelectionSummary(IZoneEditorState viewModel, ZoneConfig? zone);
}
