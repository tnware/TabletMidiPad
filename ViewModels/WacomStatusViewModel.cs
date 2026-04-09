using System.Collections.ObjectModel;
using System.Windows.Input;

namespace TabletMidiPad.ViewModels;

/// <summary>
/// Observable view-model for the Wacom service status tab in the preferences dialog.
/// </summary>
internal sealed class WacomStatusViewModel : ViewModelBase
{
    private string _statusText = "Checking...";
    private bool _isStartEnabled = true;
    private TabletDeviceListItemViewModel? _selectedTabletDevice;
    private bool _isTabletDevicePickerEnabled = true;

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsStartEnabled
    {
        get => _isStartEnabled;
        set => SetProperty(ref _isStartEnabled, value);
    }

    public ObservableCollection<TabletDeviceListItemViewModel> TabletDevices { get; init; } = [];

    public TabletDeviceListItemViewModel? SelectedTabletDevice
    {
        get => _selectedTabletDevice;
        set
        {
            if (SetProperty(ref _selectedTabletDevice, value))
            {
                OnSelectedTabletDeviceChanged?.Invoke(value);
            }
        }
    }

    public bool IsTabletDevicePickerEnabled
    {
        get => _isTabletDevicePickerEnabled;
        set => SetProperty(ref _isTabletDevicePickerEnabled, value);
    }

    public Action<TabletDeviceListItemViewModel?>? OnSelectedTabletDeviceChanged { get; init; }

    public required ICommand RefreshCommand { get; init; }

    public required ICommand ReconnectPenCommand { get; init; }

    public required ICommand StartServiceCommand { get; init; }
}
