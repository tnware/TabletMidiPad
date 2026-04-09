using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TabletMidiPad.Core.Interaction;
using TabletMidiPad.Services;
using TabletMidiPad.ViewModels;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class PadInputControllerTests
{
    private static readonly InteractionResult EmptyInteraction = new(
        ActiveZoneIndex: -1,
        ActiveZoneLabel: string.Empty,
        ValueLabel: string.Empty,
        ModifiersLabel: string.Empty,
        MidiMessages: Array.Empty<MidiMessage>());

    [Fact]
    public void RebindInputBackend_ShutsDownWintabBeforeInitialize_AndRefreshesDeviceList()
    {
        RunSta(() =>
        {
            var host = new ControllerTestHost();
            var backend = new SequencedWintabBackend { Active = true };
            var picker = new RecordingTabletDevicePickerSyncService();
            PadInputController sut = CreateController(host, backend, picker);

            sut.RebindInputBackend();

            int shutdownAt = backend.Sequence.IndexOf("Shutdown");
            int initAt = backend.Sequence.IndexOf("Initialize");
            Assert.True(shutdownAt >= 0, "Expected Shutdown to be recorded.");
            Assert.True(initAt >= 0, "Expected Initialize to be recorded.");
            Assert.True(shutdownAt < initAt, "Shutdown must precede Initialize during rebind.");
            Assert.Equal(1, host.RebindRoutingRefreshCount);
            Assert.True(picker.RefreshCallCount >= 1, "Rebind should refresh the tablet device list.");
        });
    }

    [Fact]
    public void InitializeTabletInput_StylusMouseOnly_SetsPadStateUseWintabFalse()
    {
        RunSta(() =>
        {
            var host = new ControllerTestHost();
            host.Config.InputBackendMode = "Stylus/Mouse Only";
            var backend = new SequencedWintabBackend { Active = true };
            PadInputController sut = CreateController(host, backend, new RecordingTabletDevicePickerSyncService());

            sut.InitializeTabletInput();

            Assert.False(host.PadState.UseWintab);
        });
    }

    [Fact]
    public void InitializeTabletInput_AutoWithActiveBackend_SetsPadStateUseWintabTrue()
    {
        RunSta(() =>
        {
            var host = new ControllerTestHost();
            host.Config.InputBackendMode = "Auto";
            var backend = new SequencedWintabBackend { Active = true, ExpressKeyCount = 2 };
            PadInputController sut = CreateController(host, backend, new RecordingTabletDevicePickerSyncService());

            sut.InitializeTabletInput();

            Assert.True(host.PadState.UseWintab);
        });
    }

    [Fact]
    public void ApplyTabletLifecycle_CopiesResultIntoPadState()
    {
        RunSta(() =>
        {
            var host = new ControllerTestHost();
            var backend = new SequencedWintabBackend();
            PadInputController sut = CreateController(host, backend, new RecordingTabletDevicePickerSyncService());
            var lifecycle = new InputBackendLifecycleResult(
                UseWintab: true,
                ExpressKeyCount: 5,
                LastInputBackend: "Wintab",
                WintabDiagnostics: "ok");

            sut.ApplyTabletLifecycle(lifecycle, refreshInputBackendStatusText: false);

            Assert.True(host.PadState.UseWintab);
            Assert.Equal(5, host.PadState.ExpressKeyCount);
            Assert.Equal("Wintab", host.PadState.LastInputBackend);
            Assert.Equal("ok", host.PadState.WintabDiagnostics);
        });
    }

    private static void RunSta(Action action)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            action();
            return;
        }

        Exception? caught = null;
        Thread thread = new(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (caught is not null)
        {
            throw caught;
        }
    }

    private static PadInputController CreateController(
        ControllerTestHost host,
        IWintabInputBackend backend,
        ITabletDevicePickerSyncService tabletPicker)
    {
        return new PadInputController(
            host,
            tabletPicker,
            new InputBackendCoordinator(),
            new LifecycleCoordinator(),
            backend,
            new StubWintabInputBridge(),
            new StubInputFallbackService(),
            new StubInputInteractionOrchestrator(),
            new InputBackendStatusFormatter());
    }

    private sealed class ControllerTestHost : IPadInputControllerHost
    {
        public ControllerTestHost()
        {
            Pad = new Canvas { Width = 256, Height = 256 };
            ViewModel = new MainViewModel();
            Config = AppConfig.Default;
        }

        public Canvas Pad { get; }

        public IPadInputChromeSurface PadChrome { get; } = new FakePadChrome();

        public AppConfig Config { get; }

        public MainViewModel ViewModel { get; }

        public InteractionState InteractionState { get; } = new();

        public IPadInteractionOutcomeApplier OutcomeApplier { get; } = null!;

        public PadInputControllerState PadState { get; } = new();

        public int SelectedZoneIndex { get; set; }

        public bool IsLoadingUi => false;

        public int RebindRoutingRefreshCount { get; private set; }

        public void ApplyInteractionResult(InteractionResult result)
        {
        }

        public void RefreshPadZoneOverlay()
        {
        }

        public void UpdateInputBackendStatus()
        {
        }

        public void OnRebindCompletedRefreshRouting() => RebindRoutingRefreshCount++;

        public void BeginPopulateTabletDeviceList()
        {
        }

        public void EndPopulateTabletDeviceList()
        {
        }
    }

    private sealed class FakePadChrome : IPadInputChromeSurface
    {
        public CursorTickChrome ComputeCursorForInteractionTick(
            InteractionInput input,
            Point pixelPosition,
            double? opacityOverride)
            => new(ShouldApply: false, Visibility.Collapsed, 0, 0, null);

        public CursorTickChrome ComputeCursorAfterRelease()
            => new(ShouldApply: false, Visibility.Collapsed, 0, 0, null);

        public void ApplyCursorChrome(CursorTickChrome chrome)
        {
        }
    }

    private sealed class RecordingTabletDevicePickerSyncService : ITabletDevicePickerSyncService
    {
        public int RefreshCallCount { get; private set; }

        public void Refresh(MainViewModel viewModel, int previouslySelectedDeviceIndex) => RefreshCallCount++;
    }

    private sealed class SequencedWintabBackend : IWintabInputBackend
    {
        public List<string> Sequence { get; } = new();

        public bool Active { get; set; }

        public int ExpressKeyCount { get; set; }

        public event EventHandler<WintabPacketEventArgs>? PacketReceived;

        public event EventHandler<WintabExpressKeyEventArgs>? ExpressKeyReceived;

        public event EventHandler<WintabProximityEventArgs>? ProximityChanged;

        public event EventHandler<string>? DiagnosticsChanged;

        public bool IsActive => Active;

        public IReadOnlyDictionary<uint, WintabContextInfo> Contexts { get; } = new Dictionary<uint, WintabContextInfo>();

        public void Initialize(int selectedDeviceIndex) => Sequence.Add("Initialize");

        public void Reinitialize(int selectedDeviceIndex)
        {
        }

        public void Shutdown() => Sequence.Add("Shutdown");

        public IReadOnlyList<WintabTabletDevice> GetAvailableDevices() => Array.Empty<WintabTabletDevice>();

        public void Dispose()
        {
        }
    }

    private sealed class StubWintabInputBridge : IWintabInputBridge
    {
        public bool TryBuildPacketBridge(
            WintabPacketEventArgs packet,
            double padWidth,
            double padHeight,
            bool isActiveBefore,
            out WintabPacketBridgeOutcome outcome)
        {
            outcome = default;
            return false;
        }

        public WintabExpressKeyBridgeOutcome BuildExpressKeyBridge(
            WintabExpressKeyEventArgs expressKey,
            IReadOnlyList<ExpressKeyConfig> configuredExpressKeys)
            => throw new InvalidOperationException("Not used in these tests.");
    }

    private sealed class StubInputFallbackService : IInputFallbackService
    {
        public bool TryBuildStylusSample(
            StylusEventArgs e,
            IInputElement relativeTo,
            double padWidth,
            double padHeight,
            int mousePressureFallback,
            out InputFallbackSample sample)
        {
            sample = default;
            return false;
        }

        public bool TryBuildMouseSample(
            MouseEventArgs e,
            IInputElement relativeTo,
            double padWidth,
            double padHeight,
            int mousePressureFallback,
            out InputFallbackSample sample)
        {
            sample = default;
            return false;
        }
    }

    private sealed class StubInputInteractionOrchestrator : IInputInteractionOrchestrator
    {
        public PadInteractionConfig BuildPadInteractionConfig(AppConfig config) => default;

        public IReadOnlyList<ZoneSnapshot> BuildZoneSnapshots(AppConfig config) => Array.Empty<ZoneSnapshot>();

        public InteractionResult ProcessTick(
            PadInteractionConfig config,
            IReadOnlyList<ZoneSnapshot> zones,
            InteractionState state,
            InteractionInput input)
            => EmptyInteraction;

        public InteractionResult ProcessRelease(
            PadInteractionConfig config,
            IReadOnlyList<ZoneSnapshot> zones,
            InteractionState state,
            uint lastPenButtons)
            => EmptyInteraction;
    }
}
