using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TabletMidiPad;

public partial class MainWindow
{
    private void Pad_StylusDown(object sender, StylusDownEventArgs e)
    {
        if (_useWintab || !AllowsStylusMouseFallback()) return;
        e.Handled = true;
        _isActive = true;
        Pad.CaptureStylus();
        HandleStylus(e);
    }

    private void Pad_StylusMove(object sender, StylusEventArgs e)
    {
        if (_useWintab || !AllowsStylusMouseFallback()) return;
        if (!_isActive) return;
        e.Handled = true;
        HandleStylus(e);
    }

    private void Pad_StylusUp(object sender, StylusEventArgs e)
    {
        if (_useWintab || !AllowsStylusMouseFallback()) return;
        if (!_isActive) return;
        e.Handled = true;
        HandleStylus(e);
        _isActive = false;
        Pad.ReleaseStylusCapture();
        CursorDot.Visibility = Visibility.Collapsed;
        HandleRelease();
    }

    private void Pad_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (IsPromotedStylusMouseEvent(e)) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(Pad);
        SelectZoneAtPoint(pos);

        var hit = HitTestZoneEdge(pos);
        if (hit.zone >= 0)
        {
            StartZoneResize(hit.zone, hit.edge, pos);
            Pad.CaptureMouse();
            e.Handled = true;
            return;
        }

