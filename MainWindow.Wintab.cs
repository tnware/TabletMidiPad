using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace TabletMidiPad;

public partial class MainWindow
{
    private void InitializeWintab()
    {
        string backendMode = _inputMappingService.NormalizeInputBackendMode(_config.InputBackendMode);
        if (string.Equals(backendMode, "Stylus/Mouse Only", StringComparison.Ordinal))
        {
            ShutdownWintab();
            _wintabDiagnostics = "Manual stylus/mouse mode selected";
            _lastInputBackend = "Stylus/Mouse Fallback";
            UpdateInputBackendStatus();
            return;
        }

        PopulateTabletDeviceList();

        if (!WintabInterop.IsAvailable())
        {
            _useWintab = false;
            _wintabDiagnostics = "Wintab32.dll not available";
            _lastInputBackend = string.Equals(backendMode, "Wintab Only", StringComparison.Ordinal)
                ? "Wintab unavailable"
                : "Stylus/Mouse Fallback";
            UpdateInputBackendStatus();
            return;
        }

        try
        {
            _wintabMessageSource = CreateWintabMessageWindow();
            if (_wintabMessageSource is null)
            {
                return;
            }

            _wintabMessageSource.AddHook(WndProc);
            int openedContexts = OpenWintabContexts();
            _useWintab = openedContexts > 0;
            _lastInputBackend = _useWintab
                ? "Wintab"
                : string.Equals(backendMode, "Wintab Only", StringComparison.Ordinal)
                    ? "Wintab unavailable"
                    : "Stylus/Mouse Fallback";
            if (_useWintab)
            {
                _wintabDiagnostics = $"Opened {openedContexts} Wintab context(s)";
            }

            UpdateInputBackendStatus();
        }
        catch (Exception ex)
        {
            _useWintab = false;
            _lastInputBackend = string.Equals(backendMode, "Wintab Only", StringComparison.Ordinal)
                ? "Wintab unavailable"
                : "Stylus/Mouse Fallback";
            _wintabDiagnostics = ex.Message;
            UpdateInputBackendStatus();
        }
    }

    private void PopulateTabletDeviceList()
    {
        TabletDeviceComboBox.SelectionChanged -= TabletDeviceComboBox_SelectionChanged;
        TabletDeviceComboBox.Items.Clear();

        if (!WintabInterop.IsAvailable())
        {
            TabletDeviceComboBox.Items.Add("No Wintab");
            TabletDeviceComboBox.SelectedIndex = 0;
            TabletDeviceComboBox.IsEnabled = false;
            TabletDeviceComboBox.SelectionChanged += TabletDeviceComboBox_SelectionChanged;
            return;
        }

        int deviceCount = WintabInterop.GetAttachedDeviceCount();
        if (deviceCount <= 0)
        {
            TabletDeviceComboBox.Items.Add("No tablets");
            TabletDeviceComboBox.SelectedIndex = 0;
            TabletDeviceComboBox.IsEnabled = false;
            TabletDeviceComboBox.SelectionChanged += TabletDeviceComboBox_SelectionChanged;
            return;
        }

        TabletDeviceComboBox.IsEnabled = true;
        int validCount = 0;
        int selectIndex = 0;

        for (int i = 0; i < deviceCount; i++)
        {
            string name = WintabInterop.GetDeviceName(i);
            bool integrated = WintabInterop.IsIntegratedTablet(i);
            string label = integrated ? $"{name} (built-in)" : name;
            TabletDeviceComboBox.Items.Add(new TabletDeviceItem(i, label));

            if (_selectedDeviceIndex >= 0 && i == _selectedDeviceIndex)
            {
                selectIndex = validCount;
            }

            validCount++;
        }

        if (validCount > 0)
        {
            TabletDeviceComboBox.SelectedIndex = selectIndex;
            _selectedDeviceIndex = ((TabletDeviceItem)TabletDeviceComboBox.Items[selectIndex]!).DeviceIndex;
        }

        TabletDeviceComboBox.SelectionChanged += TabletDeviceComboBox_SelectionChanged;
    }

