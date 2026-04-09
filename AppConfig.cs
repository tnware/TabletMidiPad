using System.IO;
using System.Text.Json;

namespace TabletMidiPad;

public sealed partial class AppConfig
{
    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true
    };

    public string AppPortName { get; set; } = "Tablet XY (App)";
    public string DawPortName { get; set; } = "Tablet XY (DAW)";
    public string LoopbackAssociationId { get; set; } = "{7d6e1f6b-6b0d-4d6c-a1f3-44ddefb7df4d}";
    public int MousePressureValue { get; set; } = 127;
    public double PositionDeadZone { get; set; } = 0.003;
    public double PositionSmoothing { get; set; } = 0.35;
    public string PressureCurve { get; set; } = "Linear";
    public string InputBackendMode { get; set; } = "Auto";
    public string LowerPenButtonAction { get; set; } = PenButtonActions.None;
    public int LowerPenButtonMidiChannel { get; set; } = 1;
    public int LowerPenButtonCc { get; set; } = 64;
    public int LowerPenButtonNote { get; set; } = 60;
    public string UpperPenButtonAction { get; set; } = PenButtonActions.None;
    public int UpperPenButtonMidiChannel { get; set; } = 1;
    public int UpperPenButtonCc { get; set; } = 65;
    public int UpperPenButtonNote { get; set; } = 61;
    public List<ExpressKeyConfig> ExpressKeys { get; set; } = new();
    public bool SendPressureGlobally { get; set; } = true;
    public bool AllowZoneSwitchWhilePressed { get; set; } = true;
    public int MaxZoneRows { get; set; } = 2;
    public int MaxZonesPerRow { get; set; } = 2;
    public List<ZoneConfig> Zones { get; set; } = new() { ZoneConfig.CreateDefault() };

    public static AppConfig Default => new();

    public static string GetConfigPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "config.json");
    }

    /// <summary>
    /// Normalizes and migrates configuration loaded from storage.
    /// </summary>
    /// <param name="config">The deserialized configuration object.</param>
    /// <returns>The normalized configuration.</returns>
    public static AppConfig NormalizeLoaded(AppConfig config)
    {
        if (config is null)
        {
            return Default;
        }

        if (config.Zones is null || config.Zones.Count == 0)
        {
            config.Zones = new() { ZoneConfig.CreateDefault() };
        }

        if (string.IsNullOrWhiteSpace(config.UpperPenButtonAction) && !string.IsNullOrWhiteSpace(config.PenButtonAction))
        {
            config.UpperPenButtonAction = config.PenButtonAction;
            config.UpperPenButtonMidiChannel = config.PenButtonMidiChannel;
            config.UpperPenButtonCc = config.PenButtonCc;
            config.UpperPenButtonNote = config.PenButtonNote;
        }

        config.ExpressKeys ??= new();

        // Migrate legacy single-key settings.
        if (config.ExpressKeys.Count == 0 && !string.IsNullOrWhiteSpace(config.ExpressKeyAction))
        {
            for (int i = 0; i < 8; i++)
            {
                config.ExpressKeys.Add(new ExpressKeyConfig
                {
                    Action = config.ExpressKeyAction,
                    MidiChannel = config.ExpressKeyMidiChannel,
                    Cc = Math.Clamp(config.ExpressKeyBaseCc + i, 0, 127),
                    Note = Math.Clamp(config.ExpressKeyBaseNote + i, 0, 127)
                });
            }
        }

        foreach (ZoneConfig zone in config.Zones)
        {
            if (string.IsNullOrWhiteSpace(zone.ReleaseBehavior))
            {
                zone.ReleaseBehavior = zone.ResetPressureOnRelease
                    ? ZoneReleaseBehaviors.ResetPressureOnly
                    : ZoneReleaseBehaviors.NoSendOnRelease;
            }
        }

        return config;
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
            AppConfig? config = JsonSerializer.Deserialize<AppConfig>(json);
            if (config is null)
            {
                return Default;
            }
            return NormalizeLoaded(config);
        }
        catch
        {
            return Default;
        }
    }

    public void Save()
    {
        string path = GetConfigPath();
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonWriteOptions));
    }
}

public static class PenButtonActions
{
    public const string None = "None";
    public const string LockXY = "Lock XY";
    public const string LockX = "Lock X";
    public const string LockY = "Lock Y";
    public const string DisablePressure = "Disable Pressure";
    public const string EnablePressure = "Enable Pressure";
    public const string CcMomentary = "CC Momentary";
    public const string CcToggle = "CC Toggle";
    public const string NoteMomentary = "Note Momentary";
    public const string NoteToggle = "Note Toggle";
}

public static class ZoneReleaseBehaviors
{
    public const string Hold = "Hold";
    public const string ResetPressureOnly = "Reset Pressure Only";
    public const string ResetAll = "Reset All";
    public const string NoSendOnRelease = "No Send On Release";
}

public sealed class ExpressKeyConfig
{
    public string Action { get; set; } = PenButtonActions.None;
    public int MidiChannel { get; set; } = 1;
    public int Cc { get; set; } = 70;
    public int Note { get; set; } = 36;

    public static ExpressKeyConfig CreateDefault(int index)
    {
        return new ExpressKeyConfig
        {
            Action = PenButtonActions.NoteMomentary,
            MidiChannel = 1,
            Cc = Math.Clamp(70 + index, 0, 127),
            Note = Math.Clamp(36 + index, 0, 127)
        };
    }

    public static void EnsureCount(List<ExpressKeyConfig> list, int count)
    {
        while (list.Count < count)
        {
            list.Add(CreateDefault(list.Count));
        }
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
    public bool SendX { get; set; } = true;
    public bool SendY { get; set; } = true;
    public bool InvertX { get; set; }
    public bool InvertY { get; set; } = true;
    public bool SendPressure { get; set; } = true;
    public string ReleaseBehavior { get; set; } = ZoneReleaseBehaviors.NoSendOnRelease;
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

public sealed partial class AppConfig
{
    // Legacy compatibility only.
    public string PenButtonAction { get; set; } = string.Empty;
    public int PenButtonMidiChannel { get; set; } = 1;
    public int PenButtonCc { get; set; } = 64;
    public int PenButtonNote { get; set; } = 60;
    public string ExpressKeyAction { get; set; } = string.Empty;
    public int ExpressKeyMidiChannel { get; set; } = 1;
    public int ExpressKeyBaseCc { get; set; } = 70;
    public int ExpressKeyBaseNote { get; set; } = 36;
}
