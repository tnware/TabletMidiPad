using System.Collections.ObjectModel;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services.Settings;

/// <summary>
/// Opens the preferences dialog and returns edited values when accepted.
/// </summary>
internal interface IPreferencesDialogService
{
    /// <summary>
    /// Shows the preferences dialog owned by the given window.
    /// </summary>
    /// <param name="owner">The owning window.</param>
    /// <param name="config">The current application configuration.</param>
    /// <param name="expressKeyCount">The detected express-key count.</param>
    /// <param name="tabletDevices">Current tablet device picker items.</param>
    /// <param name="selectedTabletDevice">Current tablet device selection.</param>
    /// <param name="isTabletDevicePickerEnabled">Whether tablet device picker should be enabled.</param>
    /// <param name="reconnectPen">Action that rebinds tablet input.</param>
    /// <param name="setSelectedTabletDevice">Callback invoked when the selected device changes in the dialog.</param>
    /// <returns>
    /// The edited preferences payload when accepted; otherwise <see langword="null" />.
    /// </returns>
    PreferencesDialogResult? ShowDialog(
        System.Windows.Window owner,
        AppConfig config,
        int expressKeyCount,
        ObservableCollection<TabletDeviceListItemViewModel> tabletDevices,
        TabletDeviceListItemViewModel? selectedTabletDevice,
        bool isTabletDevicePickerEnabled,
        Action reconnectPen,
        Action<TabletDeviceListItemViewModel?> setSelectedTabletDevice);
}
