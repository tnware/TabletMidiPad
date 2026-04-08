using System.Collections.ObjectModel;

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
    private string _toolbarPressureLabelText = "Pressure (Lin)";
    private string _toolbarInputBackendLabelText = "Input (Auto)";
    private string _selectedPressureCurve = "Linear";
    private string _selectedInputBackendMode = "Auto";
    private string? _pressureCurveLinearIcon;
    private string? _pressureCurveLogarithmicIcon;
    private string? _pressureCurveExponentialIcon;
    private string? _inputBackendAutoIcon;
    private string? _inputBackendWintabIcon;
    private string? _inputBackendMouseIcon;
    private string _zoneSelectionSummaryPrimaryText = "No zone selected";
    private string _zoneSelectionSummarySecondaryText = "Select a zone to edit its routing and bounds.";
    private int _selectedZoneIndex = -1;

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

    public string ToolbarPressureLabelText
    {
        get => _toolbarPressureLabelText;
        set => SetProperty(ref _toolbarPressureLabelText, value);
    }

    public string ToolbarInputBackendLabelText
    {
        get => _toolbarInputBackendLabelText;
        set => SetProperty(ref _toolbarInputBackendLabelText, value);
    }

    public string SelectedPressureCurve
    {
        get => _selectedPressureCurve;
        set => SetProperty(ref _selectedPressureCurve, value);
    }

    public string SelectedInputBackendMode
    {
        get => _selectedInputBackendMode;
        set => SetProperty(ref _selectedInputBackendMode, value);
    }

    public string? PressureCurveLinearIcon
    {
        get => _pressureCurveLinearIcon;
        set => SetProperty(ref _pressureCurveLinearIcon, value);
    }

    public string? PressureCurveLogarithmicIcon
    {
        get => _pressureCurveLogarithmicIcon;
        set => SetProperty(ref _pressureCurveLogarithmicIcon, value);
    }

    public string? PressureCurveExponentialIcon
    {
        get => _pressureCurveExponentialIcon;
        set => SetProperty(ref _pressureCurveExponentialIcon, value);
    }

    public string? InputBackendAutoIcon
    {
        get => _inputBackendAutoIcon;
        set => SetProperty(ref _inputBackendAutoIcon, value);
    }

    public string? InputBackendWintabIcon
    {
        get => _inputBackendWintabIcon;
        set => SetProperty(ref _inputBackendWintabIcon, value);
    }

    public string? InputBackendMouseIcon
    {
        get => _inputBackendMouseIcon;
        set => SetProperty(ref _inputBackendMouseIcon, value);
    }

    public ObservableCollection<ZoneListItemViewModel> Zones { get; } = new();

    public string ZoneSelectionSummaryPrimaryText
    {
        get => _zoneSelectionSummaryPrimaryText;
        set => SetProperty(ref _zoneSelectionSummaryPrimaryText, value);
    }

    public string ZoneSelectionSummarySecondaryText
    {
        get => _zoneSelectionSummarySecondaryText;
        set => SetProperty(ref _zoneSelectionSummarySecondaryText, value);
    }

    public int SelectedZoneIndex
    {
        get => _selectedZoneIndex;
        set => SetProperty(ref _selectedZoneIndex, value);
    }
}
