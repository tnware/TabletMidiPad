namespace TabletMidiPad.Services.Zone;

/// <summary>
/// Default factory for <see cref="ZonePadController"/>.
/// </summary>
internal sealed class ZonePadControllerFactory : IZonePadControllerFactory
{
    private readonly IZoneResizeService _zoneResizeService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZonePadControllerFactory"/> class.
    /// </summary>
    public ZonePadControllerFactory(IZoneResizeService zoneResizeService)
    {
        _zoneResizeService = zoneResizeService ?? throw new ArgumentNullException(nameof(zoneResizeService));
    }

    /// <inheritdoc />
    public IZonePadController Create(IZonePadControllerHost host)
        => new ZonePadController(host, _zoneResizeService);
}
