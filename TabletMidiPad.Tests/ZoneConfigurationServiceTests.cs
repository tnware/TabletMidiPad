using Xunit;

namespace TabletMidiPad.Tests;

public sealed class ZoneConfigurationServiceTests
{
    [Fact]
    public void AutoTileZonesWhenOverCapacityThrows()
    {
        var sut = new ZoneConfigurationService();
        var page = new PageConfig
        {
            Name = "P1",
            MaxZoneRows = 1,
            MaxZonesPerRow = 1,
            Zones = [new ZoneConfig(), new ZoneConfig()],
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => sut.AutoTileZones(page));
        Assert.Contains("only 1 allowed", ex.Message);
    }

    [Fact]
    public void ValidateConfigWhenPageOverCapacityThrows()
    {
        var sut = new ZoneConfigurationService();
        var config = new AppConfig
        {
            Pages =
            [
                new PageConfig
                {
                    Name = "P1",
                    MaxZoneRows = 1,
                    MaxZonesPerRow = 1,
                    Zones =
                    [
                        new ZoneConfig { Name = "A", Left = 0, Top = 0, Width = 1, Height = 1 },
                        new ZoneConfig { Name = "B", Left = 0, Top = 0, Width = 1, Height = 1 },
                    ],
                },
            ],
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => sut.ValidateConfig(config));
        Assert.Contains("only 1 allowed", ex.Message);
    }
}
