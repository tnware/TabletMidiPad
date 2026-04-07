using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TabletMidiPad.Services;

namespace TabletMidiPad.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ISettingsService _settingsService;
    private IMainWindowActions? _actions;

    private AppConfig _config = AppConfig.Default;
    private string _statusText = "Connected";
    private string _routingText = string.Empty;
    private string _inputBackendText = string.Empty;
    private string _activeZoneText = "Active zone: none";
    private string _valueText = "X: -, Y: -, Pressure: -";
    private string _penButtonsText = "Pen buttons: -";
    private string _modifiersText = "Modifiers: none";
    private string _configSummaryText = string.Empty;
    private string _settingsResultText = string.Empty;

    public AppConfig Config
    {
        get => _config;
        private set
        {
            _config = value;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public string RoutingText
    {
        get => _routingText;
        set => SetField(ref _routingText, value);
    }

    public string InputBackendText
    {
        get => _inputBackendText;
        set => SetField(ref _inputBackendText, value);
    }

    public string ActiveZoneText
    {
        get => _activeZoneText;
        set => SetField(ref _activeZoneText, value);
    }

    public string ValueText
    {
        get => _valueText;
        set => SetField(ref _valueText, value);
    }

    public string PenButtonsText
    {
        get => _penButtonsText;
        set => SetField(ref _penButtonsText, value);
    }

    public string ModifiersText
    {
        get => _modifiersText;
        set => SetField(ref _modifiersText, value);
    }

    public string ConfigSummaryText
    {
        get => _configSummaryText;
        set => SetField(ref _configSummaryText, value);
    }

    public string SettingsResultText
    {
        get => _settingsResultText;
        set => SetField(ref _settingsResultText, value);
    }

    public MainViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        SaveSettingsCommand = new RelayCommand(() => _actions?.ApplySettings());
        ResetDefaultsCommand = new RelayCommand(() => _actions?.ResetDefaults());
        OpenPreferencesCommand = new RelayCommand(() => _actions?.OpenPreferences());
        ExitCommand = new RelayCommand(() => _actions?.Exit());
        RefreshWacomStatusCommand = new RelayCommand(() => _actions?.RefreshWacomStatus());
        ReconnectInputCommand = new RelayCommand(() => _actions?.ReconnectInput());
        StartWacomServiceCommand = new RelayCommand(() => _actions?.StartWacomService());
        ShowAboutCommand = new RelayCommand(() => _actions?.ShowAbout());
        AutoLayoutCommand = new RelayCommand(() => _actions?.AutoLayoutZones());
        AddZoneCommand = new RelayCommand(() => _actions?.AddZone());
        RemoveZoneCommand = new RelayCommand(() => _actions?.RemoveZone());
    }

    public ICommand SaveSettingsCommand { get; }
    public ICommand ResetDefaultsCommand { get; }
    public ICommand OpenPreferencesCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand RefreshWacomStatusCommand { get; }
    public ICommand ReconnectInputCommand { get; }
    public ICommand StartWacomServiceCommand { get; }
    public ICommand ShowAboutCommand { get; }
    public ICommand AutoLayoutCommand { get; }
    public ICommand AddZoneCommand { get; }
    public ICommand RemoveZoneCommand { get; }

    public void AttachActions(IMainWindowActions actions) => _actions = actions;

    public void Load() => Config = _settingsService.Load();
    public void Save() => _settingsService.Save(Config);
    public void ResetToDefaults() => Config = AppConfig.Default;
    public void AutoTileZones() => _settingsService.AutoTileZones(Config);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (string.Equals(field, value, System.StringComparison.Ordinal))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }
}
