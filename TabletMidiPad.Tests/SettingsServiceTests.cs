using TabletMidiPad.Services;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class SettingsServiceTests
{
    private readonly SettingsService _service = new();

    [Fact]
    public void Validate_RejectsZeroZones()
    {
        var config = AppConfig.Default;
        config.Zones.Clear();

        Assert.Throws<InvalidOperationException>(() => _service.Validate(config));
    }

    [Fact]
    public void Validate_RejectsNonPositiveDimensions()
    {
        var config = AppConfig.Default;
        config.Zones[0].Width = 0;

        Assert.Throws<InvalidOperationException>(() => _service.Validate(config));
    }

    [Fact]
    public void Validate_RejectsOutOfBoundsZone()
    {
        var config = AppConfig.Default;
        config.Zones[0].Left = 0.8;
        config.Zones[0].Width = 0.3;

        Assert.Throws<InvalidOperationException>(() => _service.Validate(config));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    public void AutoTileZones_AssignsAllZonesWithinBounds(int zoneCount)
    {
        var config = AppConfig.Default;
        config.Zones = Enumerable.Range(0, zoneCount).Select(_ => ZoneConfig.CreateDefault()).ToList();

        _service.AutoTileZones(config);

        Assert.All(config.Zones, zone =>
        {
            Assert.InRange(zone.Left, 0, 1);
            Assert.InRange(zone.Top, 0, 1);
            Assert.InRange(zone.Width, 0.01, 1);
            Assert.InRange(zone.Height, 0.01, 1);
            Assert.True(zone.Left + zone.Width <= 1.0001);
            Assert.True(zone.Top + zone.Height <= 1.0001);
        });
    }

    [Fact]
    public void AutoTileZones_RespectsConfiguredRowAndColumnLimits()
    {
        var config = AppConfig.Default;
        config.MaxZoneRows = 2;
        config.MaxZonesPerRow = 2;
        config.Zones = Enumerable.Range(0, 5).Select(_ => ZoneConfig.CreateDefault()).ToList();

        _service.AutoTileZones(config);

        int distinctRows = config.Zones.Select(z => z.Top).Distinct().Count();
        Assert.True(distinctRows >= 3);
    }
}
