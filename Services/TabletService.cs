using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows.Interop;
using TabletMidiPad.Infrastructure;

namespace TabletMidiPad.Services;

public sealed class TabletService : ITabletService
{
    private const string WacomServiceName = "WTabletServicePro";
    private readonly Dictionary<uint, WintabContextInfo> _wintabContexts = new();
    private string _backendMode = "Auto";
    private IntPtr _messageWindowHandle;
    private uint _expressKeysMask;
    private int _wintabPacketMessage;
    private int _wintabPacketExtMessage;
    private int _wintabContextOverlapMessage;
    private int _wintabProximityMessage;
    private int _wintabInfoChangeMessage;

    public bool UseWintab { get; private set; }
    public string LastInputBackend { get; private set; } = "Idle";
    public string WintabDiagnostics { get; private set; } = "Wintab not initialized yet";
    public string ExpressKeyDiagnostics { get; private set; } = "";
    public int ExpressKeyCount { get; private set; }
    public string WacomServiceStatus => GetWacomServiceStatus();

    public event Action<string, string>? LoadingStateChanged;
    public event Action<double>? LoadingProgressChanged;
    public event Action<int, int, int, int, int, uint, string>? WintabPacketReceived;
    public event Action<int, bool>? ExpressKeyPacketReceived;
    public event Action? ProximityLost;

    public void Initialize(string backendMode, IntPtr messageWindowHandle)
    {
        _backendMode = backendMode;
        _messageWindowHandle = messageWindowHandle;

        string normalizedMode = NormalizeInputBackendMode(backendMode);
        if (normalizedMode == "Stylus/Mouse Only")
        {
            Shutdown();
            WintabDiagnostics = "Manual stylus/mouse mode selected";
            LastInputBackend = "Stylus/Mouse Fallback";
            return;
        }

        if (!WintabInterop.IsAvailable())
        {
            UseWintab = false;
            WintabDiagnostics = "Wintab32.dll not available";
            LastInputBackend = normalizedMode == "Wintab Only" ? "Wintab unavailable" : "Stylus/Mouse Fallback";
            LoadingStateChanged?.Invoke("Preparing tablet input", normalizedMode == "Wintab Only" ? "Wintab requested, but Wintab32.dll is not available" : "Wintab not found, using stylus and mouse fallback");
            return;
        }

        try
        {
            int openedContexts = OpenWintabContexts();
            UseWintab = openedContexts > 0;
            LastInputBackend = UseWintab ? "Wintab" : (normalizedMode == "Wintab Only" ? "Wintab unavailable" : "Stylus/Mouse Fallback");
            if (UseWintab) WintabDiagnostics = $"Opened {openedContexts} Wintab context(s)";

            LoadingStateChanged?.Invoke("Preparing tablet input", UseWintab ? $"Wintab digitizer input active on {openedContexts} tablet context(s)" : (normalizedMode == "Wintab Only" ? "Wintab requested, but no digitizer contexts could open" : "Wintab detected, but no digitizer contexts could open"));
        }
        catch (Exception ex)
        {
            UseWintab = false;
            LastInputBackend = normalizedMode == "Wintab Only" ? "Wintab unavailable" : "Stylus/Mouse Fallback";
            WintabDiagnostics = ex.Message;
            LoadingStateChanged?.Invoke("Preparing tablet input", normalizedMode == "Wintab Only" ? "Wintab failed to initialize" : "Wintab failed, using stylus and mouse fallback");
        }
    }

    public void Shutdown()
    {
        foreach (uint contextHandle in _wintabContexts.Keys.ToArray())
        {
            try { WintabInterop.WTClose(contextHandle); } catch { }
        }
        _wintabContexts.Clear();
        UseWintab = false;
        LastInputBackend = "Stylus/Mouse Fallback";
        WintabDiagnostics = "Wintab shut down";
        ExpressKeyDiagnostics = "";
    }

    public void Reinitialize()
    {
        Shutdown();
        Initialize(_backendMode, _messageWindowHandle);
    }

    public string GetWacomServiceStatus()
    {
        try
        {
            using var service = new ServiceController(WacomServiceName);
            string displayName = string.IsNullOrWhiteSpace(service.DisplayName) ? WacomServiceName : service.DisplayName;
            return service.Status == ServiceControllerStatus.Running ? $"Service: {displayName} running" : $"Service: {displayName} {service.Status}";
        }
        catch (InvalidOperationException) { return "Service: not found"; }
        catch (Exception ex) { return $"Service: {ex.Message}"; }
    }

    public async Task StartWacomServiceAsync()
    {
        await Task.Run(() =>
        {
            using var service = new ServiceController(WacomServiceName);
            if (service.Status != ServiceControllerStatus.Running)
            {
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
            }
        });
    }

