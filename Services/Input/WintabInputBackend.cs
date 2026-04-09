using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;

namespace TabletMidiPad.Services;

/// <summary>
/// Wintab backend implementation that owns context/message lifecycle and exposes packet events.
/// Initialization outcomes and failures are mirrored to <see cref="DiagnosticsChanged"/> for the status bar and to <see cref="IAppTrace"/> for developer diagnostics (e.g. Debug output).
/// </summary>
internal sealed class WintabInputBackend : IWintabInputBackend
{
    private readonly IAppTrace _trace;

    private HwndSource? _messageSource;
    private readonly Dictionary<uint, WintabContextInfo> _contexts = new();
    private int _packetMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_PACKET;
    private int _packetExtMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_PACKETEXT;
    private int _contextOverlapMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_CTXOVERLAP;
    private int _proximityMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_PROXIMITY;
    private int _infoChangeMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_INFOCHANGE;
    private int _selectedDeviceIndex = -1;
    private uint _expressKeysMask;
    private int _expressKeyCount;

    public WintabInputBackend()
        : this(NullAppTrace.Instance)
    {
    }

    internal WintabInputBackend(IAppTrace trace)
    {
        _trace = trace ?? throw new ArgumentNullException(nameof(trace));
    }

    public event EventHandler<WintabPacketEventArgs>? PacketReceived;
    public event EventHandler<WintabExpressKeyEventArgs>? ExpressKeyReceived;
    public event EventHandler<WintabProximityEventArgs>? ProximityChanged;
    public event EventHandler<string>? DiagnosticsChanged;

    public bool IsActive => _contexts.Count > 0;
    public IReadOnlyDictionary<uint, WintabContextInfo> Contexts => _contexts;
    public int ExpressKeyCount => _expressKeyCount;

    public IReadOnlyList<WintabTabletDevice> GetAvailableDevices()
    {
        var devices = new List<WintabTabletDevice>();
        if (!WintabInterop.IsAvailable())
        {
            return devices;
        }

        int deviceCount = WintabInterop.GetAttachedDeviceCount();
        for (int i = 0; i < deviceCount; i++)
        {
            string name = WintabInterop.GetDeviceName(i);
            bool integrated = WintabInterop.IsIntegratedTablet(i);
            string label = integrated ? $"{name} (built-in)" : name;
            devices.Add(new WintabTabletDevice(i, label));
        }

        return devices;
    }

    public void Initialize(int selectedDeviceIndex)
    {
        Shutdown();
        _selectedDeviceIndex = selectedDeviceIndex;

        if (!WintabInterop.IsAvailable())
        {
            _trace.Warn("WintabInputBackend", "Wintab32.dll not available");
            DiagnosticsChanged?.Invoke(this, "Wintab32.dll not available");
            return;
        }

        try
        {
            _messageSource = CreateMessageWindow();
            _messageSource.AddHook(WndProc);
            int opened = OpenContexts();
            string status = opened > 0 ? $"Opened {opened} Wintab context(s)" : "No Wintab contexts opened";
            if (opened == 0)
            {
                _trace.Warn("WintabInputBackend", status);
            }
            else
            {
                _trace.Info("WintabInputBackend", status);
            }

            DiagnosticsChanged?.Invoke(this, status);
        }
        catch (Exception ex)
        {
            _trace.Warn("WintabInputBackend", ex.ToString());
            DiagnosticsChanged?.Invoke(this, ex.Message);
            Shutdown();
        }
    }

    public void Reinitialize(int selectedDeviceIndex)
    {
        _selectedDeviceIndex = selectedDeviceIndex;
        if (_messageSource is null)
        {
            return;
        }

        foreach (uint contextHandle in _contexts.Keys.ToArray())
        {
            try
            {
                WintabInterop.WTClose(contextHandle);
            }
            catch
            {
            }
        }

        _ = OpenContexts();
    }

    public void Shutdown()
    {
        if (_messageSource is not null)
        {
            _messageSource.RemoveHook(WndProc);
            _messageSource.Dispose();
            _messageSource = null;
        }

        foreach (uint contextHandle in _contexts.Keys.ToArray())
        {
            try
            {
                WintabInterop.WTClose(contextHandle);
            }
            catch
            {
            }
        }

        _contexts.Clear();
        _packetMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_PACKET;
        _packetExtMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_PACKETEXT;
        _contextOverlapMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_CTXOVERLAP;
        _proximityMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_PROXIMITY;
        _infoChangeMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_INFOCHANGE;
        _expressKeysMask = 0;
        _expressKeyCount = 0;
    }

    public void Dispose()
    {
        Shutdown();
    }

    private static HwndSource CreateMessageWindow()
    {
        var parameters = new HwndSourceParameters("TabletMidiPadWintabSink")
        {
            Width = 1,
            Height = 1,
            WindowStyle = unchecked((int)0x80000000),
            ExtendedWindowStyle = 0x08000000,
        };

        return new HwndSource(parameters);
    }

