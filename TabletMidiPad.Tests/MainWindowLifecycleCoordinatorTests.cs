using System;
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

        public bool IsReady => true;

        public void ClearCachedValues() => ClearCachedValuesCalled = true;

        public void Initialize(AppConfig config)
        {
        }

        public async Task InitializeAsync(AppConfig config, Action<string>? onProgress = null)
        {
            InitializeAsyncCalled = true;
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
        }
    }
}
