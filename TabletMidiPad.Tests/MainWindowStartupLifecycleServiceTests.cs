using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TabletMidiPad.Services;
using TabletMidiPad.ViewModels;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class MainWindowStartupLifecycleServiceTests
{
    [Fact]
    public void LoadPersistedConfig_DelegatesToLifecycleCoordinator()
    {
        var inner = new MainWindowLifecycleCoordinator();
        var sut = new MainWindowStartupLifecycleService(inner);
        var store = new FixedConfigStore(new AppConfig { DawPortName = "X" });

        AppConfig loaded = sut.LoadPersistedConfig(store);

        Assert.Equal("X", loaded.DawPortName);
    }

    [Fact]
    public async Task RunStartupMidiSequenceAsync_OnSuccess_CallsRoutingReadyAndHidesMidiPanel()
    {
        var inner = new MainWindowLifecycleCoordinator();
        var sut = new MainWindowStartupLifecycleService(inner);
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
        var inner = new MainWindowLifecycleCoordinator();
        var sut = new MainWindowStartupLifecycleService(inner);
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
        var inner = new MainWindowLifecycleCoordinator();
        var sut = new MainWindowStartupLifecycleService(inner);
        var midi = new RecordingMidiEngine { Succeed = true };
        var wintab = new RecordingWintabBackend();
        var inputCoord = new RecordingInputBackendCoordinator();
        InputBackendLifecycleResult? applied = null;
        var config = AppConfig.Default;

        sut.ShutdownForExit(
            midi,
            config,
            inputCoord,
            wintab,
            r => applied = r);

        Assert.True(midi.ShutdownCalled);
        Assert.Same(wintab, inputCoord.ShutdownCalledWith);
        Assert.True(wintab.DisposeCalled);
        Assert.NotNull(applied);
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

    private sealed class RecordingInputBackendCoordinator : IMainWindowInputBackendCoordinator
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
