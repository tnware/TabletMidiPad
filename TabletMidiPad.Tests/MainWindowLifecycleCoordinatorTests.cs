using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TabletMidiPad.Services;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class MainWindowLifecycleCoordinatorTests
{
    [Fact]
    public void LoadStartupConfig_DelegatesToStore()
    {
        var coordinator = new MainWindowLifecycleCoordinator();
        var store = new FixedConfigStore(AppConfig.Default);

        AppConfig loaded = coordinator.LoadStartupConfig(store);

        Assert.Same(store.Config, loaded);
    }

    [Fact]
    public async Task TryInitializeMidiAsync_OnSuccess_ClearsCache()
    {
        var coordinator = new MainWindowLifecycleCoordinator();
        var engine = new RecordingMidiEngine { Succeed = true };

        MidiInitializationResult result = await coordinator.TryInitializeMidiAsync(engine, AppConfig.Default, _ => { });

        Assert.True(result.Succeeded);
        Assert.True(engine.InitializeAsyncCalled);
        Assert.True(engine.ClearCachedValuesCalled);
    }

    [Fact]
    public async Task TryInitializeMidiAsync_OnFailure_ReturnsMessage()
    {
        var coordinator = new MainWindowLifecycleCoordinator();
        var engine = new RecordingMidiEngine { Succeed = false, ErrorMessage = "boom" };

        MidiInitializationResult result = await coordinator.TryInitializeMidiAsync(engine, AppConfig.Default, _ => { });

        Assert.False(result.Succeeded);
        Assert.Equal("boom", result.ErrorMessage);
        Assert.False(engine.ClearCachedValuesCalled);
    }

    [Fact]
    public async Task TryInitializeMidiAsync_InvokesProgressCallback()
    {
        var coordinator = new MainWindowLifecycleCoordinator();
        var engine = new RecordingMidiEngine { Succeed = true };
        string? last = null;

        await coordinator.TryInitializeMidiAsync(engine, AppConfig.Default, s => last = s);

        Assert.Equal("progress", last);
    }

    [Fact]
    public void ShutdownMidiOutput_CallsEngineShutdown()
    {
        var coordinator = new MainWindowLifecycleCoordinator();
        var engine = new RecordingMidiEngine { Succeed = true };
        var config = AppConfig.Default;

        coordinator.ShutdownMidiOutput(engine, config);

        Assert.True(engine.ShutdownCalled);
        Assert.Same(config, engine.ShutdownConfig);
    }

    [Fact]
    public void ShutdownTabletInput_DelegatesToCoordinator()
    {
        var coordinator = new MainWindowLifecycleCoordinator();
        var inputCoordinator = new RecordingInputBackendCoordinator();
        var wintab = new MinimalWintabBackend();

        InputBackendLifecycleResult result = coordinator.ShutdownTabletInput(inputCoordinator, wintab);

        Assert.Same(wintab, inputCoordinator.ShutdownCalledWith);
        Assert.True(result.UseWintab);
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
    }

    private sealed class MinimalWintabBackend : IWintabInputBackend
    {
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

        public void Dispose()
        {
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

        public bool ClearCachedValuesCalled { get; private set; }

        public bool ShutdownCalled { get; private set; }

        public AppConfig? ShutdownConfig { get; private set; }

        public bool IsReady => true;

        public void ClearCachedValues() => ClearCachedValuesCalled = true;

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

        public void Shutdown(AppConfig config)
        {
            ShutdownCalled = true;
            ShutdownConfig = config;
        }
    }
}