        if (_useWintab || !AllowsStylusMouseFallback()) return;
        _isActive = true;
        Pad.CaptureMouse();
        HandleMouse(e);
    }

    private void Pad_MouseMove(object sender, MouseEventArgs e)
    {
        if (IsPromotedStylusMouseEvent(e)) return;

        if (_isResizingZone)
        {
            UpdateZoneResize(e.GetPosition(Pad));
            e.Handled = true;
            return;
        }

        // Update cursor for resize hints
        var pos = e.GetPosition(Pad);
        var hit = HitTestZoneEdge(pos);
        Pad.Cursor = ResizeEdgeToCursor(hit.edge);

        if (_useWintab || !AllowsStylusMouseFallback()) return;
        if (!_isActive || e.LeftButton != MouseButtonState.Pressed) return;
        HandleMouse(e);
    }

    private void Pad_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (IsPromotedStylusMouseEvent(e)) return;

        if (_isResizingZone)
        {
            FinishZoneResize();
            Pad.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        if (_useWintab || !AllowsStylusMouseFallback()) return;
        if (!_isActive) return;
        HandleMouse(e);
        _isActive = false;
        Pad.ReleaseMouseCapture();
        CursorDot.Visibility = Visibility.Collapsed;
        HandleRelease();
    }

    private (int zone, ResizeEdge edge) HitTestZoneEdge(Point pos)
    {
        double width = Pad.ActualWidth;
        double height = Pad.ActualHeight;
        if (width <= 0 || height <= 0)
            return (-1, ResizeEdge.None);

        if (_selectedZoneIndex < 0 || _selectedZoneIndex >= _config.Zones.Count)
        {
            return (-1, ResizeEdge.None);
        }

        int i = _selectedZoneIndex;
        var zone = _config.Zones[i];
        double zl = zone.Left * width;
        double zt = zone.Top * height;
        double zr = (zone.Left + zone.Width) * width;
        double zb = (zone.Top + zone.Height) * height;

        if (pos.X < zl - ResizeHitMargin || pos.X > zr + ResizeHitMargin ||
            pos.Y < zt - ResizeHitMargin || pos.Y > zb + ResizeHitMargin)
            return (-1, ResizeEdge.None);

        ResizeEdge edge = ResizeEdge.None;
        if (Math.Abs(pos.X - zl) <= ResizeHitMargin) edge |= ResizeEdge.Left;
        else if (Math.Abs(pos.X - zr) <= ResizeHitMargin) edge |= ResizeEdge.Right;
        if (Math.Abs(pos.Y - zt) <= ResizeHitMargin) edge |= ResizeEdge.Top;
        else if (Math.Abs(pos.Y - zb) <= ResizeHitMargin) edge |= ResizeEdge.Bottom;

        return edge != ResizeEdge.None ? (i, edge) : (-1, ResizeEdge.None);
    }

    private static Cursor ResizeEdgeToCursor(ResizeEdge edge)
    {
        return edge switch
        {
            ResizeEdge.Left or ResizeEdge.Right => Cursors.SizeWE,
            ResizeEdge.Top or ResizeEdge.Bottom => Cursors.SizeNS,
            ResizeEdge.Left | ResizeEdge.Top => Cursors.SizeNWSE,
            ResizeEdge.Right | ResizeEdge.Bottom => Cursors.SizeNWSE,
            ResizeEdge.Left | ResizeEdge.Bottom => Cursors.SizeNESW,
            ResizeEdge.Right | ResizeEdge.Top => Cursors.SizeNESW,
            _ => null!
        };
    }

    private void StartZoneResize(int zoneIndex, ResizeEdge edge, Point pos)
    {
        _isResizingZone = true;
        _resizeZoneIndex = zoneIndex;
        _resizeEdge = edge;
        _resizeDragStart = pos;

        var zone = _config.Zones[zoneIndex];
        _resizeOrigLeft = zone.Left;
        _resizeOrigTop = zone.Top;
        _resizeOrigWidth = zone.Width;
        _resizeOrigHeight = zone.Height;

        // Select this zone in the editor
        if (_selectedZoneIndex != zoneIndex)
        {
            CommitCurrentZoneEdits();
            _selectedZoneIndex = zoneIndex;
            ZoneListBox.SelectedIndex = zoneIndex;
            LoadSelectedZoneIntoEditor();
        }
    }

    private void SelectZoneAtPoint(Point pos)
    {
        int zoneIndex = HitTestZoneBody(pos);
        if (zoneIndex < 0 || zoneIndex == _selectedZoneIndex)
        {
            return;
        }

        CommitCurrentZoneEdits();
        _selectedZoneIndex = zoneIndex;
        ZoneListBox.SelectedIndex = zoneIndex;
        LoadSelectedZoneIntoEditor();
        DrawZoneOverlay();
    }

    private int HitTestZoneBody(Point pos)
    {
        double width = Pad.ActualWidth;
        double height = Pad.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return -1;
        }

        for (int i = 0; i < _config.Zones.Count; i++)
        {
            var zone = _config.Zones[i];
            double insetX = Math.Min(ZoneSelectionInset, Math.Max(0.0, (zone.Width / 2.0) - 0.001));
            double insetY = Math.Min(ZoneSelectionInset, Math.Max(0.0, (zone.Height / 2.0) - 0.001));
            double zl = (zone.Left + insetX) * width;
            double zt = (zone.Top + insetY) * height;
            double zr = ((zone.Left + zone.Width) - insetX) * width;
            double zb = ((zone.Top + zone.Height) - insetY) * height;

            if (pos.X >= zl && pos.X <= zr && pos.Y >= zt && pos.Y <= zb)
            {
                return i;
            }
        }

        return -1;
    }

    private void UpdateZoneResize(Point pos)
    {
        if (_resizeZoneIndex < 0 || _resizeZoneIndex >= _config.Zones.Count)
            return;

        double width = Pad.ActualWidth;
        double height = Pad.ActualHeight;
        if (width <= 0 || height <= 0) return;

        double dxNorm = (pos.X - _resizeDragStart.X) / width;
        double dyNorm = (pos.Y - _resizeDragStart.Y) / height;

        const double snap = 0.05;
        const double minSize = 0.05;
        double newLeft = _resizeOrigLeft;
        double newTop = _resizeOrigTop;
        double newWidth = _resizeOrigWidth;
        double newHeight = _resizeOrigHeight;

        if ((_resizeEdge & ResizeEdge.Left) != 0)
        {
            newLeft = _inputMappingService.Snap(Math.Clamp(_resizeOrigLeft + dxNorm, 0.0, _resizeOrigLeft + _resizeOrigWidth - minSize), snap);
            newWidth = (_resizeOrigLeft + _resizeOrigWidth) - newLeft;
        }
        else if ((_resizeEdge & ResizeEdge.Right) != 0)
        {
            double newRight = _inputMappingService.Snap(Math.Clamp(_resizeOrigLeft + _resizeOrigWidth + dxNorm, _resizeOrigLeft + minSize, 1.0), snap);
            newWidth = newRight - _resizeOrigLeft;
        }

        if ((_resizeEdge & ResizeEdge.Top) != 0)
        {
            newTop = _inputMappingService.Snap(Math.Clamp(_resizeOrigTop + dyNorm, 0.0, _resizeOrigTop + _resizeOrigHeight - minSize), snap);
            newHeight = (_resizeOrigTop + _resizeOrigHeight) - newTop;
        }
        else if ((_resizeEdge & ResizeEdge.Bottom) != 0)
        {
            double newBottom = _inputMappingService.Snap(Math.Clamp(_resizeOrigTop + _resizeOrigHeight + dyNorm, _resizeOrigTop + minSize, 1.0), snap);
            newHeight = newBottom - _resizeOrigTop;
        }

        newLeft = Math.Clamp(newLeft, 0.0, 1.0);
        newTop = Math.Clamp(newTop, 0.0, 1.0);
        newWidth = _inputMappingService.Snap(Math.Clamp(newWidth, minSize, 1.0 - newLeft), snap);
        newHeight = _inputMappingService.Snap(Math.Clamp(newHeight, minSize, 1.0 - newTop), snap);

        if (newLeft + newWidth > 1.0)
        {
            newWidth = 1.0 - newLeft;
        }

        if (newTop + newHeight > 1.0)
        {
            newHeight = 1.0 - newTop;
        }

        if (!CanResizeZoneTo(_resizeZoneIndex, newLeft, newTop, newWidth, newHeight))
        {
            return;
        }

        var zone = _config.Zones[_resizeZoneIndex];
        zone.Left = newLeft;
        zone.Top = newTop;
        zone.Width = newWidth;
        zone.Height = newHeight;

        LoadSelectedZoneIntoEditor();
        DrawZoneOverlay();
    }

    private void FinishZoneResize()
    {
        _isResizingZone = false;
        _resizeZoneIndex = -1;
        _resizeEdge = ResizeEdge.None;
        Pad.Cursor = null!;
        _viewModel.SettingsResultBrush = Brushes.DarkSeaGreen;
        _viewModel.SettingsResultText = "Zone resized. Click Apply Settings to save.";
    }

    private bool CanResizeZoneTo(int zoneIndex, double left, double top, double width, double height)
    {
        const double minSize = 0.05;
        if (width < minSize || height < minSize)
        {
            return false;
        }

        if (left < 0.0 || top < 0.0 || left + width > 1.0 || top + height > 1.0)
        {
            return false;
        }

        foreach (var other in _config.Zones.Select((zone, index) => (zone, index)))
        {
            if (other.index == zoneIndex)
            {
                continue;
            }

            if (ZonesOverlap(left, top, width, height, other.zone.Left, other.zone.Top, other.zone.Width, other.zone.Height))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ZonesOverlap(
        double leftA,
        double topA,
        double widthA,
        double heightA,
        double leftB,
        double topB,
        double widthB,
        double heightB)
    {
        double rightA = leftA + widthA;
        double bottomA = topA + heightA;
        double rightB = leftB + widthB;
        double bottomB = topB + heightB;

        return leftA < rightB &&
               rightA > leftB &&
               topA < bottomB &&
               bottomA > topB;
    }

    private void HandleStylus(StylusEventArgs e)
    {
        MarkInputBackend("Stylus");
        _viewModel.PenButtonsText = "Pen buttons: stylus fallback";
        var point = e.GetPosition(Pad);
        if (!TryGetPadCoordinates(point, out double xNorm, out double yNorm))
        {
            return;
        }

        int pressure = _config.MousePressureValue;
        var stylusPoints = e.GetStylusPoints(Pad);
        if (stylusPoints.Count > 0)
        {
            var sp = stylusPoints[0];
            if (sp.HasProperty(StylusPointProperties.NormalPressure))
            {
                int raw = sp.GetPropertyValue(StylusPointProperties.NormalPressure);
                int max = sp.Description.GetPropertyInfo(StylusPointProperties.NormalPressure).Maximum;
                pressure = max > 0 ? (int)Math.Round(raw * 127.0 / max) : _config.MousePressureValue;
            }
        }

        HandlePoint(point.X, point.Y, xNorm, yNorm, pressure);
    }

    private void HandleMouse(MouseEventArgs e)
    {
        MarkInputBackend("Mouse");
        _viewModel.PenButtonsText = "Pen buttons: mouse fallback";
        var point = e.GetPosition(Pad);
        if (!TryGetPadCoordinates(point, out double xNorm, out double yNorm))
        {
            return;
        }

        HandlePoint(point.X, point.Y, xNorm, yNorm, _config.MousePressureValue);
    }

    private void HandlePoint(double xPx, double yPx, double xNorm, double yNorm, int pressure)
    {
        var zone = ResolveZoneForCurrentInteraction(xNorm, yNorm);
        if (!ReferenceEquals(zone, _activeZone))
        {
            SendZoneRelease(_activeZone);
            _activeZone = zone;
        }

        CursorDot.Visibility = Visibility.Visible;
        Canvas.SetLeft(CursorDot, xPx - CursorDot.Width / 2);
        Canvas.SetTop(CursorDot, yPx - CursorDot.Height / 2);

        if (zone is null)
        {
            _viewModel.ActiveZoneText = "Active zone: none";
            _viewModel.ValueText = "Outside configured zones";
            return;
        }

        var local = NormalizeWithinZone(zone, xNorm, yNorm);
        local = ApplyPositionConditioning(zone, local.x, local.y);
        string lowerAction = _inputMappingService.NormalizePenButtonAction(_config.LowerPenButtonAction);
        string upperAction = _inputMappingService.NormalizePenButtonAction(_config.UpperPenButtonAction);
        bool lockXY =
            (_lowerPenButtonHeld && string.Equals(lowerAction, PenButtonActions.LockXY, StringComparison.Ordinal)) ||
            (_upperPenButtonHeld && string.Equals(upperAction, PenButtonActions.LockXY, StringComparison.Ordinal));
        bool lockX =
            (_lowerPenButtonHeld && string.Equals(lowerAction, PenButtonActions.LockX, StringComparison.Ordinal)) ||
            (_upperPenButtonHeld && string.Equals(upperAction, PenButtonActions.LockX, StringComparison.Ordinal));
        bool lockY =
            (_lowerPenButtonHeld && string.Equals(lowerAction, PenButtonActions.LockY, StringComparison.Ordinal)) ||
            (_upperPenButtonHeld && string.Equals(upperAction, PenButtonActions.LockY, StringComparison.Ordinal));

        if (lockXY || lockX || lockY)
        {
            if (!_hasLockedPosition)
            {
                _lockedLocalPosition = new Point(local.x, local.y);
                _hasLockedPosition = true;
            }

            local = (
                lockXY || lockX ? _lockedLocalPosition.X : local.x,
                lockXY || lockY ? _lockedLocalPosition.Y : local.y
            );
        }
        else
        {
            _hasLockedPosition = false;
        }
        _lastZonePoints[zone] = new Point(local.x, local.y);
        DrawZoneOverlay();
        int x = _inputMappingService.ToCc(local.x, zone.InvertX);
        int y = _inputMappingService.ToCc(local.y, zone.InvertY);
        int curvedPressure = _inputMappingService.ApplyPressureCurve(pressure, _config.PressureCurve);

        _viewModel.ActiveZoneText = $"Active zone: {zone.Name}";
        bool sendPressure = _config.SendPressureGlobally && zone.SendPressure;
        if ((_lowerPenButtonHeld && string.Equals(lowerAction, PenButtonActions.DisablePressure, StringComparison.Ordinal)) ||
            (_upperPenButtonHeld && string.Equals(upperAction, PenButtonActions.DisablePressure, StringComparison.Ordinal)))
        {
            sendPressure = false;
        }

        bool pressureEnabledByButton =
            (_lowerPenButtonHeld && string.Equals(lowerAction, PenButtonActions.EnablePressure, StringComparison.Ordinal)) ||
            (_upperPenButtonHeld && string.Equals(upperAction, PenButtonActions.EnablePressure, StringComparison.Ordinal));
        bool anyEnablePressureAction =
            string.Equals(lowerAction, PenButtonActions.EnablePressure, StringComparison.Ordinal) ||
            string.Equals(upperAction, PenButtonActions.EnablePressure, StringComparison.Ordinal);
        if (anyEnablePressureAction)
        {
            sendPressure = sendPressure && pressureEnabledByButton;
        }

        string xText = zone.SendX ? x.ToString() : "off";
        string yText = zone.SendY ? y.ToString() : "off";
        string pressureText = sendPressure ? curvedPressure.ToString() : "off";
        _viewModel.ValueText = $"{zone.Name} -> X: {xText}, Y: {yText}, Pressure: {pressureText}";

        if (zone.SendX)
        {
            SendCcIfChanged(zone.MidiChannel, zone.CcX, x);
        }

        if (zone.SendY)
        {
            SendCcIfChanged(zone.MidiChannel, zone.CcY, y);
        }

        if (sendPressure)
        {
            SendCcIfChanged(zone.MidiChannel, zone.CcPressure, curvedPressure);
        }
    }

    private void HandleRelease()
    {
        SendZoneRelease(_activeZone);
        _activeZone = null;
        _viewModel.ActiveZoneText = "Active zone: none";
        _hasLockedPosition = false;
        _lowerPenButtonHeld = false;
        _upperPenButtonHeld = false;
        UpdateModifiersText();
    }

    private void SendZoneRelease(ZoneConfig? zone)
    {
        if (zone is null || !zone.Enabled)
        {
            return;
        }

        string behavior = _inputMappingService.NormalizeReleaseBehavior(zone.ReleaseBehavior);

        if (string.Equals(behavior, ZoneReleaseBehaviors.Hold, StringComparison.Ordinal) ||
            string.Equals(behavior, ZoneReleaseBehaviors.NoSendOnRelease, StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(behavior, ZoneReleaseBehaviors.ResetAll, StringComparison.Ordinal))
        {
            if (zone.SendX)
            {
                SendCcIfChanged(zone.MidiChannel, zone.CcX, 0, force: true);
            }

            if (zone.SendY)
            {
                SendCcIfChanged(zone.MidiChannel, zone.CcY, 0, force: true);
            }
        }

        if (_config.SendPressureGlobally && zone.SendPressure)
        {
            SendCcIfChanged(zone.MidiChannel, zone.CcPressure, 0, force: true);
        }
    }

    private ZoneConfig? FindMatchingZone(double xNorm, double yNorm)
    {
        return _config.Zones.FirstOrDefault(zone =>
            zone.Enabled &&
            xNorm >= zone.Left &&
            xNorm <= zone.Left + zone.Width &&
            yNorm >= zone.Top &&
            yNorm <= zone.Top + zone.Height
        );
    }

    private ZoneConfig? ResolveZoneForCurrentInteraction(double xNorm, double yNorm)
    {
        if (!_config.AllowZoneSwitchWhilePressed && _activeZone is not null)
        {
            return _activeZone;
        }

        return FindMatchingZone(xNorm, yNorm);
    }

    private static (double x, double y) NormalizeWithinZone(ZoneConfig zone, double xNorm, double yNorm)
    {
        double x = (xNorm - zone.Left) / zone.Width;
        double y = (yNorm - zone.Top) / zone.Height;
        return (Math.Clamp(x, 0.0, 1.0), Math.Clamp(y, 0.0, 1.0));
    }

    private bool TryGetPadCoordinates(Point point, out double xNorm, out double yNorm)
    {
        xNorm = 0;
        yNorm = 0;

        if (Pad.ActualWidth <= 0 || Pad.ActualHeight <= 0)
        {
            return false;
        }

        xNorm = point.X / Pad.ActualWidth;
        yNorm = point.Y / Pad.ActualHeight;
        return true;
    }

}
