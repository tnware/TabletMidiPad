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
    public AppConfig Config
    {
        get => _config;
        private set
        {
            _config = value;
            OnPropertyChanged();
        }
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
}
