using System;
using System.Collections.Generic;
using System.Windows;
using TabletMidiPad.Core.Interaction;
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

    [Theory]
    [InlineData(true, "Auto", true)]
    [InlineData(false, "Wintab Only", true)]
    [InlineData(false, "Auto", false)]
    [InlineData(false, "Wintab", true)]
    public void IsStylusMouseFallbackSuppressed_MatchesLegacyRule(bool useWintab, string mode, bool expected)
    {
        bool actual = _sut.IsStylusMouseFallbackSuppressed(useWintab, mode);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildWintabPacketFlow_HoverWhileActive_ReleasesBeforeTick()
    {
        var update = new WintabPacketUpdate(
            XPixel: 10,
            YPixel: 20,
            XNorm: 0.1,
            YNorm: 0.2,
            Pressure127: 0,
            IsHovering: true,
            PenButtons: 3,
            BackendLabel: "x",
            PacketDiagnostics: "d",
            CursorOpacity: 0.35,
            IsDown: false);

        WintabPacketFlowResult flow = _sut.BuildWintabPacketFlow(wasActive: true, update);

        Assert.True(flow.ReleaseBeforeTickIfWasActive);
        Assert.False(flow.IsActiveAfter);
        Assert.True(flow.TickInput.IsHovering);
        Assert.False(flow.TickInput.IsDown);
        Assert.Equal(0.35, flow.CursorOpacity);
        Assert.Equal(new Point(10, 20), flow.PixelPosition);
        Assert.Equal(InteractionBackend.Wintab, flow.TickInput.Backend);
    }

    [Fact]
    public void BuildWintabPacketFlow_Down_SetsActiveFromUpdate()
    {
        var update = new WintabPacketUpdate(
            XPixel: 0,
            YPixel: 0,
            XNorm: 0.5,
            YNorm: 0.5,
            Pressure127: 80,
            IsHovering: false,
            PenButtons: 0,
            BackendLabel: "x",
            PacketDiagnostics: "d",
            CursorOpacity: 0.85,
            IsDown: true);

        WintabPacketFlowResult flow = _sut.BuildWintabPacketFlow(wasActive: false, update);

        Assert.False(flow.ReleaseBeforeTickIfWasActive);
        Assert.True(flow.IsActiveAfter);
        Assert.True(flow.TickInput.IsDown);
        Assert.False(flow.TickInput.IsHovering);
        Assert.Equal(80, flow.TickInput.Pressure);
    }

    [Fact]
    public void ShouldReleaseOnProximityLeave_OnlyWhenActive()
    {
        Assert.True(_sut.ShouldReleaseOnProximityLeave(true));
        Assert.False(_sut.ShouldReleaseOnProximityLeave(false));
    }

    [Theory]
    [InlineData("Wintab", "Wintab Only")]
    [InlineData("Mouse", "Stylus/Mouse Only")]
    [InlineData("garbage", "Auto")]
    public void InputBackendModeNormalizer_Normalize_AlignsWithLegacy(string raw, string expected)
    {
        Assert.Equal(expected, InputBackendModeNormalizer.Normalize(raw));
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