    private int OpenWintabContexts()
    {
        _wintabContexts.Clear();
        _expressKeysMask = WintabInterop.GetExtensionMask(WintabInterop.WTX_EXPKEYS2);
        bool messageBaseInitialized = false;

        void InitMessageBase(uint msgBase)
        {
            _wintabPacketMessage = unchecked((int)msgBase + WintabInterop.WT_PACKET);
            _wintabPacketExtMessage = unchecked((int)msgBase + WintabInterop.WT_PACKETEXT);
            _wintabContextOverlapMessage = unchecked((int)msgBase + WintabInterop.WT_CTXOVERLAP);
            _wintabProximityMessage = unchecked((int)msgBase + WintabInterop.WT_PROXIMITY);
            _wintabInfoChangeMessage = unchecked((int)msgBase + WintabInterop.WT_INFOCHANGE);
            messageBaseInitialized = true;
        }

        try
        {
            var defaultContext = WintabInterop.GetDefaultDigitizingContext();
            defaultContext.Name = "TabletMidiPad Default Digitizer";
            defaultContext.Options |= WintabInterop.CXO_MESSAGES;
            defaultContext.Options &= ~WintabInterop.CXO_SYSTEM;
            defaultContext.Locks = WintabInterop.CXL_INSIZE | WintabInterop.CXL_INASPECT | WintabInterop.CXL_SENSITIVITY | WintabInterop.CXL_MARGIN | WintabInterop.CXL_SYSOUT;
            defaultContext.PktData = WintabInterop.PK_X | WintabInterop.PK_Y | WintabInterop.PK_NORMAL_PRESSURE | WintabInterop.PK_BUTTONS;
            if (_expressKeysMask > 0) defaultContext.PktData |= _expressKeysMask;

            int deviceIndex = (int)defaultContext.Device;
            var xAxis = WintabInterop.GetXAxis(deviceIndex);
            var yAxis = WintabInterop.GetYAxis(deviceIndex);
            var pressureAxis = WintabInterop.GetPressureAxis(deviceIndex);

            if (xAxis.Max > xAxis.Min && yAxis.Max > yAxis.Min)
            {
                defaultContext.OutOrgX = 0;
                defaultContext.OutOrgY = 0;
                defaultContext.OutExtX = xAxis.Max - xAxis.Min + 1;
                defaultContext.OutExtY = -(yAxis.Max - yAxis.Min + 1);
                defaultContext.SysMode = false;
                InitMessageBase(defaultContext.MsgBase);

                uint handle = WintabInterop.WTOpenA(_messageWindowHandle, ref defaultContext, true);
                if (handle != 0)
                {
                    SetupExpressKeyOverrides(handle, (byte)Math.Max(0, deviceIndex));
                    _wintabContexts[handle] = new WintabContextInfo(deviceIndex, 0, Math.Max(1, defaultContext.OutExtX - 1), 0, Math.Max(1, Math.Abs(defaultContext.OutExtY) - 1), Math.Max(1, pressureAxis.Max));
                }
            }
        }
        catch { }

        if (_wintabContexts.Count == 0)
        {
            int deviceCount = Math.Max(1, WintabInterop.GetAttachedDeviceCount());
            for (int i = 0; i < deviceCount; i++)
            {
                try
                {
                    var context = WintabInterop.GetDigitizerContext(i);
                    var xAxis = WintabInterop.GetXAxis(i);
                    var yAxis = WintabInterop.GetYAxis(i);
                    var pressureAxis = WintabInterop.GetPressureAxis(i);

                    if (xAxis.Max <= xAxis.Min || yAxis.Max <= yAxis.Min) continue;

                    context.Name = $"TabletMidiPad Digitizer {i}";
                    context.Options |= WintabInterop.CXO_MESSAGES;
                    context.Options &= ~WintabInterop.CXO_SYSTEM;
                    context.Locks = WintabInterop.CXL_INSIZE | WintabInterop.CXL_INASPECT | WintabInterop.CXL_SENSITIVITY | WintabInterop.CXL_MARGIN | WintabInterop.CXL_SYSOUT;
                    context.PktData = WintabInterop.PK_X | WintabInterop.PK_Y | WintabInterop.PK_NORMAL_PRESSURE | WintabInterop.PK_BUTTONS;
                    if (_expressKeysMask > 0) context.PktData |= _expressKeysMask;
                    context.OutOrgX = 0;
                    context.OutOrgY = 0;
                    context.OutExtX = xAxis.Max - xAxis.Min + 1;
                    context.OutExtY = -(yAxis.Max - yAxis.Min + 1);
                    context.SysMode = false;

                    if (!messageBaseInitialized) InitMessageBase(context.MsgBase);

                    uint handle = WintabInterop.WTOpenA(_messageWindowHandle, ref context, true);
                    if (handle != 0)
                    {
                        SetupExpressKeyOverrides(handle, (byte)i);
                        _wintabContexts[handle] = new WintabContextInfo(i, 0, Math.Max(1, context.OutExtX - 1), 0, Math.Max(1, Math.Abs(context.OutExtY) - 1), Math.Max(1, pressureAxis.Max));
                    }
                }
                catch { }
            }
        }

        return _wintabContexts.Count;
    }

