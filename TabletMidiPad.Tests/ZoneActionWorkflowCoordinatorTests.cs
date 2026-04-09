using Xunit;

namespace TabletMidiPad.Tests;

public sealed class ZoneActionWorkflowCoordinatorTests
{
    [Fact]
    public void AutoLayoutUsesConfigurationServiceAndKeepsSelectionInRange()
    {
        var config = new AppConfig
        {
            Zones =
            [
                new(),
                new(),
            ],
        };
        var service = new StubZoneConfigurationService();
        var coordinator = new ZoneActionWorkflowCoordinator(service);

        ZoneActionWorkflowResult result = coordinator.AutoLayout(config, selectedZoneIndex: 99);

        Assert.True(service.AutoTileCalled);
        Assert.True(result.Changed);
        Assert.Equal(1, result.SelectedZoneIndex);
        Assert.Equal("Zones re-tiled across the pad. Click Apply Settings to save.", result.StatusMessage);
    }

    [Fact]
    public void AddZoneAddsConfiguredZoneAndSelectsNewZone()
    {
        var config = new AppConfig
        {
            Zones = [new()],
        };
        var service = new StubZoneConfigurationService { NextCcStart = 40 };
        var coordinator = new ZoneActionWorkflowCoordinator(service);

        ZoneActionWorkflowResult result = coordinator.AddZone(config);

        Assert.Equal(2, config.Zones.Count);
        ZoneConfig added = config.Zones[1];
        Assert.Equal("Zone 2", added.Name);
        Assert.Equal(40, added.CcX);
        Assert.Equal(41, added.CcY);
        Assert.Equal(42, added.CcPressure);
        Assert.True(service.AutoTileCalled);
        Assert.Equal(1, result.SelectedZoneIndex);
    }

    [Fact]
    public void RemoveZoneInvalidSelectionReturnsUnchanged()
    {
        var config = new AppConfig
        {
            Zones = [new()],
        };
        var coordinator = new ZoneActionWorkflowCoordinator(new StubZoneConfigurationService());

        ZoneActionWorkflowResult result = coordinator.RemoveZone(config, selectedZoneIndex: -1);

        Assert.False(result.Changed);
        Assert.Equal(-1, result.SelectedZoneIndex);
        Assert.Single(config.Zones);
    }

    [Fact]
    public void RemoveZoneLastZoneReplacesWithDefault()
    {
        var config = new AppConfig
        {
            Zones =
            [
                new()
                {
                    Name = "Only",
                },
            ],
        };
        var service = new StubZoneConfigurationService();
        var coordinator = new ZoneActionWorkflowCoordinator(service);

        ZoneActionWorkflowResult result = coordinator.RemoveZone(config, selectedZoneIndex: 0);

        Assert.True(result.Changed);
        Assert.Single(config.Zones);
        Assert.Equal("Zone 1", config.Zones[0].Name);
        Assert.True(service.AutoTileCalled);
        Assert.Equal(0, result.SelectedZoneIndex);
    }

    private sealed class StubZoneConfigurationService : IZoneConfigurationService
    {
        public bool AutoTileCalled { get; private set; }

        public int NextCcStart { get; init; } = 16;

        public void ValidateConfig(AppConfig config)
        {
        }

        public void AutoTileZones(AppConfig config)
        {
            AutoTileCalled = true;
        }

        public int GetNextAvailableCcStart(AppConfig config) => NextCcStart;
    }
}
