using System;
using System.Collections.Generic;
using TabletMidiPad.Services;
using TabletMidiPad.ViewModels;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class TabletDevicePickerSyncServiceTests
{
    [Fact]
    public void Constructor_NullWintab_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new TabletDevicePickerSyncService(null!));
    }

    [Fact]
    public void Refresh_NullViewModel_Throws()
    {
        var sut = new TabletDevicePickerSyncService(new EmptyDevicesWintab());

        Assert.Throws<ArgumentNullException>(() => sut.Refresh(null!, 0));
    }

    [Fact]
    public void Refresh_NoDevices_AddsPlaceholderAndDisablesPicker()
    {
        var sut = new TabletDevicePickerSyncService(new EmptyDevicesWintab());
        var vm = new MainViewModel();

        sut.Refresh(vm, previouslySelectedDeviceIndex: 2);

        Assert.Single(vm.TabletDevices);
        Assert.Equal(-1, vm.TabletDevices[0].DeviceIndex);
        Assert.Equal("No tablets", vm.TabletDevices[0].Label);
        Assert.Same(vm.TabletDevices[0], vm.SelectedTabletDevice);
        Assert.False(vm.IsTabletDevicePickerEnabled);
    }

    [Fact]
    public void Refresh_WithDevices_SelectsFirstWhenNoPriorMatch()
    {
        var wintab = new FixedDevicesWintab(
            new WintabTabletDevice(0, "A"),
            new WintabTabletDevice(1, "B"));
        var sut = new TabletDevicePickerSyncService(wintab);
        var vm = new MainViewModel();

        sut.Refresh(vm, previouslySelectedDeviceIndex: -1);

        Assert.Equal(2, vm.TabletDevices.Count);
        Assert.True(vm.IsTabletDevicePickerEnabled);
        Assert.Equal(0, vm.SelectedTabletDevice!.DeviceIndex);
    }

    [Fact]
    public void Refresh_WithDevices_ReSelectsMatchingDeviceIndex()
    {
        var wintab = new FixedDevicesWintab(
            new WintabTabletDevice(0, "A"),
            new WintabTabletDevice(5, "B"));
        var sut = new TabletDevicePickerSyncService(wintab);
        var vm = new MainViewModel();

        sut.Refresh(vm, previouslySelectedDeviceIndex: 5);

        Assert.Equal(5, vm.SelectedTabletDevice!.DeviceIndex);
        Assert.Equal("B", vm.SelectedTabletDevice.Label);
    }

    private sealed class EmptyDevicesWintab : IWintabInputBackend
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

    private sealed class FixedDevicesWintab : IWintabInputBackend
    {
        private readonly WintabTabletDevice[] _devices;

        public FixedDevicesWintab(params WintabTabletDevice[] devices) => _devices = devices;

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

        public IReadOnlyList<WintabTabletDevice> GetAvailableDevices() => _devices;

        public void Dispose()
        {
        }
    }
}
