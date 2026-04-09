namespace TabletMidiPad.Services.Zone;

/// <summary>
/// Encapsulates zone-configuration operations such as validation, auto-tiling,
/// and next CC block selection.
/// </summary>
internal sealed class ZoneConfigurationService : IZoneConfigurationService
{
    /// <inheritdoc />
    public int GetNextAvailableCcStart(PageConfig page)
    {
        ArgumentNullException.ThrowIfNull(page);
        List<ZoneConfig> zones = page.Zones;
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
    public void AutoTileZones(PageConfig page)
    {
        ArgumentNullException.ThrowIfNull(page);
        List<ZoneConfig> zones = page.Zones;
        if (zones.Count == 0)
        {
            return;
        }

        int count = zones.Count;
        int maxRows = Math.Max(1, page.MaxZoneRows);
        int maxPerRow = Math.Max(1, page.MaxZonesPerRow);
        int capacity = maxRows * maxPerRow;
        int rows = count <= capacity
            ? Math.Min(maxRows, Math.Max(1, (int)Math.Ceiling((double)count / maxPerRow)))
            : Math.Max(1, (int)Math.Ceiling((double)count / maxPerRow));

        double cellHeight = 1.0 / rows;

        for (int index = 0; index < count; index++)
        {
            int row = index / maxPerRow;
            int colsInThisRow = Math.Min(maxPerRow, count - (row * maxPerRow));
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
        ArgumentNullException.ThrowIfNull(config);
        if (config.Pages is null || config.Pages.Count == 0)
        {
            throw new InvalidOperationException("At least one page is required.");
        }

        foreach (PageConfig page in config.Pages)
        {
            if (page.Zones is null || page.Zones.Count == 0)
            {
                throw new InvalidOperationException($"Page '{page.Name}' must have at least one zone.");
            }

            if (page.MaxZoneRows < 1)
            {
                throw new InvalidOperationException($"Page '{page.Name}' max rows must be at least 1.");
            }

            if (page.MaxZonesPerRow < 1)
            {
                throw new InvalidOperationException($"Page '{page.Name}' max zones per row must be at least 1.");
            }

            foreach (ZoneConfig zone in page.Zones)
            {
                if (zone.Width <= 0 || zone.Height <= 0)
                {
                    throw new InvalidOperationException($"Zone '{zone.Name}' on page '{page.Name}' must have width and height greater than 0.");
                }

                if (zone.Left + zone.Width > 1.0 || zone.Top + zone.Height > 1.0)
                {
                    throw new InvalidOperationException($"Zone '{zone.Name}' on page '{page.Name}' must fit within the pad bounds.");
                }
            }
        }
    }
}
