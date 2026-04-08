using System;
using System.Collections.Generic;
using TabletMidiPad.Services;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class MainWindowInputBackendCoordinatorTests
{
    private readonly MainWindowInputBackendCoordinator _sut = new();

    [Fact]
    public void Initialize_StylusMouseOnly_ShutsDownBackendWithoutInitializingWintab()
    {
        var backend = new FakeWintabBackend { Active = true };

        InputBackendLifecycleResult result = _sut.Initialize(
            "Stylus/Mouse Only",
            backend,
            selectedDeviceIndex: 2,
            currentExpressKeyCount: 5);

        Assert.Equal(0, backend.InitializeCallCount);
        Assert.True(backend.ShutdownCallCount >= 1);
        Assert.False(result.UseWintab);
        Assert.Equal(0, result.ExpressKeyCount);
        Assert.Equal("Stylus/Mouse Fallback", result.LastInputBackend);
    }

    [Fact]
    public void Initialize_WintabPath_CallsInitializeWithDeviceIndex()
    {
        var backend = new FakeWintabBackend { Active = true, ExpressKeyCount = 4 };

        InputBackendLifecycleResult result = _sut.Initialize(
            "Auto",
            backend,
            selectedDeviceIndex: 3,
            currentExpressKeyCount: 2);

        Assert.Equal(1, backend.InitializeCallCount);
        Assert.Equal(3, backend.LastSelectedDeviceIndex);
        Assert.True(result.UseWintab);
        Assert.Equal(4, result.ExpressKeyCount);
        Assert.Equal("Wintab", result.LastInputBackend);
    }

    [Fact]
    public void Initialize_ExpressKeyCount_UsesMaxOfCurrentAndBackend()
    {
        var backend = new FakeWintabBackend { Active = true, ExpressKeyCount = 3 };

        InputBackendLifecycleResult highCurrent = _sut.Initialize("Auto", backend, 0, currentExpressKeyCount: 8);
        Assert.Equal(8, highCurrent.ExpressKeyCount);

        var backend2 = new FakeWintabBackend { Active = true, ExpressKeyCount = 9 };
        InputBackendLifecycleResult highBackend = _sut.Initialize("Auto", backend2, 0, currentExpressKeyCount: 2);
        Assert.Equal(9, highBackend.ExpressKeyCount);
    }

    [Fact]
    public void Initialize_WintabOnlyWhenInactive_ReportsUnavailable()
    {
        var backend = new FakeWintabBackend { Active = false };

        InputBackendLifecycleResult result = _sut.Initialize(
            "Wintab Only",
            backend,
            selectedDeviceIndex: 0,
            currentExpressKeyCount: 0);

        Assert.Equal(1, backend.InitializeCallCount);
        Assert.False(result.UseWintab);
        Assert.Equal("Wintab unavailable", result.LastInputBackend);
    }

    [Fact]
    public void Initialize_AutoWhenInactive_FallsBackToStylusMouseLabel()
    {
        var backend = new FakeWintabBackend { Active = false };

        InputBackendLifecycleResult result = _sut.Initialize(
            "Auto",
            backend,
            selectedDeviceIndex: 0,
            currentExpressKeyCount: 0);

        Assert.False(result.UseWintab);
        Assert.Equal("Stylus/Mouse Fallback", result.LastInputBackend);
    }

    [Fact]
    public void Shutdown_CallsBackendShutdown_AndReturnsFallbackState()
    {
        var backend = new FakeWintabBackend();

        InputBackendLifecycleResult result = _sut.Shutdown(backend);

        Assert.Equal(1, backend.ShutdownCallCount);
        Assert.False(result.UseWintab);
        Assert.Contains("shut down", result.WintabDiagnostics, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildResetState_ReturnsClearedInteractionLabels()
    {
        InputBackendResetResult reset = _sut.BuildResetState();

        Assert.False(reset.IsActive);
        Assert.Contains("none", reset.ActiveZoneText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("X: -, Y: -, Pressure: -", reset.ValueText);
    }

    private sealed class FakeWintabBackend : IWintabInputBackend
    {
        public int InitializeCallCount { get; private set; }

        public int ShutdownCallCount { get; private set; }

        public int LastSelectedDeviceIndex { get; private set; } = int.MinValue;

        public bool Active { get; set; }

        public int ExpressKeyCount { get; set; }

        public event EventHandler<WintabPacketEventArgs>? PacketReceived;

        public event EventHandler<WintabExpressKeyEventArgs>? ExpressKeyReceived;

        public event EventHandler<WintabProximityEventArgs>? ProximityChanged;

        public event EventHandler<string>? DiagnosticsChanged;

        public bool IsActive => Active;

        public IReadOnlyDictionary<uint, WintabContextInfo> Contexts { get; } = new Dictionary<uint, WintabContextInfo>();

        public void Initialize(int selectedDeviceIndex)
        {
            InitializeCallCount++;
            LastSelectedDeviceIndex = selectedDeviceIndex;
        }

        public void Reinitialize(int selectedDeviceIndex)
        {
        }

        public void Shutdown()
        {
            ShutdownCallCount++;
        }

        public IReadOnlyList<WintabTabletDevice> GetAvailableDevices() => Array.Empty<WintabTabletDevice>();

        public void Dispose()
        {
        }
    }
}
