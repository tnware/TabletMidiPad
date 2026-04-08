namespace TabletMidiPad.ViewModels;

/// <summary>
/// One row in the tablet device picker (bound to <see cref="MainViewModel.TabletDevices"/>).
/// </summary>
internal sealed class TabletDeviceListItemViewModel
{
    public TabletDeviceListItemViewModel(int deviceIndex, string label)
    {
        DeviceIndex = deviceIndex;
        Label = label;
    }

    public int DeviceIndex { get; }

    public string Label { get; }

    public override string ToString() => Label;
}
