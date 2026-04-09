using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    public bool AllowPageSwitchWhilePressed { get; set; }
    public bool WrapPageNavigation { get; set; } = true;
    public List<PageConfig> Pages { get; set; } = new() { PageConfig.CreateDefault(1) };
    public int MaxZoneRows { get; set; } = 2;
    public int MaxZonesPerRow { get; set; } = 2;
    public List<ZoneConfig> Zones { get; set; } = new() { ZoneConfig.CreateDefault() };

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ZoneOverlayStyle ZoneOverlayStyle { get; set; } = ZoneOverlayStyle.Plain;

    public AppConfig()
    {
        SyncLegacyFromFirstPage();
    }

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

        config.Zones ??= new();
        config.Pages ??= new();

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

        bool hasPageData = config.Pages.Count > 0;
        if (!hasPageData)
        {
            if (config.Zones.Count == 0)
            {
                config.Zones.Add(ZoneConfig.CreateDefault());
            }

            config.Pages.Add(new PageConfig
            {
                Name = "Page 1",
                MaxZoneRows = Math.Max(1, config.MaxZoneRows),
                MaxZonesPerRow = Math.Max(1, config.MaxZonesPerRow),
                Zones = config.Zones
            });
        }

        for (int pageIndex = 0; pageIndex < config.Pages.Count; pageIndex++)
        {
            PageConfig page = config.Pages[pageIndex];
            page.Name = string.IsNullOrWhiteSpace(page.Name) ? $"Page {pageIndex + 1}" : page.Name.Trim();
            page.MaxZoneRows = Math.Max(1, page.MaxZoneRows);
            page.MaxZonesPerRow = Math.Max(1, page.MaxZonesPerRow);
            page.Zones ??= new();
            if (page.Zones.Count == 0)
            {
                page.Zones.Add(ZoneConfig.CreateDefault());
            }

            foreach (ZoneConfig zone in page.Zones)
            {
                if (string.IsNullOrWhiteSpace(zone.ReleaseBehavior))
                {
                    zone.ReleaseBehavior = zone.ResetPressureOnRelease
                        ? ZoneReleaseBehaviors.ResetPressureOnly
                        : ZoneReleaseBehaviors.NoSendOnRelease;
                }
            }
        }

        config.SyncLegacyFromFirstPage();
        return config;
    }

    public static PageConfig GetPage(AppConfig config, int pageIndex)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Pages ??= new();
        if (config.Pages.Count == 0)
        {
            config.Pages.Add(PageConfig.CreateDefault(1));
        }

        int safeIndex = Math.Clamp(pageIndex, 0, config.Pages.Count - 1);
        PageConfig page = config.Pages[safeIndex];
        page.Zones ??= new();
        if (page.Zones.Count == 0)
        {
            page.Zones.Add(ZoneConfig.CreateDefault());
        }

        page.MaxZoneRows = Math.Max(1, page.MaxZoneRows);
        page.MaxZonesPerRow = Math.Max(1, page.MaxZonesPerRow);
        if (string.IsNullOrWhiteSpace(page.Name))
        {
            page.Name = $"Page {safeIndex + 1}";
        }

        return page;
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
        SyncLegacyFromFirstPage();
        string path = GetConfigPath();
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonWriteOptions));
    }

    private void SyncLegacyFromFirstPage()
    {
        Pages ??= new();
        if (Pages.Count == 0)
        {
            Pages.Add(PageConfig.CreateDefault(1));
        }

        PageConfig first = Pages[0];
        first.Zones ??= new();
        if (first.Zones.Count == 0)
        {
            first.Zones.Add(ZoneConfig.CreateDefault());
        }

        first.MaxZoneRows = Math.Max(1, first.MaxZoneRows);
        first.MaxZonesPerRow = Math.Max(1, first.MaxZonesPerRow);
        first.Name = string.IsNullOrWhiteSpace(first.Name) ? "Page 1" : first.Name.Trim();

        MaxZoneRows = first.MaxZoneRows;
        MaxZonesPerRow = first.MaxZonesPerRow;
        Zones = first.Zones;
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

public static class ExpressKeyActions
{
    public const string None = PenButtonActions.None;
    public const string CcMomentary = PenButtonActions.CcMomentary;
    public const string CcToggle = PenButtonActions.CcToggle;
    public const string NoteMomentary = PenButtonActions.NoteMomentary;
    public const string NoteToggle = PenButtonActions.NoteToggle;
    public const string PrevPage = "Prev Page";
    public const string NextPage = "Next Page";
    public const string GoToPage1 = "Go To Page 1";
    public const string GoToPage2 = "Go To Page 2";
    public const string GoToPage3 = "Go To Page 3";
    public const string GoToPage4 = "Go To Page 4";
    public const string GoToPage5 = "Go To Page 5";
    public const string GoToPage6 = "Go To Page 6";
    public const string GoToPage7 = "Go To Page 7";
    public const string GoToPage8 = "Go To Page 8";
}

public sealed class PageConfig
{
    public string Name { get; set; } = "Page 1";
    public int MaxZoneRows { get; set; } = 2;
    public int MaxZonesPerRow { get; set; } = 2;
    public List<ZoneConfig> Zones { get; set; } = new() { ZoneConfig.CreateDefault() };

    public static PageConfig CreateDefault(int oneBasedPageNumber)
    {
        return new PageConfig
        {
            Name = $"Page {Math.Max(1, oneBasedPageNumber)}",
            MaxZoneRows = 2,
            MaxZonesPerRow = 2,
            Zones = new List<ZoneConfig> { ZoneConfig.CreateDefault() }
        };
    }
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
