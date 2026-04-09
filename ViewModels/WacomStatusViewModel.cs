using System.Windows.Input;

namespace TabletMidiPad.ViewModels;

/// <summary>
/// Observable view-model for the Wacom service status tab in the preferences dialog.
/// </summary>
internal sealed class WacomStatusViewModel : ViewModelBase
{
    private string _statusText = "Checking...";
    private bool _isStartEnabled = true;

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

    public required ICommand RefreshCommand { get; init; }

    public required ICommand StartServiceCommand { get; init; }
}
