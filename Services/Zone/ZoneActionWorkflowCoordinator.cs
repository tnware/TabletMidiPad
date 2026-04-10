namespace TabletMidiPad.Services.Zone;

/// <summary>
/// Default workflow coordinator for zone action operations.
/// </summary>
internal sealed class ZoneActionWorkflowCoordinator : IZoneActionWorkflowCoordinator
{
    private readonly IZoneConfigurationService _zoneConfigurationService;
    private readonly IPageNavigationService _pageNavigationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZoneActionWorkflowCoordinator"/> class.
    /// </summary>
    /// <param name="zoneConfigurationService">Zone configuration service dependency.</param>
    public ZoneActionWorkflowCoordinator(
        IZoneConfigurationService zoneConfigurationService,
        IPageNavigationService pageNavigationService)
    {
        _zoneConfigurationService = zoneConfigurationService ?? throw new ArgumentNullException(nameof(zoneConfigurationService));
        _pageNavigationService = pageNavigationService ?? throw new ArgumentNullException(nameof(pageNavigationService));
    }

    /// <inheritdoc />
    public ZoneActionWorkflowResult AutoLayout(AppConfig config, int selectedZoneIndex)
    {
        PageConfig page = _pageNavigationService.GetActivePage(config);
        int capacity = _zoneConfigurationService.GetZoneCapacity(page);
        if (page.Zones.Count > capacity)
        {
            return new ZoneActionWorkflowResult(
                Changed: false,
                SelectedZoneIndex: Math.Clamp(selectedZoneIndex, -1, page.Zones.Count - 1),
                StatusMessage: $"Zone count exceeds layout capacity ({page.Zones.Count}/{capacity}). Increase limits or remove zones.");
        }

        _zoneConfigurationService.AutoTileZones(page);
        int nextSelection = page.Zones.Count == 0
            ? -1
            : Math.Clamp(selectedZoneIndex, 0, page.Zones.Count - 1);
        return new ZoneActionWorkflowResult(
            Changed: true,
            SelectedZoneIndex: nextSelection,
            StatusMessage: "Zones re-tiled across the pad. Click Apply Settings to save.");
    }

    /// <inheritdoc />
    public ZoneActionWorkflowResult AddZone(AppConfig config)
    {
        PageConfig page = _pageNavigationService.GetActivePage(config);
        int capacity = _zoneConfigurationService.GetZoneCapacity(page);
        if (page.Zones.Count >= capacity)
        {
            return new ZoneActionWorkflowResult(
                Changed: false,
                SelectedZoneIndex: Math.Clamp(page.Zones.Count - 1, -1, page.Zones.Count - 1),
                StatusMessage: $"Zone limit reached ({capacity}). Increase Max Rows or Max Per Row to add more zones.");
        }

        int nextCcStart = _zoneConfigurationService.GetNextAvailableCcStart(page);
        var zone = new ZoneConfig
        {
            Name = $"Zone {page.Zones.Count + 1}",
            Enabled = true,
            MidiChannel = 1,
            CcX = nextCcStart,
            CcY = nextCcStart + 1,
            CcPressure = nextCcStart + 2,
            SendX = true,
            SendY = true,
            InvertY = true,
            SendPressure = true,
            ReleaseBehavior = ZoneReleaseBehaviors.NoSendOnRelease,
            ResetPressureOnRelease = false,
            Left = 0.0,
            Top = 0.0,
            Width = 0.5,
            Height = 0.5
        };

        page.Zones.Add(zone);
        _zoneConfigurationService.AutoTileZones(page);
        int nextSelection = page.Zones.Count - 1;
        return new ZoneActionWorkflowResult(
            Changed: true,
            SelectedZoneIndex: nextSelection,
            StatusMessage: "Zone added. Click Apply Settings to save.");
    }

    /// <inheritdoc />
    public ZoneActionWorkflowResult RemoveZone(AppConfig config, int selectedZoneIndex)
    {
        PageConfig page = _pageNavigationService.GetActivePage(config);
        if (selectedZoneIndex < 0 || selectedZoneIndex >= page.Zones.Count)
        {
            return new ZoneActionWorkflowResult(
                Changed: false,
                SelectedZoneIndex: selectedZoneIndex,
                StatusMessage: string.Empty);
        }

        page.Zones.RemoveAt(selectedZoneIndex);
        if (page.Zones.Count == 0)
        {
            page.Zones.Add(ZoneConfig.CreateDefault());
        }

        _zoneConfigurationService.AutoTileZones(page);
        int nextSelection = Math.Clamp(selectedZoneIndex, 0, page.Zones.Count - 1);
        return new ZoneActionWorkflowResult(
            Changed: true,
            SelectedZoneIndex: nextSelection,
            StatusMessage: "Zone removed. Click Apply Settings to save.");
    }
}
