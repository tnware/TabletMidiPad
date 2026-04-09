using System;
using System.Collections.ObjectModel;
using TabletMidiPad;
using TabletMidiPad.Services;

namespace TabletMidiPad.ViewModels;

/// <summary>
/// Root shell view-model for <see cref="Views.MainWindow"/>.
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
            new MainShellCommandsViewModel(new DesignMainShellCommandController()))
    {
    }

    public MainViewModel(
        ZoneListViewModel zoneList,
        ZoneEditorViewModel zoneEditor,
        ToolbarViewModel toolbar,
        MainShellCommandsViewModel shellCommands)
    {
        ZoneList = zoneList ?? throw new ArgumentNullException(nameof(zoneList));
        ZoneEditor = zoneEditor ?? throw new ArgumentNullException(nameof(zoneEditor));
        Toolbar = toolbar ?? throw new ArgumentNullException(nameof(toolbar));
        ShellCommands = shellCommands ?? throw new ArgumentNullException(nameof(shellCommands));
    }

    public ZoneListViewModel ZoneList { get; }

    public ZoneEditorViewModel ZoneEditor { get; }

    public ToolbarViewModel Toolbar { get; }

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

    public MainShellCommandsViewModel ShellCommands { get; }

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

    private sealed class DesignToolbarSession : Services.IToolbarConfigService
    {
        public void ApplyPressureCurve(AppConfig config, string curve)
        {
        }

        public Services.ToolbarInputModeApplyResult TryApplyInputBackendMode(AppConfig config, string mode)
            => new(false, false);

        public string GetNextCycledInputBackendModeLabel(AppConfig config) => "Auto";
    }

    private sealed class DesignWorkflowCoordinator : Services.IWorkflowCoordinator
    {
        public int GetEffectiveExpressKeyCount(int detectedExpressKeyCount, int configuredExpressKeyCount) => 0;

        public Services.WorkflowSaveResult ValidateAndSave(AppConfig config) => new(true, null);

        public Services.PreferencesWorkflowResult ApplyPreferences(AppConfig config, Services.PreferencesDialogResult result, IntPtr midiOutHandle)
            => new(true, null, false, false);

        public Services.WacomServiceMenuState BuildWacomServiceMenuState(Services.WacomServiceStatus status) => new(string.Empty, false);

        public Services.ToolbarState BuildToolbarState(AppConfig config)
            => new("Pressure (Lin)", "Linear", "Input (Auto)", "Auto", true, false, false, true, false, false);

        public Services.ConnectedState BuildConnectedState(AppConfig config)
            => new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
    }

    private sealed class DesignMainShellCommandController : IMainShellCommandController
    {
        public void SaveSettings()
        {
        }

        public void ResetDefaults()
        {
        }

        public void OpenPreferences()
        {
        }

        public void Exit()
        {
        }

        public void RefreshWacomStatus()
        {
        }

        public void ReconnectPen()
        {
        }

        public void StartWacomService()
        {
        }

        public void ShowAbout()
        {
        }
    }
}
