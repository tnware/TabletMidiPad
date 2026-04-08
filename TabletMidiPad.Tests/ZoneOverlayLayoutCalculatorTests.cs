using System.Collections.Generic;
using TabletMidiPad.Core.Overlay;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class ZoneOverlayLayoutCalculatorTests
{
    [Fact]
    public void BuildLayout_MapsZoneToPixelBounds()
    {
        var zones = new[]
        {
            new ZoneOverlayZoneSnapshot(
                SendX: true,
                SendY: true,
                Left: 0.25,
                Top: 0.10,
                Width: 0.50,
                Height: 0.40),
        };

        IReadOnlyList<ZoneOverlayLayoutEntry> items = ZoneOverlayLayoutCalculator.BuildLayout(
            zones,
            padWidth: 200,
            padHeight: 100,
            lastLocalByZoneIndex: new Dictionary<int, (double x, double y)>());

        ZoneOverlayLayoutEntry item = Assert.Single(items);
        Assert.Equal(50, item.LeftPx);
        Assert.Equal(10, item.TopPx);
        Assert.Equal(100, item.WidthPx);
        Assert.Equal(40, item.HeightPx);
        Assert.Null(item.MarkerCenterPx);
    }

    [Fact]
    public void BuildLayout_UsesHalfMarkerWhenAxisDisabled()
    {
        var zones = new[]
        {
            new ZoneOverlayZoneSnapshot(
                SendX: false,
                SendY: true,
                Left: 0.0,
                Top: 0.0,
                Width: 1.0,
                Height: 1.0),
        };

        var lastLocal = new Dictionary<int, (double x, double y)>
        {
            [0] = (0.9, 0.2),
        };

        IReadOnlyList<ZoneOverlayLayoutEntry> items = ZoneOverlayLayoutCalculator.BuildLayout(
            zones,
            padWidth: 100,
            padHeight: 100,
            lastLocalByZoneIndex: lastLocal);

        ZoneOverlayLayoutEntry item = Assert.Single(items);
        Assert.NotNull(item.MarkerCenterPx);
        Assert.Equal(50, item.MarkerCenterPx!.Value.x); // X disabled => 0.5
        Assert.Equal(20, item.MarkerCenterPx!.Value.y); // Y enabled => 0.2
    }
}
