using System.Collections.ObjectModel;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services;

/// <summary>
/// Handles zone editor projection and commit logic.
/// </summary>
internal interface IZoneEditorService
{
    void RefreshZoneList(ObservableCollection<ZoneListItemViewModel> zones, AppConfig config);

    void LoadSelectedZoneIntoEditor(MainViewModel viewModel, AppConfig config, int selectedZoneIndex);

    void CommitCurrentZoneEdits(
        AppConfig config,
        MainViewModel viewModel,
        int selectedZoneIndex,
        string? maxZoneRowsText,
        string? maxZonesPerRowText);

    void SetZoneSelectionSummary(MainViewModel viewModel, ZoneConfig? zone);
}
