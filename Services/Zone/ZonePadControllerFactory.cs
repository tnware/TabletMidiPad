namespace TabletMidiPad.Services.Zone;

/// <summary>
/// Default factory for <see cref="ZonePadController"/>.
/// </summary>
internal sealed class ZonePadControllerFactory : IZonePadControllerFactory
{
    private readonly IZoneResizeService _zoneResizeService;
    private readonly IPageNavigationService _pageNavigationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZonePadControllerFactory"/> class.
    /// </summary>
    public ZonePadControllerFactory(
        IZoneResizeService zoneResizeService,
        IPageNavigationService pageNavigationService)
    {
        _zoneResizeService = zoneResizeService ?? throw new ArgumentNullException(nameof(zoneResizeService));
        _pageNavigationService = pageNavigationService ?? throw new ArgumentNullException(nameof(pageNavigationService));
    }

    /// <inheritdoc />
    public IZonePadController Create(IZonePadControllerHost host)
        => new ZonePadController(host, _zoneResizeService, _pageNavigationService);
}
