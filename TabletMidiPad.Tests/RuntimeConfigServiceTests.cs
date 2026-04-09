using TabletMidiPad.Services;
using Xunit;
using System.Collections.Generic;

namespace TabletMidiPad.Tests;

public sealed class RuntimeConfigServiceTests
{
    [Fact]
    public void Load_ReplacesCurrentFromStore()
    {
        var expected = new AppConfig { DawPortName = "Loaded" };
        var store = new RecordingConfigStore { LoadedConfig = expected };
        var sut = new RuntimeConfigService(store);

        AppConfig loaded = sut.Load();

        Assert.Same(expected, loaded);
        Assert.Same(expected, sut.Current);
    }

    [Fact]
    public void Replace_NormalizesAndPublishesCurrent()
    {
        var sut = new RuntimeConfigService(new RecordingConfigStore());
        var config = new AppConfig { Zones = new List<ZoneConfig>() };

        sut.Replace(config);

        Assert.Single(sut.Current.Zones);
    }

    [Fact]
    public void SaveCurrent_WritesCurrentConfig()
    {
        var store = new RecordingConfigStore();
        var sut = new RuntimeConfigService(store);
        var config = new AppConfig { DawPortName = "Persisted" };
        sut.Replace(config);

        sut.SaveCurrent();

        Assert.Same(config, store.SavedConfig);
    }

    private sealed class RecordingConfigStore : IConfigStore
    {
        public AppConfig LoadedConfig { get; set; } = AppConfig.Default;

        public AppConfig? SavedConfig { get; private set; }

        public AppConfig Load() => LoadedConfig;

        public void Save(AppConfig config) => SavedConfig = config;
    }
}
