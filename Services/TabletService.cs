using System;
using System.Collections.Generic;
using System.Linq;
using TabletMidiPad.Infrastructure;

namespace TabletMidiPad.Services;

public sealed class TabletService : ITabletService
{
    public event Action<TabletPacket, TabletContextInfo>? PacketReceived;
    public event Action<ExpressKeyPacket>? ExpressKeyReceived;
    public event Action<bool>? ProximityChanged;
    public event Action? ContextsChanged;

    private readonly Dictionary<uint, TabletContextInfo> _wintabContexts = new();
    private IntPtr _windowHandle;
    private int? _targetDeviceIndex;
    private uint _expressKeyMask;
    private string _diagnostics = "Not initialized";
    private string _expressKeyDiagnostics = "";
    private int _expressKeyCount;

    private int _wintabPacketMessage;
    private int _wintabPacketExtMessage;
    private int _wintabContextOverlapMessage;
    private int _wintabProximityMessage;
    private int _wintabInfoChangeMessage;

    public bool IsWintabEnabled => _wintabContexts.Count > 0;
    public string InputBackendMode { get; set; } = "Auto";

    public bool Initialize(IntPtr windowHandle, int? targetDeviceIndex, uint expressKeyMask)
    {
        _windowHandle = windowHandle;
        _targetDeviceIndex = targetDeviceIndex;
        _expressKeyMask = expressKeyMask;

        if (InputBackendMode == "Stylus/Mouse Only") {
            _diagnostics = "Manual stylus/mouse mode selected";
            return false;
        }

        if (!WintabInterop.IsAvailable())
        {
            _diagnostics = "Wintab32.dll not available";
            return false;
        }

        try
        {
            int opened = OpenContexts();
            return opened > 0;
        }
        catch (Exception ex)
        {
            _diagnostics = ex.Message;
            return false;
        }
    }

    public void Shutdown()
    {
        foreach (uint contextHandle in _wintabContexts.Keys.ToArray())
        {
            try { WintabInterop.WTClose(contextHandle); } catch { }
        }
        _wintabContexts.Clear();
        _diagnostics = "Shut down";
    }

    public void Reinitialize()
    {
        Shutdown();
        Initialize(_windowHandle, _targetDeviceIndex, _expressKeyMask);
    }

    public TabletDeviceItem[] GetAvailableDevices()
    {
        if (!WintabInterop.IsAvailable()) return Array.Empty<TabletDeviceItem>();

        int deviceCount = WintabInterop.GetAttachedDeviceCount();
        var devices = new List<TabletDeviceItem>();
        for (int i = 0; i < deviceCount; i++)
        {
            string name = WintabInterop.GetDeviceName(i);
            bool integrated = WintabInterop.IsIntegratedTablet(i);
            string label = integrated ? $"{name} (built-in)" : name;
            devices.Add(new TabletDeviceItem(i, label));
        }
        return devices.ToArray();
    }

    public string GetDiagnostics() => _diagnostics;
    public string GetExpressKeyDiagnostics() => _expressKeyDiagnostics;
    public int GetExpressKeyCount() => _expressKeyCount;

    private int OpenContexts()
    {
        _wintabContexts.Clear();
        int deviceIndex = _targetDeviceIndex ?? -1;
        if (deviceIndex < 0)
        {
            try
            {
                var defaultCtx = WintabInterop.GetDefaultDigitizingContext();
                deviceIndex = (int)defaultCtx.Device;
            }
            catch { deviceIndex = 0; }
        }

        return OpenContextForDevice(deviceIndex);
    }

    private int OpenContextForDevice(int deviceIndex)
    {
        WintabInterop.LogContext context;
        try { context = WintabInterop.GetDigitizerContext(deviceIndex); }
        catch (Exception ex) { _diagnostics = $"WTI_DDCTXS {deviceIndex} failed: {ex.Message}"; return 0; }

        var xAxis = WintabInterop.GetXAxis(deviceIndex);
        var yAxis = WintabInterop.GetYAxis(deviceIndex);
        var pressureAxis = WintabInterop.GetPressureAxis(deviceIndex);

        if (xAxis.Max <= xAxis.Min || yAxis.Max <= yAxis.Min)
        {
            _diagnostics = $"Device {deviceIndex} axes invalid";
            return 0;
        }

        string name = WintabInterop.GetDeviceName(deviceIndex);
        context.Name = $"TabletMidiPad {name}";
        context.Options |= WintabInterop.CXO_MESSAGES;
        context.Options &= ~WintabInterop.CXO_SYSTEM;
        context.Locks = WintabInterop.CXL_INSIZE | WintabInterop.CXL_INASPECT | WintabInterop.CXL_SENSITIVITY | WintabInterop.CXL_MARGIN | WintabInterop.CXL_SYSOUT;
        context.PktData = WintabInterop.PK_STATUS | WintabInterop.PK_X | WintabInterop.PK_Y | WintabInterop.PK_NORMAL_PRESSURE | WintabInterop.PK_BUTTONS;
        if (_expressKeyMask > 0) context.PktData |= _expressKeyMask;
        context.PktMode = 0;
        context.MoveMask = context.PktData;
        context.BtnUpMask = context.BtnDnMask;
        context.OutOrgX = 0;
        context.OutOrgY = 0;
        context.OutExtX = xAxis.Max - xAxis.Min + 1;
        context.OutExtY = -(yAxis.Max - yAxis.Min + 1);
        context.SysMode = false;

        _wintabPacketMessage = unchecked((int)context.MsgBase + WintabInterop.WT_PACKET);
        _wintabPacketExtMessage = unchecked((int)context.MsgBase + WintabInterop.WT_PACKETEXT);
        _wintabContextOverlapMessage = unchecked((int)context.MsgBase + WintabInterop.WT_CTXOVERLAP);
        _wintabProximityMessage = unchecked((int)context.MsgBase + WintabInterop.WT_PROXIMITY);
        _wintabInfoChangeMessage = unchecked((int)context.MsgBase + WintabInterop.WT_INFOCHANGE);

        uint handle = WintabInterop.WTOpenA(_windowHandle, ref context, true);
        if (handle == 0)
        {
            _diagnostics = $"WTOpenA failed for {name}";
            return 0;
        }

        SetExpressKeyOverrides(handle, (byte)deviceIndex);
        _diagnostics = $"{name} opened";
        _wintabContexts[handle] = new TabletContextInfo(
            deviceIndex, 0, Math.Max(1, context.OutExtX - 1),
            0, Math.Max(1, Math.Abs(context.OutExtY) - 1),
            Math.Max(1, pressureAxis.Max),
            WintabInterop.IsIntegratedTablet(deviceIndex)
        );

        return _wintabContexts.Count;
    }

