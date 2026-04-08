namespace TabletMidiPad.ViewModels;

/// <summary>
/// View-model surface for MainWindow status and telemetry text.
/// </summary>
internal sealed class MainViewModel : ViewModelBase
{
    private string _statusText = "Waiting for MIDI port...";
    private string _routingText = string.Empty;
    private string _activeZoneText = "Active zone: none";
    private string _valueText = "X: -, Y: -, Pressure: -";
    private string _penButtonsText = "Pen buttons: -";
    private string _modifiersText = "Modifiers: none";
    private string _inputBackendText = string.Empty;
    private string _configSummaryText = string.Empty;
    private string _midiStatusText = "Connecting MIDI...";

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string RoutingText
    {
        get => _routingText;
        set => SetProperty(ref _routingText, value);
    }

    public string ActiveZoneText
    {
        get => _activeZoneText;
        set => SetProperty(ref _activeZoneText, value);
    }

    public string ValueText
    {
        get => _valueText;
        set => SetProperty(ref _valueText, value);
    }

    public string PenButtonsText
    {
        get => _penButtonsText;
        set => SetProperty(ref _penButtonsText, value);
    }

    public string ModifiersText
    {
        get => _modifiersText;
        set => SetProperty(ref _modifiersText, value);
    }

    public string InputBackendText
    {
        get => _inputBackendText;
        set => SetProperty(ref _inputBackendText, value);
    }

    public string ConfigSummaryText
    {
        get => _configSummaryText;
        set => SetProperty(ref _configSummaryText, value);
    }

    public string MidiStatusText
    {
        get => _midiStatusText;
        set => SetProperty(ref _midiStatusText, value);
    }
}
