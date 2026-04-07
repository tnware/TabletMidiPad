using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TabletMidiPad;

public sealed class AppConfig
{
    public string AppPortName { get; set; } = "Tablet XY (App)";
    public string DawPortName { get; set; } = "Tablet XY (DAW)";
    public string LoopbackAssociationId { get; set; } = "{7d6e1f6b-6b0d-4d6c-a1f3-44ddefb7df4d}";
    public int MousePressureValue { get; set; } = 127;
    public bool SendPressureGlobally { get; set; } = true;
    public double PrecisionModeScale { get; set; } = 0.25;
    public bool AllowZoneSwitchWhilePressed { get; set; } = true;
    public List<ZoneConfig> Zones { get; set; } = new() { ZoneConfig.CreateDefault() };

    public static AppConfig Default => new();

    public static string GetConfigPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "config.json");
    }

    public static AppConfig Load()
    {
        string path = GetConfigPath();
        if (!File.Exists(path))
        {
            return Default;
        }

        try
        {
            string json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<AppConfig>(json);
            if (config is null)
            {
                return Default;
            }

            if (config.Zones is null || config.Zones.Count == 0)
            {
                config.Zones = new List<ZoneConfig> { ZoneConfig.CreateDefault() };
            }

            return config;
        }
        catch
        {
            return Default;
        }
    }

    public void Save()
    {
        string path = GetConfigPath();
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        File.WriteAllText(path, JsonSerializer.Serialize(this, options));
    }
}

public sealed class ZoneConfig
{
    public string Name { get; set; } = "Zone 1";
    public bool Enabled { get; set; } = true;
    public int MidiChannel { get; set; } = 1;
    public int CcX { get; set; } = 16;
    public int CcY { get; set; } = 17;
    public int CcPressure { get; set; } = 18;
    public bool InvertX { get; set; }
    public bool InvertY { get; set; } = true;
    public bool SendPressure { get; set; } = true;
    public bool ResetPressureOnRelease { get; set; } = true;

    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; } = 1.0;
    public double Height { get; set; } = 1.0;

    public static ZoneConfig CreateDefault()
    {
        return new ZoneConfig();
    }
}
