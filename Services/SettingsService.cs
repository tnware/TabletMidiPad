using System;
using System.Collections.Generic;

namespace TabletMidiPad.Services;

public sealed class SettingsService : ISettingsService
{
    public AppConfig Load() => AppConfig.Load();

    public void Save(AppConfig config)
    {
        Validate(config);
        config.Save();
    }

    public void Validate(AppConfig config)
    {
        if (config.Zones.Count == 0)
        {
            throw new InvalidOperationException("At least one zone is required.");
        }

        if (config.MaxZoneRows < 1)
        {
            throw new InvalidOperationException("Max rows must be at least 1.");
        }

        if (config.MaxZonesPerRow < 1)
        {
            throw new InvalidOperationException("Max zones per row must be at least 1.");
        }

        foreach (ZoneConfig zone in config.Zones)
        {
            if (zone.Width <= 0 || zone.Height <= 0)
            {
                throw new InvalidOperationException($"Zone '{zone.Name}' must have width and height greater than 0.");
            }

            if (zone.Left + zone.Width > 1.0 || zone.Top + zone.Height > 1.0)
            {
                throw new InvalidOperationException($"Zone '{zone.Name}' must fit within the pad bounds.");
            }

            _ = NormalizeReleaseBehavior(zone.ReleaseBehavior);
        }
    }

    public void AutoTileZones(AppConfig config)
    {
        IList<ZoneConfig> zones = config.Zones;
        if (zones.Count == 0)
        {
            return;
        }

        int count = zones.Count;
        int maxRows = Math.Max(1, config.MaxZoneRows);
        int maxPerRow = Math.Max(1, config.MaxZonesPerRow);
        int capacity = maxRows * maxPerRow;
        int rows = count <= capacity
            ? Math.Min(maxRows, Math.Max(1, (int)Math.Ceiling((double)count / maxPerRow)))
            : Math.Max(1, (int)Math.Ceiling((double)count / maxPerRow));

        double cellHeight = 1.0 / rows;

        for (int index = 0; index < count; index++)
        {
            int row = index / maxPerRow;
            int colsInThisRow = Math.Min(maxPerRow, count - row * maxPerRow);
            int column = index % maxPerRow;
            double cellWidth = 1.0 / colsInThisRow;

            zones[index].Left = column * cellWidth;
            zones[index].Top = row * cellHeight;
            zones[index].Width = cellWidth;
            zones[index].Height = cellHeight;
        }
    }

    private static string NormalizeReleaseBehavior(string? text)
    {
        string value = (text ?? string.Empty).Trim();
        return value switch
        {
            "Hold Last Values" => ZoneReleaseBehaviors.Hold,
            ZoneReleaseBehaviors.Hold => value,
            ZoneReleaseBehaviors.ResetAll => value,
            ZoneReleaseBehaviors.NoSendOnRelease => value,
            _ => ZoneReleaseBehaviors.ResetPressureOnly
        };
    }
}
