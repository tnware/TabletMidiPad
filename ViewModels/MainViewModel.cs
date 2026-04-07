using System.ComponentModel;
using System.Runtime.CompilerServices;
using TabletMidiPad.Services;

namespace TabletMidiPad.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ISettingsService _settingsService;

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
    }

    public void Load() => Config = _settingsService.Load();
    public void Save() => _settingsService.Save(Config);
    public void ResetToDefaults() => Config = AppConfig.Default;
    public void AutoTileZones() => _settingsService.AutoTileZones(Config);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