    private int OpenContexts()
    {
        _contexts.Clear();
        _expressKeysMask = WintabInterop.GetExtensionMask(WintabInterop.WTX_EXPKEYS2);

        int deviceIndex = _selectedDeviceIndex;
        if (deviceIndex < 0)
        {
            try
            {
                var defaultCtx = WintabInterop.GetDefaultDigitizingContext();
                deviceIndex = (int)defaultCtx.Device;
            }
            catch
            {
                deviceIndex = 0;
            }
        }

        return OpenContextForDevice(deviceIndex);
    }

    private int OpenContextForDevice(int deviceIndex)
    {
        WintabInterop.LogContext context;
        try
        {
            context = WintabInterop.GetDigitizerContext(deviceIndex);
        }
        catch (Exception ex)
        {
            DiagnosticsChanged?.Invoke(this, $"WTI_DDCTXS {deviceIndex} failed: {ex.Message}");
            return 0;
        }

        var xAxis = WintabInterop.GetXAxis(deviceIndex);
        var yAxis = WintabInterop.GetYAxis(deviceIndex);
        var pressureAxis = WintabInterop.GetPressureAxis(deviceIndex);

        if (xAxis.Max <= xAxis.Min || yAxis.Max <= yAxis.Min)
        {
            DiagnosticsChanged?.Invoke(this, $"Device {deviceIndex} axes invalid: X {xAxis.Min}-{xAxis.Max}, Y {yAxis.Min}-{yAxis.Max}");
            return 0;
        }

        string name = WintabInterop.GetDeviceName(deviceIndex);
        context.Name = $"TabletMidiPad {name}";
        context.Options |= WintabInterop.CXO_MESSAGES;
        context.Options &= ~WintabInterop.CXO_SYSTEM;
        context.Locks =
            WintabInterop.CXL_INSIZE |
            WintabInterop.CXL_INASPECT |
            WintabInterop.CXL_SENSITIVITY |
            WintabInterop.CXL_MARGIN |
            WintabInterop.CXL_SYSOUT;
        context.PktData = WintabInterop.PK_STATUS | WintabInterop.PK_X | WintabInterop.PK_Y | WintabInterop.PK_NORMAL_PRESSURE | WintabInterop.PK_BUTTONS;
        if (_expressKeysMask > 0)
        {
            context.PktData |= _expressKeysMask;
        }
        context.PktMode = 0;
        context.MoveMask = context.PktData;
        context.BtnUpMask = context.BtnDnMask;
        context.OutOrgX = 0;
        context.OutOrgY = 0;
        context.OutExtX = xAxis.Max - xAxis.Min + 1;
        context.OutExtY = -(yAxis.Max - yAxis.Min + 1);
        context.SysMode = false;

        _packetMessage = unchecked((int)context.MsgBase + WintabInterop.WT_PACKET);
        _packetExtMessage = unchecked((int)context.MsgBase + WintabInterop.WT_PACKETEXT);
        _contextOverlapMessage = unchecked((int)context.MsgBase + WintabInterop.WT_CTXOVERLAP);
        _proximityMessage = unchecked((int)context.MsgBase + WintabInterop.WT_PROXIMITY);
        _infoChangeMessage = unchecked((int)context.MsgBase + WintabInterop.WT_INFOCHANGE);

        uint handle = WintabInterop.WTOpenA(_messageSource!.Handle, ref context, true);
        if (handle == 0)
        {
            DiagnosticsChanged?.Invoke(this, $"WTOpenA failed for {name} (device {deviceIndex})");
            return 0;
        }

        _expressKeyCount = SetupExpressKeyOverrides(handle, (byte)deviceIndex);
        _contexts[handle] = new WintabContextInfo(
            DeviceIndex: deviceIndex,
            OutputMinX: 0,
            OutputMaxX: Math.Max(1, context.OutExtX - 1),
            OutputMinY: 0,
            OutputMaxY: Math.Max(1, Math.Abs(context.OutExtY) - 1),
            PressureMax: Math.Max(1, pressureAxis.Max),
            IsIntegrated: WintabInterop.IsIntegratedTablet(deviceIndex));

        DiagnosticsChanged?.Invoke(this, $"{name} (device {deviceIndex}) opened");
        return _contexts.Count;
    }

