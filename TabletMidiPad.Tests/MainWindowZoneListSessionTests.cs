using System.Collections.Generic;
using TabletMidiPad.Services;
using TabletMidiPad.ViewModels;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class MainWindowZoneListSessionTests
{
    [Fact]
    public void AddZone_CommitsThenAppliesCoordinatorResult()
    {
        var host = new RecordingZoneListHost { SelectedZoneIndex = 0 };
        var coordinator = new RecordingZoneCoordinator
        {
            AddResult = new ZoneActionWorkflowResult(true, 2, "added"),
        };
        var sut = new MainWindowZoneListSession(host, coordinator);

        sut.AddZone();

        Assert.Equal(1, host.CommitCount);
        Assert.Equal(1, coordinator.AddZoneCallCount);
        Assert.Equal(2, host.SelectedZoneIndex);
        Assert.Equal(1, host.RefreshZoneListCount);
        Assert.Equal(1, host.NotifySelectionCount);
        Assert.Equal(1, host.LoadEditorCount);
        Assert.Equal(1, host.RefreshOverlayCount);
        Assert.Single(host.SettingsResults);
        Assert.Equal("added", host.SettingsResults[0].Message);
        Assert.Equal(SettingsResultKind.Success, host.SettingsResults[0].Kind);
    }

    [Fact]
    public void RemoveZone_WhenUnchanged_DoesNotTouchUi()
    {
        var host = new RecordingZoneListHost();
        var coordinator = new RecordingZoneCoordinator
        {
            RemoveResult = new ZoneActionWorkflowResult(false, -1, "noop"),
        };
        var sut = new MainWindowZoneListSession(host, coordinator);

        sut.RemoveZone();

        Assert.Equal(0, host.CommitCount);
        Assert.Empty(host.SettingsResults);
        Assert.Equal(0, host.RefreshZoneListCount);
    }

    [Fact]
    public void AutoLayout_PassesSelectedIndexToCoordinator()
    {
        var host = new RecordingZoneListHost { SelectedZoneIndex = 3 };
        var coordinator = new RecordingZoneCoordinator
        {
            AutoLayoutResult = new ZoneActionWorkflowResult(true, 1, "laid out"),
        };
        var sut = new MainWindowZoneListSession(host, coordinator);

        sut.AutoLayoutZones();

        Assert.Equal(1, coordinator.AutoLayoutCalls.Count);
        Assert.Equal(3, coordinator.AutoLayoutCalls[0].SelectedIndex);
        Assert.Equal(1, host.CommitCount);
    }

    private sealed class RecordingZoneListHost : IMainWindowZoneListSessionHost
    {
        public AppConfig Config { get; } = new();

        public int SelectedZoneIndex { get; set; }

        public int CommitCount { get; private set; }

        public int RefreshZoneListCount { get; private set; }

        public int NotifySelectionCount { get; private set; }

        public int LoadEditorCount { get; private set; }

        public int RefreshOverlayCount { get; private set; }

        public List<(string Message, SettingsResultKind Kind)> SettingsResults { get; } = new();

        public void CommitCurrentZoneEdits() => CommitCount++;

        public void RefreshZoneList() => RefreshZoneListCount++;

        public void NotifySelectedZoneIndexChanged() => NotifySelectionCount++;

        public void LoadSelectedZoneIntoEditor() => LoadEditorCount++;

        public void RefreshPadZoneOverlay() => RefreshOverlayCount++;

        public void SetSettingsResult(string message, SettingsResultKind kind) => SettingsResults.Add((message, kind));
    }

    private sealed class RecordingZoneCoordinator : IZoneActionWorkflowCoordinator
    {
        public List<(AppConfig Config, int SelectedIndex)> AutoLayoutCalls { get; } = new();

        public int AddZoneCallCount { get; private set; }

        public ZoneActionWorkflowResult AutoLayoutResult { get; init; } = new(true, 0, "a");

        public ZoneActionWorkflowResult AddResult { get; init; } = new(true, 0, "a");

        public ZoneActionWorkflowResult RemoveResult { get; init; } = new(true, 0, "a");

        public ZoneActionWorkflowResult AutoLayout(AppConfig config, int selectedZoneIndex)
        {
            AutoLayoutCalls.Add((config, selectedZoneIndex));
            return AutoLayoutResult;
        }

        public ZoneActionWorkflowResult AddZone(AppConfig config)
        {
            AddZoneCallCount++;
            return AddResult;
        }

        public ZoneActionWorkflowResult RemoveZone(AppConfig config, int selectedZoneIndex) => RemoveResult;
    }
}
