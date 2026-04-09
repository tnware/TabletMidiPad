using System;

namespace TabletMidiPad.Services;

/// <summary>
/// Default factory for <see cref="ZonePadController"/>.
/// </summary>
internal sealed class ZonePadControllerFactory : IZonePadControllerFactory
{
    private readonly IZoneResizeService _zoneResizeService;

    public ZonePadControllerFactory(IZoneResizeService zoneResizeService)
    {
        _zoneResizeService = zoneResizeService ?? throw new ArgumentNullException(nameof(zoneResizeService));
    }

    public IZonePadController Create(IZonePadControllerHost host)
    {
        return new ZonePadController(host, _zoneResizeService);
    }
}

