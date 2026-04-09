namespace TabletMidiPad.Services;

/// <summary>
/// Default workflow coordinator for zone action operations.
/// </summary>
internal sealed class ZoneActionWorkflowCoordinator : IZoneActionWorkflowCoordinator
{
    private readonly IZoneConfigurationService _zoneConfigurationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZoneActionWorkflowCoordinator"/> class.
    /// </summary>
    /// <param name="zoneConfigurationService">Zone configuration service dependency.</param>
    public ZoneActionWorkflowCoordinator(IZoneConfigurationService zoneConfigurationService)
    {
        _zoneConfigurationService = zoneConfigurationService;
    }

    /// <inheritdoc />
    public ZoneActionWorkflowResult AutoLayout(AppConfig config, int selectedZoneIndex)
    {
        _zoneConfigurationService.AutoTileZones(config);
        int nextSelection = config.Zones.Count == 0
            ? -1
            : System.Math.Clamp(selectedZoneIndex, 0, config.Zones.Count - 1);
        return new ZoneActionWorkflowResult(
            Changed: true,
            SelectedZoneIndex: nextSelection,
            StatusMessage: "Zones re-tiled across the pad. Click Apply Settings to save.");
    }

    /// <inheritdoc />
    public ZoneActionWorkflowResult AddZone(AppConfig config)
    {
        int nextCcStart = _zoneConfigurationService.GetNextAvailableCcStart(config);
        var zone = new ZoneConfig
        {
            Name = $"Zone {config.Zones.Count + 1}",
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

        config.Zones.Add(zone);
        _zoneConfigurationService.AutoTileZones(config);
        int nextSelection = config.Zones.Count - 1;
        return new ZoneActionWorkflowResult(
            Changed: true,
            SelectedZoneIndex: nextSelection,
            StatusMessage: "Zone added. Click Apply Settings to save.");
    }

    /// <inheritdoc />
    public ZoneActionWorkflowResult RemoveZone(AppConfig config, int selectedZoneIndex)
    {
        if (selectedZoneIndex < 0 || selectedZoneIndex >= config.Zones.Count)
        {
            return new ZoneActionWorkflowResult(
                Changed: false,
                SelectedZoneIndex: selectedZoneIndex,
                StatusMessage: string.Empty);
        }

        config.Zones.RemoveAt(selectedZoneIndex);
        if (config.Zones.Count == 0)
        {
            config.Zones.Add(ZoneConfig.CreateDefault());
        }

        _zoneConfigurationService.AutoTileZones(config);
        int nextSelection = System.Math.Clamp(selectedZoneIndex, 0, config.Zones.Count - 1);
        return new ZoneActionWorkflowResult(
            Changed: true,
            SelectedZoneIndex: nextSelection,
            StatusMessage: "Zone removed. Click Apply Settings to save.");
    }
}
