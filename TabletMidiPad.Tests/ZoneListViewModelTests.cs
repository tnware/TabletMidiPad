using TabletMidiPad.ViewModels;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class ZoneListViewModelTests
{
    [Fact]
    public void AddZoneCommitsEditorRefreshesSelectionAndPublishesEffects()
    {
        var runtime = new RuntimeConfigService(new MemoryStore());
        runtime.Replace(AppConfig.Default);
        var pageNavigation = new PageNavigationService();
        var editorService = new ZoneEditorService(new ConfigValueParser(), pageNavigation);
        var editor = new ZoneEditorViewModel(runtime, editorService, pageNavigation);
        editor.LoadConfigIntoUi();
        editor.ZoneName = "Edited";

        var coordinator = new RecordingZoneCoordinator
        {
            AddResult = new ZoneActionWorkflowResult(true, 1, "added"),
        };
        var sut = new ZoneListViewModel(runtime, coordinator, pageNavigation, editor);
        string? status = null;
        int refreshCount = 0;
        sut.SettingsResultRequested += (message, _) => status = message;
        sut.PadZoneOverlayRefreshRequested += () => refreshCount++;

        sut.AddZoneCommand.Execute(null);

        Assert.Equal("Edited", runtime.Current.Pages[0].Zones[0].Name);
        Assert.Equal(1, editor.SelectedZoneIndex);
        Assert.Equal("added", status);
        Assert.Equal(1, refreshCount);
        Assert.Equal(2, editor.Zones.Count);
    }

    private sealed class MemoryStore : IConfigStore
    {
        public AppConfig Load() => AppConfig.Default;

        public void Save(AppConfig config)
        {
        }
    }

    private sealed class RecordingZoneCoordinator : IZoneActionWorkflowCoordinator
    {
        public ZoneActionWorkflowResult AutoLayoutResult { get; init; } = new(false, 0, string.Empty);

        public ZoneActionWorkflowResult AddResult { get; init; } = new(false, 0, string.Empty);

        public ZoneActionWorkflowResult RemoveResult { get; init; } = new(false, 0, string.Empty);

        public ZoneActionWorkflowResult AutoLayout(AppConfig config, int selectedZoneIndex) => AutoLayoutResult;

        public ZoneActionWorkflowResult AddZone(AppConfig config)
        {
            config.Pages[0].Zones.Add(new ZoneConfig { Name = "Zone 2" });
            return AddResult;
        }

        public ZoneActionWorkflowResult RemoveZone(AppConfig config, int selectedZoneIndex) => RemoveResult;
    }
}