    public void SetExpressKeyOverrides(uint contextHandle, byte tabletIndex)
    {
        if (_expressKeyMask == 0) { _expressKeyDiagnostics = "EK: mask=0"; return; }
        uint extTag = WintabInterop.WTX_EXPKEYS2;
        if (!WintabInterop.GetExtensionProperty(contextHandle, extTag, tabletIndex, 0, 0, WintabInterop.TABLET_PROPERTY_CONTROLCOUNT, out uint numControls)) return;
        if (numControls == 0) return;

        int overrideCount = 0;
        for (byte controlIndex = 0; controlIndex < numControls; controlIndex++)
        {
            if (!WintabInterop.GetExtensionProperty(contextHandle, extTag, tabletIndex, controlIndex, 0, WintabInterop.TABLET_PROPERTY_FUNCCOUNT, out uint numFuncs)) continue;
            for (byte funcIndex = 0; funcIndex < numFuncs; funcIndex++)
            {
                WintabInterop.GetExtensionProperty(contextHandle, extTag, tabletIndex, controlIndex, funcIndex, WintabInterop.TABLET_PROPERTY_AVAILABLE, out uint available);
                if (available == 0) continue;
                if (WintabInterop.SetExtensionProperty(contextHandle, extTag, tabletIndex, controlIndex, funcIndex, WintabInterop.TABLET_PROPERTY_OVERRIDE, 1))
                {
                    WintabInterop.SetExtensionPropertyString(contextHandle, extTag, tabletIndex, controlIndex, funcIndex, WintabInterop.TABLET_PROPERTY_OVERRIDE_NAME, $"MIDI EK{controlIndex}");
                    overrideCount++;
                }
            }
        }
        _expressKeyCount = (int)numControls;
        _expressKeyDiagnostics = $"EK: {numControls} keys, {overrideCount} overridden";
    }

    public IntPtr HandleWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == _wintabPacketMessage)
        {
            uint packetId = unchecked((uint)wParam.ToInt32());
            uint context = unchecked((uint)lParam.ToInt32());
            if (_wintabContexts.TryGetValue(context, out var info) && WintabInterop.WTPacket(context, packetId, out var packet))
            {
                PacketReceived?.Invoke(new TabletPacket(packet.Buttons, packet.X, packet.Y, (int)packet.NormalPressure, packet.Status), info);
                handled = true;
            }
        }
        else if (msg == _wintabPacketExtMessage)
        {
            uint packetId = unchecked((uint)wParam.ToInt32());
            uint context = unchecked((uint)lParam.ToInt32());
            if (WintabInterop.GetPacketExt(context, packetId, out var pktExt))
            {
                ExpressKeyReceived?.Invoke(new ExpressKeyPacket(pktExt.ExpKey.Control, pktExt.ExpKey.State != 0));
                handled = true;
            }
        }
        else if (msg == _wintabContextOverlapMessage)
        {
            uint context = unchecked((uint)wParam.ToInt32());
            if (_wintabContexts.ContainsKey(context))
            {
                WintabInterop.WTOverlap(context, true);
                WintabInterop.WTEnable(context, true);
                handled = true;
            }
        }
        else if (msg == _wintabProximityMessage)
        {
            bool entering = unchecked((uint)lParam.ToInt32()) != 0;
            ProximityChanged?.Invoke(entering);
            handled = true;
        }
        else if (msg == _wintabInfoChangeMessage)
        {
            Reinitialize();
            ContextsChanged?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void ProcessExternalPacket(TabletPacket packet, TabletContextInfo info)
    {
        PacketReceived?.Invoke(packet, info);
    }
}
