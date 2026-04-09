using System.Windows;
using TabletMidiPad.ViewModels;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class SettingsWorkflowTests
{
    [Fact]
    public void ApplySettingsWhenPersistFailsSetsErrorAndSkipsUiRefresh()
    {
        var host = new RecordingSettingsHost();
        var workflow = new StubSettingsWorkflowCoordinator { ValidateAndSaveResult = new WorkflowSaveResult(false, "bad") };
        SettingsWorkflow sut = CreateController(host, workflow);

        sut.ApplySettings();

        Assert.Equal(1, host.CommitCurrentZoneEditsCount);
        Assert.Equal(0, host.UpdateUiForCurrentConfigCount);
        Assert.Equal(0, host.RefreshPadZoneOverlayCount);
        Assert.Single(host.SettingsResults);
        Assert.Equal(SettingsResultKind.Error, host.SettingsResults[0].Kind);
        Assert.Equal("bad", host.SettingsResults[0].Message);
    }

    [Fact]
    public void ApplySettingsWhenPersistSucceedsRefreshesUiAndSuccessMessage()
    {
        var host = new RecordingSettingsHost();
        var workflow = new StubSettingsWorkflowCoordinator { ValidateAndSaveResult = new WorkflowSaveResult(true, null) };
        SettingsWorkflow sut = CreateController(host, workflow);

        sut.ApplySettings();

        Assert.Equal(1, host.CommitCurrentZoneEditsCount);
        Assert.Equal(1, host.UpdateUiForCurrentConfigCount);
        Assert.Equal(1, host.RefreshPadZoneOverlayCount);
        Assert.Single(host.SettingsResults);
        Assert.Equal(SettingsResultKind.Success, host.SettingsResults[0].Kind);
        Assert.Equal("Settings applied and saved.", host.SettingsResults[0].Message);
    }

    [Fact]
    public void ApplySettingsOnExceptionSetsErrorFromMessage()
    {
        var host = new RecordingSettingsHost();
        var workflow = new ThrowingSettingsWorkflowCoordinator();
        SettingsWorkflow sut = CreateController(host, workflow);

        sut.ApplySettings();

        Assert.Single(host.SettingsResults);
        Assert.Equal(SettingsResultKind.Error, host.SettingsResults[0].Kind);
        Assert.Equal("boom", host.SettingsResults[0].Message);
    }

    [Fact]
    public void ResetToDefaultsAndApplyReplacesConfigAutoTilesLoadsAndApplies()
    {
        var host = new RecordingSettingsHost();
        var workflow = new StubSettingsWorkflowCoordinator { ValidateAndSaveResult = new WorkflowSaveResult(true, null) };
        var zones = new RecordingZoneConfigurationService();
        SettingsWorkflow sut = CreateController(host, workflow, zones);

        sut.ResetToDefaultsAndApply();

        Assert.Equal(1, host.ReplaceConfigCount);
        Assert.Equal(1, zones.AutoTileZonesCount);
        Assert.Equal(1, host.LoadConfigIntoUiCount);
        Assert.Equal(1, host.CommitCurrentZoneEditsCount);
        Assert.Equal(1, host.UpdateUiForCurrentConfigCount);
    }

    private static SettingsWorkflow CreateController(
        RecordingSettingsHost host,
        ISettingsWorkflowCoordinator workflow,
        RecordingZoneConfigurationService? zones = null)
    {
        var runtime = new RuntimeConfigService(new MemoryConfigStore());
        runtime.Replace(host.Config);
        return new SettingsWorkflow(
            host,
            workflow,
            new StubPreferencesDialogService(),
            zones ?? new RecordingZoneConfigurationService(),
            runtime,
            new StubMidiEngine(),
            new StubPadInputController());
    }

    private sealed class RecordingSettingsHost : ISettingsWorkflowHost
    {
        public Window OwnerWindow { get; } = null!;

        public AppConfig Config { get; private set; } = AppConfig.Default;

        public PadInputControllerState PadState { get; } = new();

        public int CommitCurrentZoneEditsCount { get; private set; }

        public int ReplaceConfigCount { get; private set; }

        public int LoadConfigIntoUiCount { get; private set; }

        public int UpdateUiForCurrentConfigCount { get; private set; }

        public int RefreshPadZoneOverlayCount { get; private set; }

        public int SyncToolbarCount { get; private set; }

        public List<(string Message, SettingsResultKind Kind)> SettingsResults { get; } = [];

        public void CommitCurrentZoneEdits() => CommitCurrentZoneEditsCount++;

        public void SetSettingsResult(string message, SettingsResultKind kind) => SettingsResults.Add((message, kind));

        public void UpdateUiForCurrentConfig() => UpdateUiForCurrentConfigCount++;

        public void RefreshPadZoneOverlay() => RefreshPadZoneOverlayCount++;

        public void SyncToolbar() => SyncToolbarCount++;

        public void LoadConfigIntoUi()
        {
            LoadConfigIntoUiCount++;
            ReplaceConfigCount++;
        }

        public void SetMidiReconnectStatus(bool panelVisible, bool progressVisible, MidiStatusSeverity severity, string statusText)
        {
        }
    }

    private sealed class StubSettingsWorkflowCoordinator : ISettingsWorkflowCoordinator
    {
        public WorkflowSaveResult ValidateAndSaveResult { get; set; } = new(true, null);

        public PreferencesWorkflowResult ApplyPreferencesResult { get; set; } = new(true, null, false, false);

        public int GetEffectiveExpressKeyCount(int detectedExpressKeyCount, int configuredExpressKeyCount) => 0;

        public WorkflowSaveResult ValidateAndSave(AppConfig config) => ValidateAndSaveResult;

        public PreferencesWorkflowResult ApplyPreferences(AppConfig config, PreferencesDialogResult result, IntPtr midiOutHandle)
            => ApplyPreferencesResult;

    }

    private sealed class ThrowingSettingsWorkflowCoordinator : ISettingsWorkflowCoordinator
    {
        public int GetEffectiveExpressKeyCount(int detectedExpressKeyCount, int configuredExpressKeyCount) => 0;

        public WorkflowSaveResult ValidateAndSave(AppConfig config) => throw new InvalidOperationException("boom");

        public PreferencesWorkflowResult ApplyPreferences(AppConfig config, PreferencesDialogResult result, IntPtr midiOutHandle)
            => throw new NotImplementedException();
    }

    private sealed class StubPreferencesDialogService : IPreferencesDialogService
    {
        public PreferencesDialogResult? ShowDialog(Window owner, AppConfig config, int expressKeyCount) => null;
    }

    private sealed class RecordingZoneConfigurationService : IZoneConfigurationService
    {
        public int AutoTileZonesCount { get; private set; }

        public void ValidateConfig(AppConfig config)
        {
        }

        public void AutoTileZones(AppConfig config) => AutoTileZonesCount++;

        public int GetNextAvailableCcStart(AppConfig config) => 0;
    }

    private sealed class MemoryConfigStore : IConfigStore
    {
        public AppConfig Load() => AppConfig.Default;

        public void Save(AppConfig config)
        {
        }
    }

    private sealed class StubMidiEngine : IMidiEngine
    {
        public bool IsReady => false;

        public void Initialize(AppConfig config)
        {
        }

        public Task InitializeAsync(AppConfig config, Action<string>? onProgress = null)
            => Task.CompletedTask;

        public void Restart(AppConfig config)
        {
        }

        public Task RestartAsync(AppConfig config, Action<string>? onProgress = null) => Task.CompletedTask;

        public Task RecreateLoopbackPairAsync(AppConfig config, Action<string>? onProgress = null) => Task.CompletedTask;

        public Task RemoveLoopbackPairAsync(AppConfig config, Action<string>? onProgress = null) => Task.CompletedTask;

        public void Shutdown(AppConfig config)
        {
        }

        public void SendCcIfChanged(int channel, int cc, int value, bool force = false)
        {
        }

        public void SendNoteIfChanged(int channel, int note, bool on, int velocity, bool force = false)
        {
        }

        public void ClearCachedValues()
        {
        }
    }

    private sealed class StubPadInputController : IPadInputController
    {
        public void RebindInputBackend()
        {
        }

        public void ResetActiveInputState()
        {
        }

        public void ShutdownTabletInput()
        {
        }

        public void InitializeTabletInput()
        {
        }

        public void RefreshTabletDeviceListOnly()
        {
        }

        public void ShutdownWintabBackendOnly()
        {
        }

        public void ApplyTabletLifecycle(InputBackendLifecycleResult lifecycle, bool refreshInputBackendStatusText)
        {
        }

        public void HandleWintabPacket(WintabPacketEventArgs packet)
        {
        }

        public void HandleExpressKeyPacket(WintabExpressKeyEventArgs expressKey)
        {
        }

        public void HandleProximityChanged(WintabProximityEventArgs e)
        {
        }

        public void OnWintabDiagnosticsChanged(string diagnostics)
        {
        }

        public void HandleStylusDown(System.Windows.Input.StylusDownEventArgs e)
        {
        }

        public void HandleStylusMove(System.Windows.Input.StylusEventArgs e)
        {
        }

        public void HandleStylusUp(System.Windows.Input.StylusEventArgs e)
        {
        }

        public void HandlePadMouseDownForInteraction(System.Windows.Input.MouseButtonEventArgs e)
        {
        }

        public void HandlePadMouseMoveForInteraction(System.Windows.Input.MouseEventArgs e)
        {
        }

        public void HandlePadMouseUpForInteraction(System.Windows.Input.MouseButtonEventArgs e)
        {
        }

        public void HandleRelease()
        {
        }
    }
}
