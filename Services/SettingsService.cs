using System;
using TabletMidiPad.Models;

namespace TabletMidiPad.Services;

public sealed class SettingsService : ISettingsService
{
    private AppConfig? _config;

    public AppConfig Config => _config ?? throw new InvalidOperationException("Configuration not loaded.");

    public void Load()
    {
        _config = AppConfig.Load();
    }

    public void Save()
    {
        Config.Save();
    }

    public void ResetToDefault()
    {
        _config = AppConfig.Default;
    }
}