    private void TabletDeviceComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (TabletDeviceComboBox.SelectedItem is not TabletDeviceItem item)
        {
            return;
        }

        if (item.DeviceIndex == _selectedDeviceIndex)
        {
            return;
        }

        _selectedDeviceIndex = item.DeviceIndex;
        RebindInputBackend();
    }

    private sealed record TabletDeviceItem(int DeviceIndex, string Label)
    {
        public override string ToString() => Label;
    }

    private void ShutdownWintab()
    {
        if (_wintabMessageSource is not null)
        {
            _wintabMessageSource.RemoveHook(WndProc);
            _wintabMessageSource.Dispose();
            _wintabMessageSource = null;
        }

        foreach (uint contextHandle in _wintabContexts.Keys.ToArray())
        {
            try
            {
                WintabInterop.WTClose(contextHandle);
            }
            catch
            {
            }
        }

        _wintabContexts.Clear();
        _useWintab = false;
        _lastInputBackend = "Stylus/Mouse Fallback";
        _wintabDiagnostics = "Wintab shut down";
        _wintabPacketMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_PACKET;
        _wintabPacketExtMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_PACKETEXT;
        _wintabContextOverlapMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_CTXOVERLAP;
        _wintabProximityMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_PROXIMITY;
        _wintabInfoChangeMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_INFOCHANGE;
        _expressKeysMask = 0;
        _expressKeyHeld.Clear();
        _expressKeyToggleState.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == _wintabPacketMessage)
        {
            uint packetId = unchecked((uint)wParam.ToInt32());
            uint context = unchecked((uint)lParam.ToInt32());

            if (_wintabContexts.ContainsKey(context) && WintabInterop.WTPacket(context, packetId, out var packet))
            {
                HandleWintabPacket(context, packet);
                handled = true;
            }
        }
        else if (msg == _wintabPacketExtMessage)
        {
            uint packetId = unchecked((uint)wParam.ToInt32());
            uint context = unchecked((uint)lParam.ToInt32());

            if (WintabInterop.GetPacketExt(context, packetId, out var pktExt))
            {
                HandleExpressKeyPacket(pktExt);
            }
            else
            {
                _expressKeyDiagnostics = $"EK: GetPacketExt failed (ctx=0x{context:X}, pkt={packetId})";
                UpdateInputBackendStatus();
            }
            handled = true;
        }
        else if (msg == _wintabContextOverlapMessage)
        {
            uint context = unchecked((uint)wParam.ToInt32());
            if (_wintabContexts.ContainsKey(context))
            {
                _ = WintabInterop.WTOverlap(context, true);
                _ = WintabInterop.WTEnable(context, true);
                _wintabDiagnostics = $"WT_CTXOVERLAP recovered for context 0x{context:X}";
                UpdateInputBackendStatus();
                handled = true;
            }
        }
        else if (msg == _wintabProximityMessage)
        {
            bool entering = unchecked((uint)lParam.ToInt32()) != 0;
            _wintabDiagnostics = $"WT_PROXIMITY: wParam=0x{wParam.ToInt32():X} lParam=0x{lParam.ToInt32():X} entering={entering}";
            UpdateInputBackendStatus();

            if (!entering)
            {
                if (_isActive)
                {
                    _isActive = false;
                    HandleRelease();
                }

                CursorDot.Visibility = Visibility.Collapsed;
                CursorDot.Opacity = 0.85;
                _viewModel.ActiveZoneText = "Active zone: none";
            }

            handled = true;
        }
        else if (msg == _wintabInfoChangeMessage)
        {
            ReinitializeWintabContexts();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static HwndSource CreateWintabMessageWindow()
    {
        var parameters = new HwndSourceParameters("TabletMidiPadWintabSink")
        {
            Width = 1,
            Height = 1,
            WindowStyle = unchecked((int)0x80000000), // WS_POPUP
            ExtendedWindowStyle = 0x08000000 // WS_EX_NOACTIVATE
        };

        return new HwndSource(parameters);
    }

    private int OpenWintabContexts()
    {
        _wintabContexts.Clear();
        _expressKeyHeld.Clear();
        _expressKeyToggleState.Clear();

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

        return OpenWintabContextForDevice(deviceIndex);
    }

    private int OpenWintabContextForDevice(int deviceIndex)
    {
        WintabInterop.LogContext context;
        try
        {
            context = WintabInterop.GetDigitizerContext(deviceIndex);
        }
        catch (Exception ex)
        {
            _wintabDiagnostics = $"WTI_DDCTXS {deviceIndex} failed: {ex.Message}";
            return 0;
        }

        var xAxis = WintabInterop.GetXAxis(deviceIndex);
        var yAxis = WintabInterop.GetYAxis(deviceIndex);
        var pressureAxis = WintabInterop.GetPressureAxis(deviceIndex);

        if (xAxis.Max <= xAxis.Min || yAxis.Max <= yAxis.Min)
        {
            _wintabDiagnostics = $"Device {deviceIndex} axes invalid: X {xAxis.Min}-{xAxis.Max}, Y {yAxis.Min}-{yAxis.Max}";
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
            context.PktData |= _expressKeysMask;
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

        uint handle = WintabInterop.WTOpenA(_wintabMessageSource!.Handle, ref context, true);
        if (handle == 0)
        {
            _wintabDiagnostics = $"WTOpenA failed for {name} (device {deviceIndex})";
            return 0;
        }

        SetupExpressKeyOverrides(handle, (byte)deviceIndex);
        _wintabDiagnostics = $"{name} (device {deviceIndex}) opened";
        _wintabContexts[handle] = new WintabContextInfo(
            DeviceIndex: deviceIndex,
            OutputMinX: 0,
            OutputMaxX: Math.Max(1, context.OutExtX - 1),
            OutputMinY: 0,
            OutputMaxY: Math.Max(1, Math.Abs(context.OutExtY) - 1),
            PressureMax: Math.Max(1, pressureAxis.Max),
            IsIntegrated: WintabInterop.IsIntegratedTablet(deviceIndex)
        );

        return _wintabContexts.Count;
    }

    private void ReinitializeWintabContexts()
    {
        if (_wintabMessageSource is null)
        {
            return;
        }

        foreach (uint contextHandle in _wintabContexts.Keys.ToArray())
        {
            try
            {
                WintabInterop.WTClose(contextHandle);
            }
            catch
            {
            }
        }

        int openedContexts = OpenWintabContexts();
        _useWintab = openedContexts > 0;
        _lastInputBackend = _useWintab
            ? "Wintab"
            : string.Equals(_inputMappingService.NormalizeInputBackendMode(_config.InputBackendMode), "Wintab Only", StringComparison.Ordinal)
                ? "Wintab unavailable"
                : "Stylus/Mouse Fallback";
        UpdateInputBackendStatus();
    }

    private void RebindInputBackend()
    {
        ResetActiveInputState();
        ShutdownWintab();
        InitializeWintab();
        UpdateUiForCurrentConfig();
    }

    private void ResetActiveInputState()
    {
        _isActive = false;
        _activeZone = null;
        _lowerPenButtonHeld = false;
        _upperPenButtonHeld = false;
        _hasLockedPosition = false;
        CursorDot.Visibility = Visibility.Collapsed;
        _viewModel.ActiveZoneText = "Active zone: none";
        _viewModel.ValueText = "X: -, Y: -, Pressure: -";
        _viewModel.PenButtonsText = "Pen buttons: -";
        UpdateModifiersText();
        HandleRelease();
        Pad.ReleaseMouseCapture();
        Pad.ReleaseStylusCapture();
    }

    private void SetupExpressKeyOverrides(uint contextHandle, byte tabletIndex)
    {
        if (_expressKeysMask == 0)
        {
            _expressKeyDiagnostics = "EK: mask=0, not supported";
            return;
        }

        // WTExtGet/WTExtSet take the extension TAG value, not the WTI_EXTENSIONS index.
        uint extTag = WintabInterop.WTX_EXPKEYS2;

        if (!WintabInterop.GetExtensionProperty(contextHandle, extTag, tabletIndex,
            0, 0, WintabInterop.TABLET_PROPERTY_CONTROLCOUNT, out uint numControls))
        {
            _expressKeyDiagnostics = $"EK: CONTROLCOUNT failed (ctx=0x{contextHandle:X}, tag={extTag}, tablet={tabletIndex})";
            return;
        }

        if (numControls == 0)
        {
            _expressKeyDiagnostics = $"EK: 0 controls on tablet {tabletIndex}";
            return;
        }

        int overrideCount = 0;
        int failCount = 0;

        for (byte controlIndex = 0; controlIndex < numControls; controlIndex++)
        {
            if (!WintabInterop.GetExtensionProperty(contextHandle, extTag, tabletIndex,
                controlIndex, 0, WintabInterop.TABLET_PROPERTY_FUNCCOUNT, out uint numFuncs))
                continue;

            for (byte funcIndex = 0; funcIndex < numFuncs; funcIndex++)
            {
                WintabInterop.GetExtensionProperty(contextHandle, extTag, tabletIndex,
                    controlIndex, funcIndex, WintabInterop.TABLET_PROPERTY_AVAILABLE, out uint available);

                if (available == 0)
                    continue;

                bool overrideOk = WintabInterop.SetExtensionProperty(contextHandle, extTag, tabletIndex,
                    controlIndex, funcIndex, WintabInterop.TABLET_PROPERTY_OVERRIDE, 1);

                WintabInterop.SetExtensionPropertyString(contextHandle, extTag, tabletIndex,
                    controlIndex, funcIndex, WintabInterop.TABLET_PROPERTY_OVERRIDE_NAME,
                    $"MIDI EK{controlIndex}");

                if (overrideOk)
                    overrideCount++;
                else
                    failCount++;
            }
        }

        _expressKeyCount = (int)numControls;
        ExpressKeyConfig.EnsureCount(_config.ExpressKeys, _expressKeyCount);
        _expressKeyDiagnostics = $"EK: {numControls} keys, {overrideCount} overridden";
    }

    private void HandleExpressKeyPacket(WintabInterop.PacketExt pktExt)
    {
        int control = pktExt.ExpKey.Control;
        bool isPressed = pktExt.ExpKey.State != 0;

        _expressKeyHeld.TryGetValue(control, out bool wasHeld);
        _expressKeyToggleState.TryGetValue(control, out bool toggleState);

        bool justPressed = isPressed && !wasHeld;
        bool justReleased = !isPressed && wasHeld;

        ExpressKeyConfig.EnsureCount(_config.ExpressKeys, control + 1);
        var keyConfig = _config.ExpressKeys[control];
        string action = _inputMappingService.NormalizePenButtonAction(keyConfig.Action);
        int channel = keyConfig.MidiChannel;
        int cc = Math.Clamp(keyConfig.Cc, 0, 127);
        int note = Math.Clamp(keyConfig.Note, 0, 127);

        if (string.Equals(action, PenButtonActions.CcMomentary, StringComparison.Ordinal))
        {
            if (justPressed)
                SendCcIfChanged(channel, cc, 127, force: true);
            else if (justReleased)
                SendCcIfChanged(channel, cc, 0, force: true);
        }
        else if (string.Equals(action, PenButtonActions.CcToggle, StringComparison.Ordinal))
        {
            if (justPressed)
            {
                toggleState = !toggleState;
                SendCcIfChanged(channel, cc, toggleState ? 127 : 0, force: true);
            }
        }
        else if (string.Equals(action, PenButtonActions.NoteMomentary, StringComparison.Ordinal))
        {
            if (justPressed)
                SendNoteIfChanged(channel, note, true, 127, force: true);
            else if (justReleased)
                SendNoteIfChanged(channel, note, false, 0, force: true);
        }
        else if (string.Equals(action, PenButtonActions.NoteToggle, StringComparison.Ordinal))
        {
            if (justPressed)
            {
                toggleState = !toggleState;
                SendNoteIfChanged(channel, note, toggleState, toggleState ? 127 : 0, force: true);
            }
        }

        _expressKeyHeld[control] = isPressed;
        _expressKeyToggleState[control] = toggleState;

        _expressKeyDiagnostics = $"EK{control}: {(isPressed ? "pressed" : "released")} [{action}]";
        MarkInputBackend($"Wintab EK{control}");
        UpdateInputBackendStatus();
    }

    private void HandleWintabPacket(uint contextHandle, WintabInterop.Packet packet)
    {
        double width = Pad.ActualWidth;
        double height = Pad.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (!_wintabContexts.TryGetValue(contextHandle, out WintabContextInfo context))
        {
            return;
        }

        double xNorm = NormalizeWintabCoordinate(packet.X, context.OutputMinX, context.OutputMaxX);
        double yNorm = NormalizeWintabCoordinate(packet.Y, context.OutputMinY, context.OutputMaxY);
        MarkInputBackend($"Wintab D{context.DeviceIndex + 1}");
        UpdatePenButtonsDebug(packet.Buttons);
        UpdatePenButtonActionState(packet.Buttons);
        double xPx = xNorm * width;
        double yPx = yNorm * height;
        int pressure = context.PressureMax > 0
            ? (int)Math.Round(packet.NormalPressure * 127.0 / context.PressureMax)
            : 127;
        bool hovering = pressure <= 0;

        _wintabDiagnostics = $"pkt: status=0x{packet.Status:X} p={packet.NormalPressure} x={packet.X} y={packet.Y} hover={hovering}";
        UpdateInputBackendStatus();

        if (hovering)
        {
            if (_isActive)
            {
                _isActive = false;
                HandleRelease();
            }

            CursorDot.Opacity = 0.35;
            CursorDot.Visibility = Visibility.Visible;
            Canvas.SetLeft(CursorDot, xPx - CursorDot.Width / 2);
            Canvas.SetTop(CursorDot, yPx - CursorDot.Height / 2);

            var hoverZone = FindMatchingZone(xNorm, yNorm);
            _viewModel.ActiveZoneText = hoverZone is not null
                ? $"Hovering: {hoverZone.Name}"
                : "Hovering: outside zones";
            return;
        }

        CursorDot.Opacity = 0.85;
        _isActive = true;
        HandlePoint(xPx, yPx, xNorm, yNorm, pressure);
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

    private static bool IsPromotedStylusMouseEvent(MouseEventArgs e)
    {
        return e.StylusDevice is not null;
    }

    private bool AllowsStylusMouseFallback()
    {
        return !string.Equals(_inputMappingService.NormalizeInputBackendMode(_config.InputBackendMode), "Wintab Only", StringComparison.Ordinal);
    }

    private (double x, double y) ApplyPositionConditioning(ZoneConfig zone, double x, double y)
    {
        if (!_smoothedZonePoints.TryGetValue(zone, out Point lastPoint))
        {
            var initial = new Point(x, y);
            _smoothedZonePoints[zone] = initial;
            return (initial.X, initial.Y);
        }

        double deadZone = Math.Clamp(_config.PositionDeadZone, 0.0, 0.1);
        double smoothing = Math.Clamp(_config.PositionSmoothing, 0.0, 0.95);
        double alpha = 1.0 - smoothing;

        double targetX = Math.Abs(x - lastPoint.X) < deadZone ? lastPoint.X : x;
        double targetY = Math.Abs(y - lastPoint.Y) < deadZone ? lastPoint.Y : y;

        double filteredX = lastPoint.X + ((targetX - lastPoint.X) * alpha);
        double filteredY = lastPoint.Y + ((targetY - lastPoint.Y) * alpha);
        var filtered = new Point(Math.Clamp(filteredX, 0.0, 1.0), Math.Clamp(filteredY, 0.0, 1.0));
        _smoothedZonePoints[zone] = filtered;
        return (filtered.X, filtered.Y);
    }

}
