using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using TabletMidiPad;

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
    private bool _isPressureCurveLinearChecked = true;
    private bool _isPressureCurveLogarithmicChecked;
    private bool _isPressureCurveExponentialChecked;
    private bool _isInputBackendAutoChecked = true;
    private bool _isInputBackendWintabChecked;
    private bool _isInputBackendMouseChecked;
    private string _zoneSelectionSummaryPrimaryText = "No zone selected";
    private string _zoneSelectionSummarySecondaryText = "Select a zone to edit its routing and bounds.";
    private int _selectedZoneIndex = -1;
    private bool _zoneDetailsEnabled;
    private string _zoneName = string.Empty;
    private bool _zoneEnabled;
    private int _zoneMidiChannel = 1;
    private string _zoneCcX = string.Empty;
    private string _zoneCcY = string.Empty;
    private string _zoneCcPressure = string.Empty;
    private string _zoneLeft = string.Empty;
    private string _zoneTop = string.Empty;
    private string _zoneWidth = string.Empty;
    private string _zoneHeight = string.Empty;
    private bool _zoneSendX = true;
    private bool _zoneSendY = true;
    private bool _zoneInvertX;
    private bool _zoneInvertY;
    private bool _zoneSendPressure = true;
    private string _zoneReleaseBehavior = string.Empty;
    private string _settingsResultMessage = string.Empty;
    private SettingsResultKind _settingsResultKind = SettingsResultKind.None;
    private string _maxZoneRowsText = string.Empty;
    private string _maxZonesPerRowText = string.Empty;
    private bool _isMidiStatusPanelVisible;
    private bool _isMidiProgressVisible = true;
    private MidiStatusSeverity _midiStatusSeverity = MidiStatusSeverity.Normal;
    private bool _sendPressureGlobally = true;
    private bool _allowZoneSwitchWhilePressed = true;
    private string _wacomServiceStatusHeader = "Service: checking...";
    private bool _isWacomServiceStartEnabled = true;
    private TabletDeviceListItemViewModel? _selectedTabletDevice;
    private bool _isTabletDevicePickerEnabled = true;

    public MainViewModel()
    {
        SaveSettingsCommand = new DelegateCommand(() => { });
        ResetDefaultsCommand = new DelegateCommand(() => { });
        OpenPreferencesCommand = new DelegateCommand(() => { });
        ExitCommand = new DelegateCommand(() => { });
        RefreshWacomStatusCommand = new DelegateCommand(() => { });
        ReconnectPenCommand = new DelegateCommand(() => { });
        StartWacomServiceCommand = new DelegateCommand(() => { });
        AboutCommand = new DelegateCommand(() => { });
        AddZoneCommand = new DelegateCommand(() => { });
        RemoveZoneCommand = new DelegateCommand(() => { });
        AutoLayoutCommand = new DelegateCommand(() => { });
        SelectPressureCurveLinearCommand = new DelegateCommand(() => { });
        SelectPressureCurveLogarithmicCommand = new DelegateCommand(() => { });
        SelectPressureCurveExponentialCommand = new DelegateCommand(() => { });
        SelectInputBackendAutoCommand = new DelegateCommand(() => { });
        SelectInputBackendWintabCommand = new DelegateCommand(() => { });
        SelectInputBackendMouseCommand = new DelegateCommand(() => { });
        CycleInputBackendCommand = new DelegateCommand(() => { });

        for (int ch = 1; ch <= 16; ch++)
        {
            MidiChannels.Add(ch);
        }

        ReleaseBehaviorOptions.Add(ZoneReleaseBehaviors.NoSendOnRelease);
        ReleaseBehaviorOptions.Add(ZoneReleaseBehaviors.Hold);
        ReleaseBehaviorOptions.Add(ZoneReleaseBehaviors.ResetPressureOnly);
        ReleaseBehaviorOptions.Add(ZoneReleaseBehaviors.ResetAll);
    }

    public ObservableCollection<int> MidiChannels { get; } = new();

    public ObservableCollection<string> ReleaseBehaviorOptions { get; } = new();

    public ObservableCollection<TabletDeviceListItemViewModel> TabletDevices { get; } = new();

    public string SettingsResultMessage
    {
        get => _settingsResultMessage;
        set => SetProperty(ref _settingsResultMessage, value);
    }

    public SettingsResultKind SettingsResultKind
    {
        get => _settingsResultKind;
        set => SetProperty(ref _settingsResultKind, value);
    }

    public string MaxZoneRowsText
    {
        get => _maxZoneRowsText;
        set => SetProperty(ref _maxZoneRowsText, value);
    }

    public string MaxZonesPerRowText
    {
        get => _maxZonesPerRowText;
        set => SetProperty(ref _maxZonesPerRowText, value);
    }

    public bool IsMidiStatusPanelVisible
    {
        get => _isMidiStatusPanelVisible;
        set => SetProperty(ref _isMidiStatusPanelVisible, value);
    }

    public bool IsMidiProgressVisible
    {
        get => _isMidiProgressVisible;
        set => SetProperty(ref _isMidiProgressVisible, value);
    }

    public MidiStatusSeverity MidiStatusSeverity
    {
        get => _midiStatusSeverity;
        set => SetProperty(ref _midiStatusSeverity, value);
    }

    public bool SendPressureGlobally
    {
        get => _sendPressureGlobally;
        set => SetProperty(ref _sendPressureGlobally, value);
    }

    public bool AllowZoneSwitchWhilePressed
    {
        get => _allowZoneSwitchWhilePressed;
        set => SetProperty(ref _allowZoneSwitchWhilePressed, value);
    }

    public string WacomServiceStatusHeader
    {
        get => _wacomServiceStatusHeader;
        set => SetProperty(ref _wacomServiceStatusHeader, value);
    }

    public bool IsWacomServiceStartEnabled
    {
        get => _isWacomServiceStartEnabled;
        set => SetProperty(ref _isWacomServiceStartEnabled, value);
    }

    public TabletDeviceListItemViewModel? SelectedTabletDevice
    {
        get => _selectedTabletDevice;
        set => SetProperty(ref _selectedTabletDevice, value);
    }

    public bool IsTabletDevicePickerEnabled
    {
        get => _isTabletDevicePickerEnabled;
        set => SetProperty(ref _isTabletDevicePickerEnabled, value);
    }

    public ICommand SaveSettingsCommand { get; private set; }
    public ICommand ResetDefaultsCommand { get; private set; }
    public ICommand OpenPreferencesCommand { get; private set; }
    public ICommand ExitCommand { get; private set; }
    public ICommand RefreshWacomStatusCommand { get; private set; }
    public ICommand ReconnectPenCommand { get; private set; }
    public ICommand StartWacomServiceCommand { get; private set; }
    public ICommand AboutCommand { get; private set; }
    public ICommand AddZoneCommand { get; private set; }
    public ICommand RemoveZoneCommand { get; private set; }
    public ICommand AutoLayoutCommand { get; private set; }

    public ICommand SelectPressureCurveLinearCommand { get; private set; }

    public ICommand SelectPressureCurveLogarithmicCommand { get; private set; }

    public ICommand SelectPressureCurveExponentialCommand { get; private set; }

    public ICommand SelectInputBackendAutoCommand { get; private set; }

    public ICommand SelectInputBackendWintabCommand { get; private set; }

    public ICommand SelectInputBackendMouseCommand { get; private set; }

    public ICommand CycleInputBackendCommand { get; private set; }

    public void ConfigureCommands(
        Action saveSettings,
        Action resetDefaults,
        Action openPreferences,
        Action exit,
        Action refreshWacomStatus,
        Action reconnectPen,
        Action startWacomService,
        Action showAbout,
        Action addZone,
        Action removeZone,
        Action autoLayout)
    {
        SaveSettingsCommand = new DelegateCommand(saveSettings);
        ResetDefaultsCommand = new DelegateCommand(resetDefaults);
        OpenPreferencesCommand = new DelegateCommand(openPreferences);
        ExitCommand = new DelegateCommand(exit);
        RefreshWacomStatusCommand = new DelegateCommand(refreshWacomStatus);
        ReconnectPenCommand = new DelegateCommand(reconnectPen);
        StartWacomServiceCommand = new DelegateCommand(startWacomService);
        AboutCommand = new DelegateCommand(showAbout);
        AddZoneCommand = new DelegateCommand(addZone);
        RemoveZoneCommand = new DelegateCommand(removeZone);
        AutoLayoutCommand = new DelegateCommand(autoLayout);
        OnPropertyChanged(nameof(SaveSettingsCommand));
        OnPropertyChanged(nameof(ResetDefaultsCommand));
        OnPropertyChanged(nameof(OpenPreferencesCommand));
        OnPropertyChanged(nameof(ExitCommand));
        OnPropertyChanged(nameof(RefreshWacomStatusCommand));
        OnPropertyChanged(nameof(ReconnectPenCommand));
        OnPropertyChanged(nameof(StartWacomServiceCommand));
        OnPropertyChanged(nameof(AboutCommand));
        OnPropertyChanged(nameof(AddZoneCommand));
        OnPropertyChanged(nameof(RemoveZoneCommand));
        OnPropertyChanged(nameof(AutoLayoutCommand));
    }

    /// <summary>
    /// Wires toolbar pressure-curve and input-backend commands (invoked from the view after construction).
    /// </summary>
    public void ConfigureToolbarCommands(
        Action selectPressureCurveLinear,
        Action selectPressureCurveLogarithmic,
        Action selectPressureCurveExponential,
        Action selectInputBackendAuto,
        Action selectInputBackendWintab,
        Action selectInputBackendMouse,
        Action cycleInputBackend)
    {
        SelectPressureCurveLinearCommand = new DelegateCommand(selectPressureCurveLinear);
        SelectPressureCurveLogarithmicCommand = new DelegateCommand(selectPressureCurveLogarithmic);
        SelectPressureCurveExponentialCommand = new DelegateCommand(selectPressureCurveExponential);
        SelectInputBackendAutoCommand = new DelegateCommand(selectInputBackendAuto);
        SelectInputBackendWintabCommand = new DelegateCommand(selectInputBackendWintab);
        SelectInputBackendMouseCommand = new DelegateCommand(selectInputBackendMouse);
        CycleInputBackendCommand = new DelegateCommand(cycleInputBackend);
        OnPropertyChanged(nameof(SelectPressureCurveLinearCommand));
        OnPropertyChanged(nameof(SelectPressureCurveLogarithmicCommand));
        OnPropertyChanged(nameof(SelectPressureCurveExponentialCommand));
        OnPropertyChanged(nameof(SelectInputBackendAutoCommand));
        OnPropertyChanged(nameof(SelectInputBackendWintabCommand));
        OnPropertyChanged(nameof(SelectInputBackendMouseCommand));
        OnPropertyChanged(nameof(CycleInputBackendCommand));
    }

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

    public bool IsPressureCurveLinearChecked
    {
        get => _isPressureCurveLinearChecked;
        set => SetProperty(ref _isPressureCurveLinearChecked, value);
    }

    public bool IsPressureCurveLogarithmicChecked
    {
        get => _isPressureCurveLogarithmicChecked;
        set => SetProperty(ref _isPressureCurveLogarithmicChecked, value);
    }

    public bool IsPressureCurveExponentialChecked
    {
        get => _isPressureCurveExponentialChecked;
        set => SetProperty(ref _isPressureCurveExponentialChecked, value);
    }

    public bool IsInputBackendAutoChecked
    {
        get => _isInputBackendAutoChecked;
        set => SetProperty(ref _isInputBackendAutoChecked, value);
    }

    public bool IsInputBackendWintabChecked
    {
        get => _isInputBackendWintabChecked;
        set => SetProperty(ref _isInputBackendWintabChecked, value);
    }

    public bool IsInputBackendMouseChecked
    {
        get => _isInputBackendMouseChecked;
        set => SetProperty(ref _isInputBackendMouseChecked, value);
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

    /// <summary>
    /// Raises <see cref="SelectedZoneIndex"/> after the zone list collection is replaced so the ListBox binding re-applies.
    /// </summary>
    internal void NotifySelectedZoneIndexChanged()
    {
        OnPropertyChanged(nameof(SelectedZoneIndex));
    }

    public bool ZoneDetailsEnabled
    {
        get => _zoneDetailsEnabled;
        set => SetProperty(ref _zoneDetailsEnabled, value);
    }

    public string ZoneName
    {
        get => _zoneName;
        set => SetProperty(ref _zoneName, value);
    }

    public bool ZoneEnabled
    {
        get => _zoneEnabled;
        set => SetProperty(ref _zoneEnabled, value);
    }

    public int ZoneMidiChannel
    {
        get => _zoneMidiChannel;
        set => SetProperty(ref _zoneMidiChannel, value);
    }

    public string ZoneCcX
    {
        get => _zoneCcX;
        set => SetProperty(ref _zoneCcX, value);
    }

    public string ZoneCcY
    {
        get => _zoneCcY;
        set => SetProperty(ref _zoneCcY, value);
    }

    public string ZoneCcPressure
    {
        get => _zoneCcPressure;
        set => SetProperty(ref _zoneCcPressure, value);
    }

    public string ZoneLeft
    {
        get => _zoneLeft;
        set => SetProperty(ref _zoneLeft, value);
    }

    public string ZoneTop
    {
        get => _zoneTop;
        set => SetProperty(ref _zoneTop, value);
    }

    public string ZoneWidth
    {
        get => _zoneWidth;
        set => SetProperty(ref _zoneWidth, value);
    }

    public string ZoneHeight
    {
        get => _zoneHeight;
        set => SetProperty(ref _zoneHeight, value);
    }

    public bool ZoneSendX
    {
        get => _zoneSendX;
        set => SetProperty(ref _zoneSendX, value);
    }

    public bool ZoneSendY
    {
        get => _zoneSendY;
        set => SetProperty(ref _zoneSendY, value);
    }

    public bool ZoneInvertX
    {
        get => _zoneInvertX;
        set => SetProperty(ref _zoneInvertX, value);
    }

    public bool ZoneInvertY
    {
        get => _zoneInvertY;
        set => SetProperty(ref _zoneInvertY, value);
    }

    public bool ZoneSendPressure
    {
        get => _zoneSendPressure;
        set => SetProperty(ref _zoneSendPressure, value);
    }

    public string ZoneReleaseBehavior
    {
        get => _zoneReleaseBehavior;
        set => SetProperty(ref _zoneReleaseBehavior, value);
    }
}
