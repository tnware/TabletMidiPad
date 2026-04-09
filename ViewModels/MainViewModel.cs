using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using TabletMidiPad;

namespace TabletMidiPad.ViewModels;

/// <summary>
/// Root shell view-model for <see cref="Views.MainWindow"/>.
/// </summary>
internal sealed class MainViewModel : ViewModelBase, IZoneEditorState
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
    private string _settingsResultMessage = string.Empty;
    private SettingsResultKind _settingsResultKind = SettingsResultKind.None;
    private bool _isMidiStatusPanelVisible;
    private bool _isMidiProgressVisible = true;
    private MidiStatusSeverity _midiStatusSeverity = MidiStatusSeverity.Normal;
    private string _wacomServiceStatusHeader = "Service: checking...";
    private bool _isWacomServiceStartEnabled = true;
    private TabletDeviceListItemViewModel? _selectedTabletDevice;
    private bool _isTabletDevicePickerEnabled = true;

    public MainViewModel()
        : this(
            new ZoneListViewModel(
                new DesignRuntimeConfigService(),
                new DesignZoneActionWorkflowCoordinator(),
                new ZoneEditorViewModel(new DesignRuntimeConfigService(), new DesignZoneEditorService())),
            new ZoneEditorViewModel(new DesignRuntimeConfigService(), new DesignZoneEditorService()),
            new ToolbarViewModel(new DesignRuntimeConfigService(), new DesignToolbarSession(), new DesignWorkflowCoordinator()),
            saveSettings: () => { },
            resetDefaults: () => { },
            openPreferences: () => { },
            exit: () => { },
            refreshWacomStatus: () => { },
            reconnectPen: () => { },
            startWacomService: () => { },
            showAbout: () => { })
    {
    }

    public MainViewModel(
        ZoneListViewModel zoneList,
        ZoneEditorViewModel zoneEditor,
        ToolbarViewModel toolbar,
        Action saveSettings,
        Action resetDefaults,
        Action openPreferences,
        Action exit,
        Action refreshWacomStatus,
        Action reconnectPen,
        Action startWacomService,
        Action showAbout)
    {
        ZoneList = zoneList ?? throw new ArgumentNullException(nameof(zoneList));
        ZoneEditor = zoneEditor ?? throw new ArgumentNullException(nameof(zoneEditor));
        Toolbar = toolbar ?? throw new ArgumentNullException(nameof(toolbar));
        SaveSettingsCommand = new DelegateCommand(saveSettings ?? throw new ArgumentNullException(nameof(saveSettings)));
        ResetDefaultsCommand = new DelegateCommand(resetDefaults ?? throw new ArgumentNullException(nameof(resetDefaults)));
        OpenPreferencesCommand = new DelegateCommand(openPreferences ?? throw new ArgumentNullException(nameof(openPreferences)));
        ExitCommand = new DelegateCommand(exit ?? throw new ArgumentNullException(nameof(exit)));
        RefreshWacomStatusCommand = new DelegateCommand(refreshWacomStatus ?? throw new ArgumentNullException(nameof(refreshWacomStatus)));
        ReconnectPenCommand = new DelegateCommand(reconnectPen ?? throw new ArgumentNullException(nameof(reconnectPen)));
        StartWacomServiceCommand = new DelegateCommand(startWacomService ?? throw new ArgumentNullException(nameof(startWacomService)));
        AboutCommand = new DelegateCommand(showAbout ?? throw new ArgumentNullException(nameof(showAbout)));
    }

    public ZoneListViewModel ZoneList { get; }

    public ZoneEditorViewModel ZoneEditor { get; }

    public ToolbarViewModel Toolbar { get; }

    public ObservableCollection<int> MidiChannels => ZoneEditor.MidiChannels;

    public ObservableCollection<string> ReleaseBehaviorOptions => ZoneEditor.ReleaseBehaviorOptions;

    public ObservableCollection<ZoneListItemViewModel> Zones => ZoneEditor.Zones;

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

    public bool SendPressureGlobally
    {
        get => Toolbar.SendPressureGlobally;
        set => Toolbar.SendPressureGlobally = value;
    }

    public bool AllowZoneSwitchWhilePressed
    {
        get => Toolbar.AllowZoneSwitchWhilePressed;
        set => Toolbar.AllowZoneSwitchWhilePressed = value;
    }

    public string ToolbarPressureLabelText
    {
        get => Toolbar.ToolbarPressureLabelText;
        set => Toolbar.ToolbarPressureLabelText = value;
    }

    public string ToolbarInputBackendLabelText
    {
        get => Toolbar.ToolbarInputBackendLabelText;
        set => Toolbar.ToolbarInputBackendLabelText = value;
    }

    public string SelectedPressureCurve
    {
        get => Toolbar.SelectedPressureCurve;
        set => Toolbar.SelectedPressureCurve = value;
    }

    public string SelectedInputBackendMode
    {
        get => Toolbar.SelectedInputBackendMode;
        set => Toolbar.SelectedInputBackendMode = value;
    }

    public bool IsPressureCurveLinearChecked
    {
        get => Toolbar.IsPressureCurveLinearChecked;
        set => Toolbar.IsPressureCurveLinearChecked = value;
    }

    public bool IsPressureCurveLogarithmicChecked
    {
        get => Toolbar.IsPressureCurveLogarithmicChecked;
        set => Toolbar.IsPressureCurveLogarithmicChecked = value;
    }

    public bool IsPressureCurveExponentialChecked
    {
        get => Toolbar.IsPressureCurveExponentialChecked;
        set => Toolbar.IsPressureCurveExponentialChecked = value;
    }

    public bool IsInputBackendAutoChecked
    {
        get => Toolbar.IsInputBackendAutoChecked;
        set => Toolbar.IsInputBackendAutoChecked = value;
    }

    public bool IsInputBackendWintabChecked
    {
        get => Toolbar.IsInputBackendWintabChecked;
        set => Toolbar.IsInputBackendWintabChecked = value;
    }

    public bool IsInputBackendMouseChecked
    {
        get => Toolbar.IsInputBackendMouseChecked;
        set => Toolbar.IsInputBackendMouseChecked = value;
    }

    public ICommand SaveSettingsCommand { get; }

    public ICommand ResetDefaultsCommand { get; }

    public ICommand OpenPreferencesCommand { get; }

    public ICommand ExitCommand { get; }

    public ICommand RefreshWacomStatusCommand { get; }

    public ICommand ReconnectPenCommand { get; }

    public ICommand StartWacomServiceCommand { get; }

    public ICommand AboutCommand { get; }

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

    public string MaxZoneRowsText
    {
        get => ZoneEditor.MaxZoneRowsText;
        set => ZoneEditor.MaxZoneRowsText = value;
    }

    public string MaxZonesPerRowText
    {
        get => ZoneEditor.MaxZonesPerRowText;
        set => ZoneEditor.MaxZonesPerRowText = value;
    }

    public string ZoneSelectionSummaryPrimaryText
    {
        get => ZoneEditor.ZoneSelectionSummaryPrimaryText;
        set => ZoneEditor.ZoneSelectionSummaryPrimaryText = value;
    }

    public string ZoneSelectionSummarySecondaryText
    {
        get => ZoneEditor.ZoneSelectionSummarySecondaryText;
        set => ZoneEditor.ZoneSelectionSummarySecondaryText = value;
    }

    public int SelectedZoneIndex
    {
        get => ZoneEditor.SelectedZoneIndex;
        set => ZoneEditor.SelectedZoneIndex = value;
    }

    internal void NotifySelectedZoneIndexChanged() => ZoneEditor.NotifySelectedZoneIndexChanged();

    public bool ZoneDetailsEnabled
    {
        get => ZoneEditor.ZoneDetailsEnabled;
        set => ZoneEditor.ZoneDetailsEnabled = value;
    }

    public string ZoneName
    {
        get => ZoneEditor.ZoneName;
        set => ZoneEditor.ZoneName = value;
    }

    public bool ZoneEnabled
    {
        get => ZoneEditor.ZoneEnabled;
        set => ZoneEditor.ZoneEnabled = value;
    }

    public int ZoneMidiChannel
    {
        get => ZoneEditor.ZoneMidiChannel;
        set => ZoneEditor.ZoneMidiChannel = value;
    }

    public string ZoneCcX
    {
        get => ZoneEditor.ZoneCcX;
        set => ZoneEditor.ZoneCcX = value;
    }

    public string ZoneCcY
    {
        get => ZoneEditor.ZoneCcY;
        set => ZoneEditor.ZoneCcY = value;
    }

    public string ZoneCcPressure
    {
        get => ZoneEditor.ZoneCcPressure;
        set => ZoneEditor.ZoneCcPressure = value;
    }

    public string ZoneLeft
    {
        get => ZoneEditor.ZoneLeft;
        set => ZoneEditor.ZoneLeft = value;
    }

    public string ZoneTop
    {
        get => ZoneEditor.ZoneTop;
        set => ZoneEditor.ZoneTop = value;
    }

    public string ZoneWidth
    {
        get => ZoneEditor.ZoneWidth;
        set => ZoneEditor.ZoneWidth = value;
    }

    public string ZoneHeight
    {
        get => ZoneEditor.ZoneHeight;
        set => ZoneEditor.ZoneHeight = value;
    }

    public bool ZoneSendX
    {
        get => ZoneEditor.ZoneSendX;
        set => ZoneEditor.ZoneSendX = value;
    }

    public bool ZoneSendY
    {
        get => ZoneEditor.ZoneSendY;
        set => ZoneEditor.ZoneSendY = value;
    }

    public bool ZoneInvertX
    {
        get => ZoneEditor.ZoneInvertX;
        set => ZoneEditor.ZoneInvertX = value;
    }

    public bool ZoneInvertY
    {
        get => ZoneEditor.ZoneInvertY;
        set => ZoneEditor.ZoneInvertY = value;
    }

    public bool ZoneSendPressure
    {
        get => ZoneEditor.ZoneSendPressure;
        set => ZoneEditor.ZoneSendPressure = value;
    }

    public string ZoneReleaseBehavior
    {
        get => ZoneEditor.ZoneReleaseBehavior;
        set => ZoneEditor.ZoneReleaseBehavior = value;
    }

    private sealed class DesignRuntimeConfigService : Services.IRuntimeConfigService
    {
        public AppConfig Current { get; private set; } = AppConfig.Default;

        public event EventHandler? CurrentChanged;

        public AppConfig Load()
        {
            CurrentChanged?.Invoke(this, EventArgs.Empty);
            return Current;
        }

        public void Replace(AppConfig config)
        {
            Current = config;
            CurrentChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ResetToDefaults() => Replace(AppConfig.Default);

        public void SaveCurrent()
        {
        }
    }

    private sealed class DesignZoneEditorService : Services.IZoneEditorService
    {
        public void RefreshZoneList(ObservableCollection<ZoneListItemViewModel> zones, AppConfig config)
        {
        }

        public void LoadSelectedZoneIntoEditor(IZoneEditorState viewModel, AppConfig config, int selectedZoneIndex)
        {
        }

        public void CommitCurrentZoneEdits(AppConfig config, IZoneEditorState viewModel, int selectedZoneIndex, string? maxZoneRowsText, string? maxZonesPerRowText)
        {
        }

        public void SetZoneSelectionSummary(IZoneEditorState viewModel, ZoneConfig? zone)
        {
        }
    }

    private sealed class DesignZoneActionWorkflowCoordinator : Services.IZoneActionWorkflowCoordinator
    {
        public Services.ZoneActionWorkflowResult AutoLayout(AppConfig config, int selectedZoneIndex) => new(false, selectedZoneIndex, string.Empty);

        public Services.ZoneActionWorkflowResult AddZone(AppConfig config) => new(false, 0, string.Empty);

        public Services.ZoneActionWorkflowResult RemoveZone(AppConfig config, int selectedZoneIndex) => new(false, selectedZoneIndex, string.Empty);
    }

    private sealed class DesignToolbarSession : Services.IMainWindowToolbarSession
    {
        public void ApplyPressureCurve(AppConfig config, string curve)
        {
        }

        public Services.ToolbarInputModeApplyResult TryApplyInputBackendMode(AppConfig config, string mode)
            => new(false, false);

        public string GetNextCycledInputBackendModeLabel(AppConfig config) => "Auto";
    }

    private sealed class DesignWorkflowCoordinator : Services.IMainWindowWorkflowCoordinator
    {
        public int GetEffectiveExpressKeyCount(int detectedExpressKeyCount, int configuredExpressKeyCount) => 0;

        public Services.WorkflowSaveResult ValidateAndSave(AppConfig config) => new(true, null);

        public Services.WorkflowSaveResult ApplySettingsAndPersist(AppConfig config, Services.IConfigStore store) => new(true, null);

        public Services.WorkflowSaveResult PersistConfigToDisk(AppConfig config, Services.IConfigStore store) => new(true, null);

        public Services.PreferencesWorkflowResult ApplyPreferences(AppConfig config, Services.PreferencesDialogResult result, IntPtr midiOutHandle)
            => new(true, null, false, false);

        public Services.WacomServiceMenuState BuildWacomServiceMenuState(Services.WacomServiceStatus status) => new(string.Empty, false);

        public Services.MainWindowToolbarState BuildToolbarState(AppConfig config)
            => new("Pressure (Lin)", "Linear", "Input (Auto)", "Auto", true, false, false, true, false, false);

        public Services.MainWindowConnectedState BuildConnectedState(AppConfig config)
            => new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
    }
}
