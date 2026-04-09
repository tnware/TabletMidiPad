using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TabletMidiPad.Services;
using TabletMidiPad.ViewModels;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class StartupLifecycleServiceTests
{
    [Fact]
    public void LoadPersistedConfig_LoadsFromRuntimeConfigService()
    {
        var runtime = new RuntimeConfigService(new FixedConfigStore(new AppConfig { DawPortName = "X" }));
        var sut = new StartupLifecycleService(runtime, new InputBackendCoordinator());

        AppConfig loaded = sut.LoadPersistedConfig();

        Assert.Equal("X", loaded.DawPortName);
    }

    [Fact]
    public async Task RunColdStartAsync_ReplacesConfigLoadsUiShellThenMidi_WhenWacomRunning()
    {
        var runtime = new RuntimeConfigService(new FixedConfigStore(new AppConfig { DawPortName = "ColdStart" }));
        var sut = new StartupLifecycleService(runtime, new InputBackendCoordinator());
        var host = new RecordingStartupHost
        {
            StatusToReturn = new WacomServiceStatus("running", CanStart: false, IsServiceRunning: true),
        };
        var vm = new MainViewModel();
        var engine = new RecordingMidiEngine { Succeed = true };
        bool routingReady = false;

        await sut.RunColdStartAsync(host, engine, vm, a => a.Invoke(), () => routingReady = true);

        Assert.Equal("ColdStart", runtime.Current.DawPortName);
        Assert.Equal(
            new[]
            {
                nameof(RecordingStartupHost.LoadConfigIntoUi),
                nameof(RecordingStartupHost.GetWacomServiceStatus),
                nameof(RecordingStartupHost.ApplyWacomServiceMenuState),
                nameof(RecordingStartupHost.InitializeTabletInput),
            },
            host.CallOrder);
        Assert.DoesNotContain(nameof(RecordingStartupHost.ApplyTabletStackBlockedAtStartup), host.CallOrder);
        Assert.True(routingReady);
        Assert.True(engine.InitializeAsyncCalled);
    }

    [Fact]
    public async Task RunColdStartAsync_WhenWacomStoppedAuto_AppliesBlockedNotTabletInit()
    {
        var runtime = new RuntimeConfigService(new FixedConfigStore(new AppConfig { InputBackendMode = "Auto" }));
        var sut = new StartupLifecycleService(runtime, new InputBackendCoordinator());
        var host = new RecordingStartupHost
        {
            StatusToReturn = new WacomServiceStatus("stopped", CanStart: true, IsServiceRunning: false),
        };
        var vm = new MainViewModel();
        var engine = new RecordingMidiEngine { Succeed = true };

        await sut.RunColdStartAsync(host, engine, vm, a => a.Invoke(), () => { });

        Assert.Contains(nameof(RecordingStartupHost.ApplyTabletStackBlockedAtStartup), host.CallOrder);
        Assert.DoesNotContain(nameof(RecordingStartupHost.InitializeTabletInput), host.CallOrder);
    }

    [Fact]
    public async Task RunStartupMidiSequenceAsync_OnSuccess_CallsRoutingReadyAndHidesMidiPanel()
    {
        var sut = new StartupLifecycleService(
            new RuntimeConfigService(new FixedConfigStore(AppConfig.Default)),
            new InputBackendCoordinator());
        var vm = new MainViewModel();
        var engine = new RecordingMidiEngine { Succeed = true };
        bool routingReady = false;

        await sut.RunStartupMidiSequenceAsync(
            engine,
            AppConfig.Default,
            vm,
            a => a.Invoke(),
            () => routingReady = true);

        Assert.True(routingReady);
        Assert.False(vm.IsMidiStatusPanelVisible);
        Assert.True(engine.InitializeAsyncCalled);
    }

    [Fact]
    public async Task RunStartupMidiSequenceAsync_OnFailure_SetsErrorPresentation()
    {
        var sut = new StartupLifecycleService(
            new RuntimeConfigService(new FixedConfigStore(AppConfig.Default)),
            new InputBackendCoordinator());
        var vm = new MainViewModel();
        var engine = new RecordingMidiEngine { Succeed = false, ErrorMessage = "port missing" };

        await sut.RunStartupMidiSequenceAsync(
            engine,
            AppConfig.Default,
            vm,
            a => a.Invoke(),
            () => { });

        Assert.False(vm.IsMidiProgressVisible);
        Assert.Equal(MidiStatusSeverity.Error, vm.MidiStatusSeverity);
        Assert.Equal("MIDI init failed", vm.MidiStatusText);
        Assert.Equal("MIDI init failed", vm.StatusText);
        Assert.Equal("port missing", vm.ValueText);
    }

    [Fact]
    public void ShutdownForExit_ShutsDownMidiTablet_AppliesState_DisposesBackend()
    {
        var inputCoord = new RecordingInputBackendCoordinator();
        var sut = new StartupLifecycleService(
            new RuntimeConfigService(new FixedConfigStore(AppConfig.Default)),
            inputCoord);
        var midi = new RecordingMidiEngine { Succeed = true };
        var wintab = new RecordingWintabBackend();
        InputBackendLifecycleResult? applied = null;
        var config = AppConfig.Default;

        sut.ShutdownForExit(
            midi,
            config,
            wintab,
            r => applied = r);

        Assert.True(midi.ShutdownCalled);
        Assert.Same(wintab, inputCoord.ShutdownCalledWith);
        Assert.True(wintab.DisposeCalled);
        Assert.NotNull(applied);
    }

    [Fact]
    public void ShouldProbeWintabStack_StylusMouseOnly_ProbesEvenWhenServiceStopped()
    {
        var cfg = new AppConfig { InputBackendMode = "Stylus/Mouse Only" };
        var wacom = new WacomServiceStatus("stopped", CanStart: true, IsServiceRunning: false);

        Assert.True(StartupLifecycleService.ShouldProbeWintabStack(cfg, wacom));
    }

    [Fact]
    public void ShouldProbeWintabStack_Auto_RequiresRunningService()
    {
        var cfg = new AppConfig { InputBackendMode = "Auto" };
        var stopped = new WacomServiceStatus("stopped", CanStart: true, IsServiceRunning: false);
        Assert.False(StartupLifecycleService.ShouldProbeWintabStack(cfg, stopped));
        var running = new WacomServiceStatus("running", CanStart: false, IsServiceRunning: true);
        Assert.True(StartupLifecycleService.ShouldProbeWintabStack(cfg, running));
    }

    [Fact]
    public void RunPostConfigLoadShellPhase_WhenServiceStopped_AndAuto_CallsBlockedNotInit()
    {
        var sut = new StartupLifecycleService(
            new RuntimeConfigService(new FixedConfigStore(AppConfig.Default)),
            new InputBackendCoordinator());
        bool init = false;
        bool blocked = false;
        WacomServiceStatus? menuStatus = null;

        sut.RunPostConfigLoadShellPhase(new StartupShellContext(
            GetWacomServiceStatus: () => new WacomServiceStatus("stopped", CanStart: true, IsServiceRunning: false),
            ApplyWacomServiceMenuState: s => menuStatus = s,
            Config: new AppConfig { InputBackendMode = "Auto" },
            InitializeTabletWhenReady: () => init = true,
            ApplyTabletBlockedByWacomService: _ => blocked = true));

        Assert.NotNull(menuStatus);
        Assert.True(blocked);
        Assert.False(init);
    }

    [Fact]
    public void RunPostConfigLoadShellPhase_WhenServiceRunning_CallsInitNotBlocked()
    {
        var sut = new StartupLifecycleService(
            new RuntimeConfigService(new FixedConfigStore(AppConfig.Default)),
            new InputBackendCoordinator());
        bool init = false;
        bool blocked = false;

        sut.RunPostConfigLoadShellPhase(new StartupShellContext(
            GetWacomServiceStatus: () => new WacomServiceStatus("running", CanStart: false, IsServiceRunning: true),
            ApplyWacomServiceMenuState: _ => { },
            Config: new AppConfig { InputBackendMode = "Auto" },
            InitializeTabletWhenReady: () => init = true,
            ApplyTabletBlockedByWacomService: _ => blocked = true));

        Assert.True(init);
        Assert.False(blocked);
    }

    [Fact]
    public void BuildTabletStackBlockedLifecycleResult_WintabOnly_UsesUnavailableLabel()
    {
        var sut = new StartupLifecycleService(
            new RuntimeConfigService(new FixedConfigStore(AppConfig.Default)),
            new InputBackendCoordinator());
        var wacom = new WacomServiceStatus("stopped", CanStart: true, IsServiceRunning: false);
        var cfg = new AppConfig { InputBackendMode = "Wintab Only" };

        InputBackendLifecycleResult r = sut.BuildTabletStackBlockedLifecycleResult(cfg, wacom);

        Assert.False(r.UseWintab);
        Assert.Equal("Wintab unavailable", r.LastInputBackend);
        Assert.Contains("not running", r.WintabDiagnostics, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildTabletStackBlockedLifecycleResult_ServiceNotFound_MentionsInstall()
    {
        var sut = new StartupLifecycleService(
            new RuntimeConfigService(new FixedConfigStore(AppConfig.Default)),
            new InputBackendCoordinator());
        var wacom = new WacomServiceStatus("Service: not found", CanStart: false, IsServiceRunning: false);
        var cfg = new AppConfig { InputBackendMode = "Auto" };

        InputBackendLifecycleResult r = sut.BuildTabletStackBlockedLifecycleResult(cfg, wacom);

        Assert.Equal("Stylus/Mouse Fallback", r.LastInputBackend);
        Assert.Contains("not found", r.WintabDiagnostics, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingStartupHost : IStartupShellHost
    {
        public List<string> CallOrder { get; } = new();

        public WacomServiceStatus StatusToReturn { get; init; } = new("running", CanStart: false, IsServiceRunning: true);

        public void LoadConfigIntoUi()
        {
            CallOrder.Add(nameof(LoadConfigIntoUi));
        }

        public WacomServiceStatus GetWacomServiceStatus()
        {
            CallOrder.Add(nameof(GetWacomServiceStatus));
            return StatusToReturn;
        }

        public void ApplyWacomServiceMenuState(WacomServiceStatus status)
        {
            CallOrder.Add(nameof(ApplyWacomServiceMenuState));
        }

        public void InitializeTabletInput()
        {
            CallOrder.Add(nameof(InitializeTabletInput));
        }

        public void ApplyTabletStackBlockedAtStartup(WacomServiceStatus wacom)
        {
            CallOrder.Add(nameof(ApplyTabletStackBlockedAtStartup));
        }
    }

    private sealed class FixedConfigStore : IConfigStore
    {
        public FixedConfigStore(AppConfig config) => Config = config;

        public AppConfig Config { get; }

        public AppConfig Load() => Config;

        public void Save(AppConfig config)
        {
        }
    }

    private sealed class RecordingMidiEngine : IMidiEngine
    {
        public bool Succeed { get; init; }

        public string ErrorMessage { get; init; } = "err";

        public bool InitializeAsyncCalled { get; private set; }

        public bool ShutdownCalled { get; private set; }

        public bool IsReady => true;

        public void ClearCachedValues()
        {
        }

        public void Initialize(AppConfig config)
        {
        }

        public async Task InitializeAsync(AppConfig config, Action<string>? onProgress = null)
        {
            InitializeAsyncCalled = true;
            onProgress?.Invoke("progress");
            await Task.Yield();
            if (!Succeed)
            {
                throw new InvalidOperationException(ErrorMessage);
            }
        }

        public void Restart(AppConfig config)
        {
        }

        public void SendCcIfChanged(int channel, int cc, int value, bool force = false)
        {
        }

        public void SendNoteIfChanged(int channel, int note, bool on, int velocity, bool force = false)
        {
        }

        public void Shutdown(AppConfig config) => ShutdownCalled = true;
    }

    private sealed class RecordingWintabBackend : IWintabInputBackend
    {
        public bool DisposeCalled { get; private set; }

        public event EventHandler<WintabPacketEventArgs>? PacketReceived;

        public event EventHandler<WintabExpressKeyEventArgs>? ExpressKeyReceived;

        public event EventHandler<WintabProximityEventArgs>? ProximityChanged;

        public event EventHandler<string>? DiagnosticsChanged;

        public bool IsActive => false;

        public int ExpressKeyCount => 0;

        public IReadOnlyDictionary<uint, WintabContextInfo> Contexts { get; } = new Dictionary<uint, WintabContextInfo>();

        public void Initialize(int selectedDeviceIndex)
        {
        }

        public void Reinitialize(int selectedDeviceIndex)
        {
        }

        public void Shutdown()
        {
        }

        public IReadOnlyList<WintabTabletDevice> GetAvailableDevices() => Array.Empty<WintabTabletDevice>();

        public void Dispose() => DisposeCalled = true;
    }

    private sealed class RecordingInputBackendCoordinator : IInputBackendCoordinator
    {
        public IWintabInputBackend? ShutdownCalledWith { get; private set; }

        public InputBackendLifecycleResult Initialize(
            string inputBackendMode,
            IWintabInputBackend backend,
            int selectedDeviceIndex,
            int currentExpressKeyCount)
            => new(true, 0, "x", "d");

        public InputBackendLifecycleResult Shutdown(IWintabInputBackend backend)
        {
            ShutdownCalledWith = backend;
            return new InputBackendLifecycleResult(true, 0, "Idle", "ok");
        }

        public InputBackendResetResult BuildResetState()
            => new(false, "a", "v", "p", "m");

        public bool IsStylusMouseFallbackSuppressed(bool useWintab, string inputBackendMode) => false;

        public WintabPacketFlowResult BuildWintabPacketFlow(bool wasActive, WintabPacketUpdate update)
            => throw new NotImplementedException();

        public bool ShouldReleaseOnProximityLeave(bool isActive) => false;
    }
}
