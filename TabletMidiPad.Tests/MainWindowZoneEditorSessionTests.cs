using System;
using System.Collections.ObjectModel;
using TabletMidiPad.Services;
using TabletMidiPad.ViewModels;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class MainWindowZoneEditorSessionTests
{
    [Fact]
    public void OnZoneListSelectionChanged_WhenLoadingUi_DoesNotLoadEditorOrRefreshOverlay()
    {
        var host = new RecordingZoneEditorHost { IsLoadingUi = true };
        var editor = new RecordingZoneEditorService();
        var coordinator = new StubWorkflowCoordinator();
        var sut = new MainWindowZoneEditorSession(host, editor, coordinator);

        sut.OnZoneListSelectionChanged();

        Assert.Equal(0, editor.LoadSelectedZoneIntoEditorCount);
        Assert.Equal(0, host.RefreshPadZoneOverlayCount);
    }

    [Fact]
    public void OnZoneListSelectionChanged_WhenNotLoading_LoadsEditorAndRefreshesOverlay()
    {
        var host = new RecordingZoneEditorHost();
        var editor = new RecordingZoneEditorService();
        var coordinator = new StubWorkflowCoordinator();
        var sut = new MainWindowZoneEditorSession(host, editor, coordinator);

        sut.OnZoneListSelectionChanged();

        Assert.Equal(1, editor.LoadSelectedZoneIntoEditorCount);
        Assert.Equal(1, host.RefreshPadZoneOverlayCount);
    }

    [Fact]
    public void LoadConfigIntoUi_SetsGridTextsSelectionAndToolbar_AndClearsLoadingFlag()
    {
        var vm = CreateTestMainViewModel();
        var config = AppConfig.Default;
        config.MaxZoneRows = 7;
        config.MaxZonesPerRow = 5;
        config.Zones.Add(new ZoneConfig { Name = "A" });

        var host = new RecordingZoneEditorHost { ViewModel = vm, Config = config };
        var editor = new RecordingZoneEditorService();
        var coordinator = new StubWorkflowCoordinator();
        var sut = new MainWindowZoneEditorSession(host, editor, coordinator);

        sut.LoadConfigIntoUi(config);

        Assert.False(host.IsLoadingUi);
        Assert.Equal("7", vm.MaxZoneRowsText);
        Assert.Equal("5", vm.MaxZonesPerRowText);
        Assert.Equal(0, host.SelectedZoneIndex);
        Assert.Equal(1, editor.RefreshZoneListCount);
        Assert.Equal(1, editor.LoadSelectedZoneIntoEditorCount);
        Assert.Equal(1, host.SyncToolbarCount);
        Assert.Equal(1, host.RefreshPadZoneOverlayCount);
    }

    [Fact]
    public void UpdateUiForCurrentConfig_AppliesConnectedStateThenInputBackendRefreshOrder()
    {
        var vm = CreateTestMainViewModel();
        var host = new RecordingZoneEditorHost { ViewModel = vm, Config = AppConfig.Default };
        var editor = new RecordingZoneEditorService();
        var coordinator = new DistinctStringsWorkflowCoordinator();
        var sut = new MainWindowZoneEditorSession(host, editor, coordinator);

        sut.UpdateUiForCurrentConfig();

        Assert.Equal("S", vm.StatusText);
        Assert.Equal("R", vm.RoutingText);
        Assert.Equal(1, host.RefreshInputBackendStatusCount);
        Assert.Equal("A", vm.ActiveZoneText);
        Assert.Equal("C", vm.ConfigSummaryText);
        Assert.Equal("V", vm.ValueText);
        Assert.Equal("P", vm.PenButtonsText);
        Assert.Equal("M", vm.ModifiersText);
    }

    [Fact]
    public void CommitCurrentZoneEdits_DelegatesToZoneEditorService_AndRefreshesList()
    {
        var vm = CreateTestMainViewModel();
        var host = new RecordingZoneEditorHost { ViewModel = vm, Config = AppConfig.Default, SelectedZoneIndex = 2 };
        var editor = new RecordingZoneEditorService();
        var coordinator = new StubWorkflowCoordinator();
        var sut = new MainWindowZoneEditorSession(host, editor, coordinator);

        sut.CommitCurrentZoneEdits();

        Assert.Equal(1, editor.CommitCurrentZoneEditsCount);
        Assert.Equal(2, editor.LastCommitSelectedIndex);
        Assert.Equal(1, editor.RefreshZoneListCount);
    }

    private static MainViewModel CreateTestMainViewModel()
    {
        var settingsStore = new StubConfigStore();
        var settingsService = new SettingsService(settingsStore);
        var zoneActionCoordinator = new StubZoneActionWorkflowCoordinator();
        var editorService = new RecordingZoneEditorService();
        var zoneList = new ZoneListViewModel(settingsService, zoneActionCoordinator, editorService);
        var zoneEditor = new ZoneEditorViewModel(settingsService, editorService, zoneList);
        return new MainViewModel(
            zoneList,
            zoneEditor,
            settingsService,
            new StubMainWindowSettingsSession(),
            new StubMainWindowToolbarSession(),
            new StubMainWindowPadInputSession());
    }

    private sealed class RecordingZoneEditorHost : IMainWindowZoneEditorSessionHost
    {
        public AppConfig Config { get; set; } = AppConfig.Default;

        public MainViewModel ViewModel { get; set; } = CreateTestMainViewModel();

        public int SelectedZoneIndex { get; set; } = -1;

        public bool IsLoadingUi { get; set; }

        public int SyncToolbarCount { get; private set; }

        public int RefreshPadZoneOverlayCount { get; private set; }

        public int RefreshInputBackendStatusCount { get; private set; }

        public void SyncToolbar() => SyncToolbarCount++;

        public void RefreshPadZoneOverlay() => RefreshPadZoneOverlayCount++;

        public void RefreshInputBackendStatus() => RefreshInputBackendStatusCount++;
    }

    private sealed class RecordingZoneEditorService : IZoneEditorService
    {
        public int RefreshZoneListCount { get; private set; }

        public int LoadSelectedZoneIntoEditorCount { get; private set; }

        public int CommitCurrentZoneEditsCount { get; private set; }

        public int LastCommitSelectedIndex { get; private set; }

        public void RefreshZoneList(ObservableCollection<ZoneListItemViewModel> zones, AppConfig config)
            => RefreshZoneListCount++;

        public void LoadSelectedZoneIntoEditor(IZoneEditorState state, AppConfig config, int selectedZoneIndex)
            => LoadSelectedZoneIntoEditorCount++;

        public void CommitCurrentZoneEdits(
            AppConfig config,
            IZoneEditorState state,
            int selectedZoneIndex,
            string? maxZoneRowsText,
            string? maxZonesPerRowText)
        {
            CommitCurrentZoneEditsCount++;
            LastCommitSelectedIndex = selectedZoneIndex;
        }

        public void SetZoneSelectionSummary(IZoneEditorState state, ZoneConfig? zone)
        {
        }
    }

    private sealed class StubWorkflowCoordinator : IMainWindowWorkflowCoordinator
    {
        public int GetEffectiveExpressKeyCount(int detectedExpressKeyCount, int configuredExpressKeyCount) => 0;
        public WorkflowSaveResult ValidateAndSave(AppConfig config) => new(true, null);
        public WorkflowSaveResult ApplySettingsAndPersist(AppConfig config, IConfigStore store) => new(true, null);
        public WorkflowSaveResult PersistConfigToDisk(AppConfig config, IConfigStore store) => new(true, null);
        public PreferencesWorkflowResult ApplyPreferences(AppConfig config, PreferencesDialogResult result, IntPtr midiOutHandle)
            => new(true, null, false, false);
        public WacomServiceMenuState BuildWacomServiceMenuState(WacomServiceStatus status) => new(string.Empty, false);
        public MainWindowToolbarState BuildToolbarState(AppConfig config) => new(
            string.Empty, string.Empty, string.Empty, string.Empty,
            false, false, false, false, false, false);
        public MainWindowConnectedState BuildConnectedState(AppConfig config) => new(
            string.Empty, string.Empty, string.Empty, string.Empty,
            string.Empty, string.Empty, string.Empty);
    }

    private sealed class DistinctStringsWorkflowCoordinator : IMainWindowWorkflowCoordinator
    {
        public int GetEffectiveExpressKeyCount(int detectedExpressKeyCount, int configuredExpressKeyCount) => 0;
        public WorkflowSaveResult ValidateAndSave(AppConfig config) => new(true, null);
        public WorkflowSaveResult ApplySettingsAndPersist(AppConfig config, IConfigStore store) => new(true, null);
        public WorkflowSaveResult PersistConfigToDisk(AppConfig config, IConfigStore store) => new(true, null);
        public PreferencesWorkflowResult ApplyPreferences(AppConfig config, PreferencesDialogResult result, IntPtr midiOutHandle)
            => new(true, null, false, false);
        public WacomServiceMenuState BuildWacomServiceMenuState(WacomServiceStatus status) => new(string.Empty, false);
        public MainWindowToolbarState BuildToolbarState(AppConfig config) => new(
            string.Empty, string.Empty, string.Empty, string.Empty,
            false, false, false, false, false, false);
        public MainWindowConnectedState BuildConnectedState(AppConfig config)
            => new("S", "R", "A", "C", "V", "P", "M");
    }

    private sealed class StubConfigStore : IConfigStore
    {
        public AppConfig Load() => AppConfig.Default;
        public void Save(AppConfig config) { }
    }

    private sealed class StubZoneActionWorkflowCoordinator : IZoneActionWorkflowCoordinator
    {
        public ZoneActionWorkflowResult AutoLayout(AppConfig config, int selectedZoneIndex) => new(false, -1, string.Empty);
        public ZoneActionWorkflowResult AddZone(AppConfig config) => new(false, -1, string.Empty);
        public ZoneActionWorkflowResult RemoveZone(AppConfig config, int selectedZoneIndex) => new(false, -1, string.Empty);
    }

    private sealed class StubMainWindowSettingsSession : IMainWindowSettingsSession
    {
        public void ApplySettings() { }
        public void RunPreferencesFlow() { }
        public void ResetToDefaultsAndApply() { }
    }

    private sealed class StubMainWindowToolbarSession : IMainWindowToolbarSession
    {
        public void ApplyPressureCurve(AppConfig config, string curve) { }
        public ToolbarInputModeApplyResult TryApplyInputBackendMode(AppConfig config, string mode) => new(false, false);
        public string GetNextCycledInputBackendModeLabel(AppConfig config) => string.Empty;
    }

    private sealed class StubMainWindowPadInputSession : IMainWindowPadInputSession
    {
        public void RebindInputBackend() { }
        public void ResetActiveInputState() { }
        public void ShutdownTabletInput() { }
        public void InitializeTabletInput() { }
        public void RefreshTabletDeviceListOnly() { }
        public void ShutdownWintabBackendOnly() { }
        public void ApplyTabletLifecycle(InputBackendLifecycleResult lifecycle, bool refreshInputBackendStatusText) { }
        public void HandleWintabPacket(WintabPacketEventArgs packet) { }
        public void HandleExpressKeyPacket(WintabExpressKeyEventArgs expressKey) { }
        public void HandleProximityChanged(WintabProximityEventArgs e) { }
        public void OnWintabDiagnosticsChanged(string diagnostics) { }
        public void HandleStylusDown(System.Windows.Input.StylusDownEventArgs e) { }
        public void HandleStylusMove(System.Windows.Input.StylusEventArgs e) { }
        public void HandleStylusUp(System.Windows.Input.StylusEventArgs e) { }
        public void HandlePadMouseDownForInteraction(System.Windows.Input.MouseButtonEventArgs e) { }
        public void HandlePadMouseMoveForInteraction(System.Windows.Input.MouseEventArgs e) { }
        public void HandlePadMouseUpForInteraction(System.Windows.Input.MouseButtonEventArgs e) { }
        public void HandleRelease() { }
    }
}
