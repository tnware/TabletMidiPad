using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using TabletMidiPad;

namespace TabletMidiPad.ViewModels;

/// <summary>
/// View-model surface for MainWindow status and telemetry text.
/// </summary>
internal sealed class MainViewModel : ViewModelBase, Services.IZoneEditorState
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

    public ZoneListViewModel ZoneList { get; }
    public ZoneEditorViewModel ZoneEditor { get; }

    private readonly Services.ISettingsService _settingsService;
    private readonly Services.IMainWindowToolbarSession _toolbarSession;
    private readonly Services.IMainWindowPadInputSession _padInputSession;

    public MainViewModel(
        ZoneListViewModel zoneList,
        ZoneEditorViewModel zoneEditor,
        Services.ISettingsService settingsService,
        Services.IMainWindowSettingsSession settingsSession,
        Services.IMainWindowToolbarSession toolbarSession,
        Services.IMainWindowPadInputSession padInputSession)
    {
        ZoneList = zoneList ?? throw new ArgumentNullException(nameof(zoneList));
        ZoneEditor = zoneEditor ?? throw new ArgumentNullException(nameof(zoneEditor));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _toolbarSession = toolbarSession ?? throw new ArgumentNullException(nameof(toolbarSession));
        _padInputSession = padInputSession ?? throw new ArgumentNullException(nameof(padInputSession));

        SaveSettingsCommand = new DelegateCommand(settingsSession.ApplySettings);
        ResetDefaultsCommand = new DelegateCommand(settingsSession.ResetToDefaultsAndApply);
        OpenPreferencesCommand = new DelegateCommand(settingsSession.RunPreferencesFlow);
        ExitCommand = new DelegateCommand(() => System.Windows.Application.Current.MainWindow?.Close());
        RefreshWacomStatusCommand = new DelegateCommand(() => { }); // Action provided via property or event later
        ReconnectPenCommand = new DelegateCommand(() => _padInputSession.RebindInputBackend());
        StartWacomServiceCommand = new DelegateCommand(() => { });
        AboutCommand = new DelegateCommand(() =>
        {
             System.Windows.MessageBox.Show(
                "Tablet MIDI Pad\n\nTurns a Wacom tablet into a MIDI XY controller with configurable zones.",
                "About Tablet MIDI Pad",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        });
        AddZoneCommand = ZoneList.AddZoneCommand;
        RemoveZoneCommand = ZoneList.RemoveZoneCommand;
        AutoLayoutCommand = ZoneList.AutoLayoutCommand;

        SelectPressureCurveLinearCommand = new DelegateCommand(() => SetPressureCurve("Linear"));
        SelectPressureCurveLogarithmicCommand = new DelegateCommand(() => SetPressureCurve("Logarithmic"));
        SelectPressureCurveExponentialCommand = new DelegateCommand(() => SetPressureCurve("Exponential"));
        SelectInputBackendAutoCommand = new DelegateCommand(() => SetInputBackendMode("Auto"));
        SelectInputBackendWintabCommand = new DelegateCommand(() => SetInputBackendMode("Wintab Only"));
        SelectInputBackendMouseCommand = new DelegateCommand(() => SetInputBackendMode("Mouse"));
        CycleInputBackendCommand = new DelegateCommand(CycleInputBackend);

        _settingsService.SettingsUpdated += HandleSettingsUpdated;
        HandleSettingsUpdated(null, EventArgs.Empty);

        for (int ch = 1; ch <= 16; ch++)
        {
            MidiChannels.Add(ch);
        }

        ReleaseBehaviorOptions.Add(ZoneReleaseBehaviors.NoSendOnRelease);
        ReleaseBehaviorOptions.Add(ZoneReleaseBehaviors.Hold);
        ReleaseBehaviorOptions.Add(ZoneReleaseBehaviors.ResetPressureOnly);
        ReleaseBehaviorOptions.Add(ZoneReleaseBehaviors.ResetAll);
    }

    private void SetPressureCurve(string curve)
    {
        _toolbarSession.ApplyPressureCurve(_settingsService.Config, curve);
        _settingsService.Save();
    }

    private void SetInputBackendMode(string mode)
    {
        var result = _toolbarSession.TryApplyInputBackendMode(_settingsService.Config, mode);
        if (result.ModeChanged)
        {
            if (result.RequiresInputRebind)
            {
                _padInputSession.RebindInputBackend();
            }
            _settingsService.Save();
        }
    }

    private void CycleInputBackend()
    {
        SetInputBackendMode(_toolbarSession.GetNextCycledInputBackendModeLabel(_settingsService.Config));
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
        // Deprecated, logic moved to constructor
    }

    public void ConfigureToolbarCommands(
        Action selectPressureCurveLinear,
        Action selectPressureCurveLogarithmic,
        Action selectPressureCurveExponential,
        Action selectInputBackendAuto,
        Action selectInputBackendWintab,
        Action selectInputBackendMouse,
        Action cycleInputBackend)
    {
        // Deprecated, logic moved to constructor
    }

    private void HandleSettingsUpdated(object? sender, EventArgs e)
    {
        var config = _settingsService.Config;
        SendPressureGlobally = config.SendPressureGlobally;
        AllowZoneSwitchWhilePressed = config.AllowZoneSwitchWhilePressed;
        MaxZoneRowsText = config.MaxZoneRows.ToString();
        MaxZonesPerRowText = config.MaxZonesPerRow.ToString();
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

    #region Redundant properties being removed/moved

    string Services.IZoneEditorState.ZoneName { get => ZoneEditor.ZoneName; set => ZoneEditor.ZoneName = value; }
    bool Services.IZoneEditorState.ZoneEnabled { get => ZoneEditor.ZoneEnabled; set => ZoneEditor.ZoneEnabled = value; }
    int Services.IZoneEditorState.ZoneMidiChannel { get => ZoneEditor.ZoneMidiChannel; set => ZoneEditor.ZoneMidiChannel = value; }
    string Services.IZoneEditorState.ZoneCcX { get => ZoneEditor.ZoneCcX; set => ZoneEditor.ZoneCcX = value; }
    string Services.IZoneEditorState.ZoneCcY { get => ZoneEditor.ZoneCcY; set => ZoneEditor.ZoneCcY = value; }
    string Services.IZoneEditorState.ZoneCcPressure { get => ZoneEditor.ZoneCcPressure; set => ZoneEditor.ZoneCcPressure = value; }
    string Services.IZoneEditorState.ZoneLeft { get => ZoneEditor.ZoneLeft; set => ZoneEditor.ZoneLeft = value; }
    string Services.IZoneEditorState.ZoneTop { get => ZoneEditor.ZoneTop; set => ZoneEditor.ZoneTop = value; }
    string Services.IZoneEditorState.ZoneWidth { get => ZoneEditor.ZoneWidth; set => ZoneEditor.ZoneWidth = value; }
    string Services.IZoneEditorState.ZoneHeight { get => ZoneEditor.ZoneHeight; set => ZoneEditor.ZoneHeight = value; }
    bool Services.IZoneEditorState.ZoneSendX { get => ZoneEditor.ZoneSendX; set => ZoneEditor.ZoneSendX = value; }
    bool Services.IZoneEditorState.ZoneSendY { get => ZoneEditor.ZoneSendY; set => ZoneEditor.ZoneSendY = value; }
    bool Services.IZoneEditorState.ZoneInvertX { get => ZoneEditor.ZoneInvertX; set => ZoneEditor.ZoneInvertX = value; }
    bool Services.IZoneEditorState.ZoneInvertY { get => ZoneEditor.ZoneInvertY; set => ZoneEditor.ZoneInvertY = value; }
    bool Services.IZoneEditorState.ZoneSendPressure { get => ZoneEditor.ZoneSendPressure; set => ZoneEditor.ZoneSendPressure = value; }
    string Services.IZoneEditorState.ZoneReleaseBehavior { get => ZoneEditor.ZoneReleaseBehavior; set => ZoneEditor.ZoneReleaseBehavior = value; }
    bool Services.IZoneEditorState.ZoneDetailsEnabled { get => ZoneEditor.ZoneDetailsEnabled; set => ZoneEditor.ZoneDetailsEnabled = value; }
    string Services.IZoneEditorState.ZoneSelectionSummaryPrimaryText { get => ZoneEditor.ZoneSelectionSummaryPrimaryText; set => ZoneEditor.ZoneSelectionSummaryPrimaryText = value; }
    string Services.IZoneEditorState.ZoneSelectionSummarySecondaryText { get => ZoneEditor.ZoneSelectionSummarySecondaryText; set => ZoneEditor.ZoneSelectionSummarySecondaryText = value; }

    public int SelectedZoneIndex
    {
        get => ZoneList.SelectedZoneIndex;
        set => ZoneList.SelectedZoneIndex = value;
    }

    #endregion

    /// <summary>
    /// Raises <see cref="SelectedZoneIndex"/> after the zone list collection is replaced so the ListBox binding re-applies.
    /// </summary>
    internal void NotifySelectedZoneIndexChanged()
    {
        OnPropertyChanged(nameof(SelectedZoneIndex));
    }
}
