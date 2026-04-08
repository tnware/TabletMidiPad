using System;
using System.Collections.Generic;
using System.Windows;
using TabletMidiPad.Services;
using TabletMidiPad.ViewModels;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class MainWindowSettingsSessionTests
{
    [Fact]
    public void ApplySettings_WhenPersistFails_SetsErrorAndSkipsUiRefresh()
    {
        var host = new RecordingSettingsHost();
        var workflow = new StubWorkflowCoordinator { ApplySettingsAndPersistResult = new WorkflowSaveResult(false, "bad") };
        var sut = CreateSession(host, workflow);

        sut.ApplySettings();

        Assert.Equal(1, host.CommitCurrentZoneEditsCount);
        Assert.Equal(0, host.UpdateUiForCurrentConfigCount);
        Assert.Equal(0, host.RefreshPadZoneOverlayCount);
        Assert.Single(host.SettingsResults);
        Assert.Equal(SettingsResultKind.Error, host.SettingsResults[0].Kind);
        Assert.Equal("bad", host.SettingsResults[0].Message);
    }

    [Fact]
    public void ApplySettings_WhenPersistSucceeds_RefreshesUiAndSuccessMessage()
    {
        var host = new RecordingSettingsHost();
        var workflow = new StubWorkflowCoordinator { ApplySettingsAndPersistResult = new WorkflowSaveResult(true, null) };
        var sut = CreateSession(host, workflow);

        sut.ApplySettings();

        Assert.Equal(1, host.CommitCurrentZoneEditsCount);
        Assert.Equal(1, host.UpdateUiForCurrentConfigCount);
        Assert.Equal(1, host.RefreshPadZoneOverlayCount);
        Assert.Single(host.SettingsResults);
        Assert.Equal(SettingsResultKind.Success, host.SettingsResults[0].Kind);
        Assert.Equal("Settings applied and saved.", host.SettingsResults[0].Message);
    }

    [Fact]
    public void ApplySettings_OnException_SetsErrorFromMessage()
    {
        var host = new RecordingSettingsHost();
        var workflow = new ThrowingWorkflowCoordinator();
        var sut = CreateSession(host, workflow);

        sut.ApplySettings();

        Assert.Single(host.SettingsResults);
        Assert.Equal(SettingsResultKind.Error, host.SettingsResults[0].Kind);
        Assert.Equal("boom", host.SettingsResults[0].Message);
    }

    [Fact]
    public void ResetToDefaultsAndApply_ReplacesConfigAutoTilesLoadsAndApplies()
    {
        var host = new RecordingSettingsHost();
        var workflow = new StubWorkflowCoordinator { ApplySettingsAndPersistResult = new WorkflowSaveResult(true, null) };
        var zones = new RecordingZoneConfigurationService();
        var sut = CreateSession(host, workflow, zones);

        sut.ResetToDefaultsAndApply();

        Assert.Equal(1, host.ReplaceConfigCount);
        Assert.Equal(1, zones.AutoTileZonesCount);
        Assert.Equal(1, host.LoadConfigIntoUiCount);
        Assert.Equal(1, host.CommitCurrentZoneEditsCount);
        Assert.Equal(1, host.UpdateUiForCurrentConfigCount);
    }

    private static MainWindowSettingsSession CreateSession(
        RecordingSettingsHost host,
        IMainWindowWorkflowCoordinator workflow,
        RecordingZoneConfigurationService? zones = null)
    {
        return new MainWindowSettingsSession(
            host,
            workflow,
            new StubPreferencesDialogService(),
            zones ?? new RecordingZoneConfigurationService(),
            new MemoryConfigStore(),
            new StubMidiEngine(),
            new StubPadInputSession());
    }

    private sealed class RecordingSettingsHost : IMainWindowSettingsSessionHost
    {
        public Window OwnerWindow { get; } = null!;

        public AppConfig Config { get; private set; } = AppConfig.Default;

        public PadInputSessionState PadState { get; } = new();

        public int CommitCurrentZoneEditsCount { get; private set; }

        public int ReplaceConfigCount { get; private set; }

        public int LoadConfigIntoUiCount { get; private set; }

        public int UpdateUiForCurrentConfigCount { get; private set; }

        public int RefreshPadZoneOverlayCount { get; private set; }

        public int SyncToolbarCount { get; private set; }

        public List<(string Message, SettingsResultKind Kind)> SettingsResults { get; } = new();

        public void ReplaceConfig(AppConfig config)
        {
            ReplaceConfigCount++;
            Config = config;
        }

        public void CommitCurrentZoneEdits() => CommitCurrentZoneEditsCount++;

        public void SetSettingsResult(string message, SettingsResultKind kind) => SettingsResults.Add((message, kind));

        public void UpdateUiForCurrentConfig() => UpdateUiForCurrentConfigCount++;

        public void RefreshPadZoneOverlay() => RefreshPadZoneOverlayCount++;

        public void SyncToolbar() => SyncToolbarCount++;

        public void LoadConfigIntoUi(AppConfig config)
        {
            LoadConfigIntoUiCount++;
            Config = config;
        }
    }

    private sealed class StubWorkflowCoordinator : IMainWindowWorkflowCoordinator
    {
        public WorkflowSaveResult ApplySettingsAndPersistResult { get; set; } = new(true, null);

        public WorkflowSaveResult PersistConfigToDiskResult { get; set; } = new(true, null);

        public PreferencesWorkflowResult ApplyPreferencesResult { get; set; } = new(true, null, false, false);

        public int GetEffectiveExpressKeyCount(int detectedExpressKeyCount, int configuredExpressKeyCount) => 0;

        public WorkflowSaveResult ValidateAndSave(AppConfig config) => new(true, null);

        public WorkflowSaveResult ApplySettingsAndPersist(AppConfig config, IConfigStore store) => ApplySettingsAndPersistResult;

        public WorkflowSaveResult PersistConfigToDisk(AppConfig config, IConfigStore store) => PersistConfigToDiskResult;

        public PreferencesWorkflowResult ApplyPreferences(AppConfig config, PreferencesDialogResult result, IntPtr midiOutHandle)
            => ApplyPreferencesResult;

        public WacomServiceMenuState BuildWacomServiceMenuState(WacomServiceStatus status) => new(string.Empty, false);

        public MainWindowToolbarState BuildToolbarState(AppConfig config) => new(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            false,
            false,
            false,
            false,
            false,
            false);

        public MainWindowConnectedState BuildConnectedState(AppConfig config) => new(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);
    }

    private sealed class ThrowingWorkflowCoordinator : IMainWindowWorkflowCoordinator
    {
        public int GetEffectiveExpressKeyCount(int detectedExpressKeyCount, int configuredExpressKeyCount) => 0;

        public WorkflowSaveResult ValidateAndSave(AppConfig config) => throw new System.NotImplementedException();

        public WorkflowSaveResult ApplySettingsAndPersist(AppConfig config, IConfigStore store) => throw new System.InvalidOperationException("boom");

        public WorkflowSaveResult PersistConfigToDisk(AppConfig config, IConfigStore store) => throw new System.NotImplementedException();

        public PreferencesWorkflowResult ApplyPreferences(AppConfig config, PreferencesDialogResult result, IntPtr midiOutHandle)
            => throw new NotImplementedException();

        public WacomServiceMenuState BuildWacomServiceMenuState(WacomServiceStatus status) => throw new System.NotImplementedException();

        public MainWindowToolbarState BuildToolbarState(AppConfig config) => throw new System.NotImplementedException();

        public MainWindowConnectedState BuildConnectedState(AppConfig config) => throw new System.NotImplementedException();
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

        public System.Threading.Tasks.Task InitializeAsync(AppConfig config, System.Action<string>? onProgress = null)
            => System.Threading.Tasks.Task.CompletedTask;

        public void Restart(AppConfig config)
        {
        }

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

    private sealed class StubPadInputSession : IMainWindowPadInputSession
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