    private int SetupExpressKeyOverrides(uint contextHandle, byte tabletIndex)
    {
        if (_expressKeysMask == 0)
        {
            DiagnosticsChanged?.Invoke(this, "EK: mask=0, not supported");
            return 0;
        }

        uint extTag = WintabInterop.WTX_EXPKEYS2;
        if (!WintabInterop.GetExtensionProperty(contextHandle, extTag, tabletIndex, 0, 0, WintabInterop.TABLET_PROPERTY_CONTROLCOUNT, out uint numControls))
        {
            DiagnosticsChanged?.Invoke(this, $"EK: CONTROLCOUNT failed (ctx=0x{contextHandle:X}, tag={extTag}, tablet={tabletIndex})");
            return 0;
        }

        int overrideCount = 0;
        for (byte controlIndex = 0; controlIndex < numControls; controlIndex++)
        {
            if (!WintabInterop.GetExtensionProperty(contextHandle, extTag, tabletIndex, controlIndex, 0, WintabInterop.TABLET_PROPERTY_FUNCCOUNT, out uint numFuncs))
            {
                continue;
            }

            for (byte funcIndex = 0; funcIndex < numFuncs; funcIndex++)
            {
                WintabInterop.GetExtensionProperty(contextHandle, extTag, tabletIndex, controlIndex, funcIndex, WintabInterop.TABLET_PROPERTY_AVAILABLE, out uint available);
                if (available == 0)
                {
                    continue;
                }

                bool overrideOk = WintabInterop.SetExtensionProperty(contextHandle, extTag, tabletIndex, controlIndex, funcIndex, WintabInterop.TABLET_PROPERTY_OVERRIDE, 1);
                WintabInterop.SetExtensionPropertyString(contextHandle, extTag, tabletIndex, controlIndex, funcIndex, WintabInterop.TABLET_PROPERTY_OVERRIDE_NAME, $"MIDI EK{controlIndex}");
                if (overrideOk)
                {
                    overrideCount++;
                }
            }
        }

        DiagnosticsChanged?.Invoke(this, $"EK: {numControls} keys, {overrideCount} overridden");
        return (int)numControls;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == _packetMessage)
        {
            uint packetId = unchecked((uint)wParam.ToInt32());
            uint context = unchecked((uint)lParam.ToInt32());
            if (_contexts.TryGetValue(context, out WintabContextInfo info) && WintabInterop.WTPacket(context, packetId, out WintabInterop.Packet packet))
            {
                double xNorm = NormalizeWintabCoordinate(packet.X, info.OutputMinX, info.OutputMaxX);
                double yNorm = NormalizeWintabCoordinate(packet.Y, info.OutputMinY, info.OutputMaxY);
                int pressure = info.PressureMax > 0
                    ? (int)Math.Round(packet.NormalPressure * 127.0 / info.PressureMax)
                    : 127;
                bool hovering = pressure <= 0;
                string diagnostics = $"pkt: status=0x{packet.Status:X} p={packet.NormalPressure} x={packet.X} y={packet.Y} hover={hovering}";
                PacketReceived?.Invoke(this, new WintabPacketEventArgs(
                    ContextHandle: context,
                    DeviceIndex: info.DeviceIndex,
                    XNorm: xNorm,
                    YNorm: yNorm,
                    Pressure127: pressure,
                    IsHovering: hovering,
                    Buttons: packet.Buttons,
                    PacketDiagnostics: diagnostics));
                handled = true;
            }
        }
        else if (msg == _packetExtMessage)
        {
            uint packetId = unchecked((uint)wParam.ToInt32());
            uint context = unchecked((uint)lParam.ToInt32());
            if (WintabInterop.GetPacketExt(context, packetId, out WintabInterop.PacketExt packetExt))
            {
                int control = packetExt.ExpKey.Control;
                bool isPressed = packetExt.ExpKey.State != 0;
                string diagnostics = $"EK{control}: {(isPressed ? "pressed" : "released")}";
                ExpressKeyReceived?.Invoke(this, new WintabExpressKeyEventArgs(control, isPressed, diagnostics));
            }
            else
            {
                DiagnosticsChanged?.Invoke(this, $"EK: GetPacketExt failed (ctx=0x{context:X}, pkt={packetId})");
            }

            handled = true;
        }
        else if (msg == _contextOverlapMessage)
        {
            uint context = unchecked((uint)wParam.ToInt32());
            if (_contexts.ContainsKey(context))
            {
                _ = WintabInterop.WTOverlap(context, true);
                _ = WintabInterop.WTEnable(context, true);
                DiagnosticsChanged?.Invoke(this, $"WT_CTXOVERLAP recovered for context 0x{context:X}");
                handled = true;
            }
        }
        else if (msg == _proximityMessage)
        {
            bool entering = unchecked((uint)lParam.ToInt32()) != 0;
            string diagnostics = $"WT_PROXIMITY: wParam=0x{wParam.ToInt32():X} lParam=0x{lParam.ToInt32():X} entering={entering}";
            ProximityChanged?.Invoke(this, new WintabProximityEventArgs(entering, diagnostics));
            handled = true;
        }
        else if (msg == _infoChangeMessage)
        {
            Reinitialize(_selectedDeviceIndex);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static double NormalizeWintabCoordinate(int value, int min, int max)
    {
        if (max <= min)
        {
            return 0.0;
        }

        double normalized = (value - min) / (double)(max - min);
        return Math.Clamp(normalized, 0.0, 1.0);
    }
}
