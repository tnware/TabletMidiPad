using Xunit;

namespace TabletMidiPad.Tests;

public sealed class ZoneActionWorkflowCoordinatorTests
{
    [Fact]
    public void AutoLayoutUsesConfigurationServiceAndKeepsSelectionInRange()
    {
        var config = new AppConfig
        {
            Pages = [new PageConfig { Name = "Page 1", Zones = [new(), new()] }],
        };
        var service = new StubZoneConfigurationService();
        var pageNavigation = new PageNavigationService();
        pageNavigation.Initialize(config);
        var coordinator = new ZoneActionWorkflowCoordinator(service, pageNavigation);

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
            Pages = [new PageConfig { Name = "Page 1", Zones = [new()] }],
        };
        var service = new StubZoneConfigurationService { NextCcStart = 40 };
        var pageNavigation = new PageNavigationService();
        pageNavigation.Initialize(config);
        var coordinator = new ZoneActionWorkflowCoordinator(service, pageNavigation);

        ZoneActionWorkflowResult result = coordinator.AddZone(config);

        Assert.Equal(2, config.Pages[0].Zones.Count);
        ZoneConfig added = config.Pages[0].Zones[1];
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
            Pages = [new PageConfig { Name = "Page 1", Zones = [new()] }],
        };
        var pageNavigation = new PageNavigationService();
        pageNavigation.Initialize(config);
        var coordinator = new ZoneActionWorkflowCoordinator(new StubZoneConfigurationService(), pageNavigation);

        ZoneActionWorkflowResult result = coordinator.RemoveZone(config, selectedZoneIndex: -1);

        Assert.False(result.Changed);
        Assert.Equal(-1, result.SelectedZoneIndex);
        Assert.Single(config.Pages[0].Zones);
    }

    [Fact]
    public void RemoveZoneLastZoneReplacesWithDefault()
    {
        var config = new AppConfig
        {
            Pages =
            [
                new PageConfig
                {
                    Name = "Page 1",
                    Zones = [new() { Name = "Only" }],
                },
            ],
        };
        var service = new StubZoneConfigurationService();
        var pageNavigation = new PageNavigationService();
        pageNavigation.Initialize(config);
        var coordinator = new ZoneActionWorkflowCoordinator(service, pageNavigation);

        ZoneActionWorkflowResult result = coordinator.RemoveZone(config, selectedZoneIndex: 0);

        Assert.True(result.Changed);
        Assert.Single(config.Pages[0].Zones);
        Assert.Equal("Zone 1", config.Pages[0].Zones[0].Name);
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

        public void AutoTileZones(PageConfig page)
        {
            AutoTileCalled = true;
        }

        public int GetNextAvailableCcStart(PageConfig page) => NextCcStart;
    }
}
