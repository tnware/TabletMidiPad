using System;
using System.Collections.Generic;
using TabletMidiPad.Infrastructure;

namespace TabletMidiPad.Services;

public record TabletPacket(uint Buttons, int X, int Y, int NormalPressure, uint Status);
public record TabletContextInfo(int DeviceIndex, int OutputMinX, int OutputMaxX, int OutputMinY, int OutputMaxY, int PressureMax, bool IsIntegrated);
public record TabletDeviceItem(int DeviceIndex, string Label);
public record ExpressKeyPacket(int Control, bool IsPressed);

public interface ITabletService
{
    event Action<TabletPacket, TabletContextInfo> PacketReceived;
    event Action<ExpressKeyPacket> ExpressKeyReceived;
    event Action<bool> ProximityChanged;
    event Action ContextsChanged;

    bool IsWintabEnabled { get; }
    string InputBackendMode { get; set; }

    bool Initialize(IntPtr windowHandle, int? targetDeviceIndex, uint expressKeyMask);
    void Shutdown();
    void Reinitialize();
    TabletDeviceItem[] GetAvailableDevices();
    string GetDiagnostics();
    string GetExpressKeyDiagnostics();
    int GetExpressKeyCount();
    void SetExpressKeyOverrides(uint contextHandle, byte tabletIndex);
    IntPtr HandleWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled);

    void ProcessExternalPacket(TabletPacket packet, TabletContextInfo info);
}
