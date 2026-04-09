using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services.Input;

/// <summary>
/// Projects Wintab-reported tablet devices into the main window view model for the device combo box.
/// </summary>
internal interface ITabletDevicePickerSyncService
{
    /// <summary>
    /// Rebuilds <see cref="MainViewModel.TabletDevices"/> from <see cref="IWintabInputBackend.GetAvailableDevices"/>
    /// and updates <see cref="MainViewModel.SelectedTabletDevice"/> / <see cref="MainViewModel.IsTabletDevicePickerEnabled"/>.
    /// </summary>
    /// <param name="viewModel">Main window view model.</param>
    /// <param name="previouslySelectedDeviceIndex">Wintab device index to re-select when still present, or -1 for first row.</param>
    void Refresh(MainViewModel viewModel, int previouslySelectedDeviceIndex);
}
