using Xunit;

namespace TabletMidiPad.Tests;

/// <summary>
/// Regression tests for persisted configuration JSON (startup / apply settings path).
/// </summary>
public sealed class JsonConfigStoreTests
{
    [Fact]
    public void RoundTripPreservesCoreFields()
    {
        string path = Path.Combine(Path.GetTempPath(), $"tabletmidipad_test_{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonConfigStore(path);
            var original = new AppConfig
            {
                AppPortName = "App Out",
                DawPortName = "DAW In",
                SendPressureGlobally = false,
                InputBackendMode = "Mouse",
            };
            original.Pages.Clear();
            original.Pages.Add(new PageConfig
            {
                Name = "Page 1",
                MaxZoneRows = 3,
                MaxZonesPerRow = 4,
                Zones = new List<ZoneConfig> { new ZoneConfig { Name = "Test", Width = 0.5, Height = 0.5 } }
            });

            store.Save(original);

            AppConfig loaded = store.Load();

            Assert.Equal("App Out", loaded.AppPortName);
            Assert.Equal("DAW In", loaded.DawPortName);
            Assert.Equal(3, loaded.MaxZoneRows);
            Assert.Equal(4, loaded.MaxZonesPerRow);
            Assert.False(loaded.SendPressureGlobally);
            Assert.Equal("Mouse", loaded.InputBackendMode);
            Assert.Single(loaded.Pages[0].Zones);
            Assert.Equal("Test", loaded.Pages[0].Zones[0].Name);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void LoadMissingFileReturnsDefault()
    {
        string path = Path.Combine(Path.GetTempPath(), $"tabletmidipad_missing_{Guid.NewGuid():N}.json");
        var store = new JsonConfigStore(path);

        AppConfig loaded = store.Load();

        Assert.Equal(AppConfig.Default.AppPortName, loaded.AppPortName);
        Assert.NotEmpty(loaded.Zones);
    }
}
