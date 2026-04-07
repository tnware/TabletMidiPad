using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabletMidiPad.Services;

namespace TabletMidiPad.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private IMainWindowActions? _actions;

    private AppConfig _config = AppConfig.Default;

    [ObservableProperty]
    private string _statusText = "Connected";

    [ObservableProperty]
    private string _routingText = string.Empty;

    [ObservableProperty]
    private string _inputBackendText = string.Empty;

    [ObservableProperty]
    private string _activeZoneText = "Active zone: none";

    [ObservableProperty]
    private string _valueText = "X: -, Y: -, Pressure: -";

    [ObservableProperty]
    private string _penButtonsText = "Pen buttons: -";

    [ObservableProperty]
    private string _modifiersText = "Modifiers: none";

    [ObservableProperty]
    private string _configSummaryText = string.Empty;

    [ObservableProperty]
    private string _settingsResultText = string.Empty;

    [ObservableProperty]
    private Brush _settingsResultBrush = Brushes.DarkSeaGreen;

    [ObservableProperty]
    private string _wacomServiceStatusText = "Service: checking...";

    [ObservableProperty]
    private bool _canStartWacomService;

    [ObservableProperty]
    private string _toolbarPressureLabelText = "Pressure (Lin)";

    [ObservableProperty]
    private string _toolbarInputBackendLabelText = "Input (Auto)";

    public AppConfig Config
    {
        get => _config;
        private set
        {
            if (SetProperty(ref _config, value))
            {
                OnPropertyChanged(nameof(SendPressureGlobally));
                OnPropertyChanged(nameof(AllowZoneSwitchWhilePressed));
                RefreshConfigSummaryFromConfig();
            }
        }
    }

    public bool SendPressureGlobally
    {
        get => Config.SendPressureGlobally;
        set
        {
            if (Config.SendPressureGlobally == value)
            {
                return;
            }

            Config.SendPressureGlobally = value;
            OnPropertyChanged();
        }
    }

    public bool AllowZoneSwitchWhilePressed
    {
        get => Config.AllowZoneSwitchWhilePressed;
        set
        {
            if (Config.AllowZoneSwitchWhilePressed == value)
            {
                return;
            }

            Config.AllowZoneSwitchWhilePressed = value;
            OnPropertyChanged();
            RefreshConfigSummaryFromConfig();
        }
    }

    public MainViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [RelayCommand]
    private void SaveSettings() => _actions?.ApplySettings();

    [RelayCommand]
    private void ResetDefaults() => _actions?.ResetDefaults();

    [RelayCommand]
    private void OpenPreferences() => _actions?.OpenPreferences();

    [RelayCommand]
    private void Exit() => _actions?.Exit();

    [RelayCommand]
    private void RefreshWacomStatus() => _actions?.RefreshWacomStatus();

    [RelayCommand]
    private void ReconnectInput() => _actions?.ReconnectInput();

    [RelayCommand]
    private void StartWacomService() => _actions?.StartWacomService();

    [RelayCommand]
    private void ShowAbout() => _actions?.ShowAbout();

    [RelayCommand]
    private void AutoLayout() => _actions?.AutoLayoutZones();

    [RelayCommand]
    private void AddZone() => _actions?.AddZone();

    [RelayCommand]
    private void RemoveZone() => _actions?.RemoveZone();

    public void AttachActions(IMainWindowActions actions) => _actions = actions;

    public void Load() => Config = _settingsService.Load();
    public void Save() => _settingsService.Save(Config);
    public void ResetToDefaults() => Config = AppConfig.Default;
    public void AutoTileZones() => _settingsService.AutoTileZones(Config);

    private void RefreshConfigSummaryFromConfig()
    {
        string switchMode = Config.AllowZoneSwitchWhilePressed ? "latch on enter" : "lock zone until release";
        ConfigSummaryText = $"{Config.Zones.Count} zone(s) configured | layout {Config.MaxZoneRows} row(s) max / {Config.MaxZonesPerRow} per row | {switchMode}";
    }
}
