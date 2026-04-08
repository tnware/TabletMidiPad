using System;
using System.Collections.Generic;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services;

/// <summary>
/// Default implementation of <see cref="ITabletDevicePickerSyncService"/>.
/// </summary>
internal sealed class TabletDevicePickerSyncService : ITabletDevicePickerSyncService
{
    private readonly IWintabInputBackend _wintabInputBackend;

    public TabletDevicePickerSyncService(IWintabInputBackend wintabInputBackend)
    {
        _wintabInputBackend = wintabInputBackend ?? throw new ArgumentNullException(nameof(wintabInputBackend));
    }

    /// <inheritdoc />
    public void Refresh(MainViewModel viewModel, int previouslySelectedDeviceIndex)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        viewModel.TabletDevices.Clear();
        IReadOnlyList<WintabTabletDevice> devices = _wintabInputBackend.GetAvailableDevices();
        if (devices.Count <= 0)
        {
            var placeholder = new TabletDeviceListItemViewModel(-1, "No tablets");
            viewModel.TabletDevices.Add(placeholder);
            viewModel.SelectedTabletDevice = placeholder;
            viewModel.IsTabletDevicePickerEnabled = false;
            return;
        }

        viewModel.IsTabletDevicePickerEnabled = true;
        int selectIndex = 0;
        for (int i = 0; i < devices.Count; i++)
        {
            WintabTabletDevice device = devices[i];
            viewModel.TabletDevices.Add(new TabletDeviceListItemViewModel(device.DeviceIndex, device.Label));
            if (previouslySelectedDeviceIndex >= 0 && device.DeviceIndex == previouslySelectedDeviceIndex)
            {
                selectIndex = i;
            }
        }

        viewModel.SelectedTabletDevice = viewModel.TabletDevices[selectIndex];
    }
}
