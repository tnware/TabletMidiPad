using TabletMidiPad.Services;
using TabletMidiPad.ViewModels;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class ToolbarViewModelTests
{
    [Fact]
    public void SendPressureGlobally_UpdatesConfigAndRequestsChromeRefresh()
    {
        var runtime = new RuntimeConfigService(new MemoryStore());
        runtime.Replace(AppConfig.Default);
        var sut = new ToolbarViewModel(runtime, new ToolbarConfigService(), new WorkflowCoordinator(new ZoneConfigurationService()));
        int chromeRefresh = 0;
        sut.PressureChromeRefreshRequested += () => chromeRefresh++;

        sut.SendPressureGlobally = false;

        Assert.False(runtime.Current.SendPressureGlobally);
        Assert.Equal(1, chromeRefresh);
    }

    [Fact]
    public void SelectInputBackend_RebindsWhenModeChanges()
    {
        var runtime = new RuntimeConfigService(new MemoryStore());
        runtime.Replace(AppConfig.Default);
        var sut = new ToolbarViewModel(runtime, new ToolbarConfigService(), new WorkflowCoordinator(new ZoneConfigurationService()));
        int rebinds = 0;
        sut.InputBackendRebindRequested += () => rebinds++;

        sut.SelectInputBackendWintabCommand.Execute(null);

        Assert.Equal("Wintab Only", runtime.Current.InputBackendMode);
        Assert.Equal(1, rebinds);
    }

    private sealed class MemoryStore : IConfigStore
    {
        public AppConfig Load() => AppConfig.Default;

        public void Save(AppConfig config)
        {
        }
    }
}