    private void SetupExpressKeyOverrides(uint contextHandle, byte tabletIndex)
    {
        if (_expressKeysMask == 0) return;
        uint extTag = WintabInterop.WTX_EXPKEYS2;
        if (!WintabInterop.GetExtensionProperty(contextHandle, extTag, tabletIndex, 0, 0, WintabInterop.TABLET_PROPERTY_CONTROLCOUNT, out uint numControls) || numControls == 0) return;

        int overrideCount = 0;
        for (byte controlIndex = 0; controlIndex < numControls; controlIndex++)
        {
            if (!WintabInterop.GetExtensionProperty(contextHandle, extTag, tabletIndex, controlIndex, 0, WintabInterop.TABLET_PROPERTY_FUNCCOUNT, out uint numFuncs)) continue;
            for (byte funcIndex = 0; funcIndex < numFuncs; funcIndex++)
            {
                if (!WintabInterop.GetExtensionProperty(contextHandle, extTag, tabletIndex, controlIndex, funcIndex, WintabInterop.TABLET_PROPERTY_AVAILABLE, out uint available) || available == 0) continue;
                if (WintabInterop.SetExtensionProperty(contextHandle, extTag, tabletIndex, controlIndex, funcIndex, WintabInterop.TABLET_PROPERTY_OVERRIDE, 1))
                {
                    WintabInterop.SetExtensionPropertyString(contextHandle, extTag, tabletIndex, controlIndex, funcIndex, WintabInterop.TABLET_PROPERTY_OVERRIDE_NAME, $"MIDI EK{controlIndex}");
                    overrideCount++;
                }
            }
        }
        ExpressKeyCount = (int)numControls;
        ExpressKeyDiagnostics = $"EK: {numControls} keys, {overrideCount} overridden";
    }

    public IntPtr HandleWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == _wintabPacketMessage)
        {
            uint packetId = unchecked((uint)wParam.ToInt32());
            uint contextHandle = unchecked((uint)lParam.ToInt32());
            if (_wintabContexts.TryGetValue(contextHandle, out var context) && WintabInterop.WTPacket(contextHandle, packetId, out var packet))
            {
                WintabPacketReceived?.Invoke(context.DeviceIndex, packet.X, packet.Y, (int)packet.NormalPressure, context.PressureMax, packet.Buttons, $"Wintab D{context.DeviceIndex + 1}");
                handled = true;
            }
        }
        else if (msg == _wintabPacketExtMessage)
        {
            uint packetId = unchecked((uint)wParam.ToInt32());
            uint contextHandle = unchecked((uint)lParam.ToInt32());
            if (WintabInterop.GetPacketExt(contextHandle, packetId, out var pktExt))
            {
                ExpressKeyPacketReceived?.Invoke(pktExt.ExpKey.Control, pktExt.ExpKey.State != 0);
            }
            handled = true;
        }
        else if (msg == _wintabContextOverlapMessage)
        {
            uint contextHandle = unchecked((uint)wParam.ToInt32());
            if (_wintabContexts.ContainsKey(contextHandle))
            {
                WintabInterop.WTOverlap(contextHandle, true);
                WintabInterop.WTEnable(contextHandle, true);
                handled = true;
            }
        }
        else if (msg == _wintabProximityMessage)
        {
            if (unchecked((uint)wParam.ToInt32()) == 0) ProximityLost?.Invoke();
            handled = true;
        }
        else if (msg == _wintabInfoChangeMessage)
        {
            Reinitialize();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static string NormalizeInputBackendMode(string? text) => (text ?? "").Trim() switch { "Wintab" or "Wintab Only" => "Wintab Only", "Mouse" or "Stylus/Mouse Only" => "Stylus/Mouse Only", _ => "Auto" };

    public void Dispose() => Shutdown();

    private readonly record struct WintabContextInfo(int DeviceIndex, int OutputMinX, int OutputMaxX, int OutputMinY, int OutputMaxY, int PressureMax);
}
