using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using TabletMidiPad.Models;

namespace TabletMidiPad.Services;

public sealed class SettingsService : ISettingsService
{
    private AppConfig _config = AppConfig.Default;
    public AppConfig Config => _config;

    public SettingsService() => Load();

    public void Load()
    {
        string path = GetConfigPath();
        if (!File.Exists(path)) { _config = AppConfig.Default; return; }
        try {
            string json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<AppConfig>(json);
            if (config != null) { _config = config; Migrate(_config); }
            else _config = AppConfig.Default;
        } catch { _config = AppConfig.Default; }
    }

    public void Save()
    {
        try {
            string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GetConfigPath(), json);
        } catch { }
    }

    public void ResetToDefaults() { _config = AppConfig.Default; Save(); }

    public AppConfig CloneConfig()
    {
        string json = JsonSerializer.Serialize(_config);
        return JsonSerializer.Deserialize<AppConfig>(json) ?? AppConfig.Default;
    }

    public void ApplyConfig(AppConfig config)
    {
        _config = config;
        Save();
    }

    private static string GetConfigPath() => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

    private void Migrate(AppConfig config)
    {
        if (config.Zones == null || config.Zones.Count == 0) config.Zones = new List<ZoneConfig> { ZoneConfig.CreateDefault() };
        config.ExpressKeys ??= new List<ExpressKeyConfig>();
        if (config.ExpressKeys.Count == 0 && !string.IsNullOrWhiteSpace(config.ExpressKeyAction)) {
            for (int i = 0; i < 8; i++) config.ExpressKeys.Add(new ExpressKeyConfig { Action = config.ExpressKeyAction, MidiChannel = config.ExpressKeyMidiChannel, Cc = Math.Clamp(config.ExpressKeyBaseCc + i, 0, 127), Note = Math.Clamp(config.ExpressKeyBaseNote + i, 0, 127) });
        }
        if (string.IsNullOrWhiteSpace(config.UpperPenButtonAction) && !string.IsNullOrWhiteSpace(config.PenButtonAction)) {
            config.UpperPenButtonAction = config.PenButtonAction;
            config.UpperPenButtonMidiChannel = config.PenButtonMidiChannel;
            config.UpperPenButtonCc = config.PenButtonCc;
            config.UpperPenButtonNote = config.PenButtonNote;
        }
        foreach (var zone in config.Zones) {
            if (string.IsNullOrWhiteSpace(zone.ReleaseBehavior)) {
                zone.ReleaseBehavior = zone.ResetPressureOnRelease ? ZoneReleaseBehaviors.ResetPressureOnly : ZoneReleaseBehaviors.NoSendOnRelease;
            }
        }
    }
}
