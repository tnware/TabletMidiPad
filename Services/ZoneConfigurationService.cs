using System;
using System.Collections.Generic;
using System.Linq;

namespace TabletMidiPad.Services;

/// <summary>
/// Encapsulates zone-configuration operations such as validation, auto-tiling,
/// and next CC block selection.
/// </summary>
internal sealed class ZoneConfigurationService : IZoneConfigurationService
{
    /// <inheritdoc />
    public int GetNextAvailableCcStart(AppConfig config)
    {
        IReadOnlyList<ZoneConfig> zones = config.Zones;
        var used = new HashSet<int>(
            zones.SelectMany(zone => new[] { zone.CcX, zone.CcY, zone.CcPressure }));

        int preferredStart = zones.Count == 0
            ? 16
            : zones.Max(zone => Math.Max(zone.CcX, Math.Max(zone.CcY, zone.CcPressure))) + 1;

        for (int start = preferredStart; start <= 125; start++)
        {
            if (!used.Contains(start) && !used.Contains(start + 1) && !used.Contains(start + 2))
            {
                return start;
            }
        }

        for (int start = 16; start <= 125; start++)
        {
            if (!used.Contains(start) && !used.Contains(start + 1) && !used.Contains(start + 2))
            {
                return start;
            }
        }

        throw new InvalidOperationException("No free 3-CC block is available for a new zone.");
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void ValidateConfig(AppConfig config)
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
