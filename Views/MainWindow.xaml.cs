using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using TabletMidiPad.Infrastructure;
using TabletMidiPad.Models;
using TabletMidiPad.Services;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Views;

public partial class MainWindow : Window
{
    private readonly ITabletService _tabletService;
    private MainViewModel ViewModel => (MainViewModel)DataContext;
    private HwndSource? _wintabMessageSource;

    private const double ResizeHitMargin = 8.0;
    private const double ZoneSelectionInset = 0.02;
    private bool _isResizingZone;
    private int _resizeZoneIndex = -1;
    private ResizeEdge _resizeEdge;
    private Point _resizeDragStart;
    private double _resizeOrigLeft, _resizeOrigTop, _resizeOrigWidth, _resizeOrigHeight;
    private bool _isActive;

    [Flags]
    private enum ResizeEdge { None = 0, Left = 1, Right = 2, Top = 4, Bottom = 8 }

    public MainWindow(MainViewModel viewModel, ITabletService tabletService)
    {
        _tabletService = tabletService;
        InitializeComponent();
        DataContext = viewModel;
        WindowChromeTheme.ApplyDarkNonClientChrome(this);
        ContentRendered += OnContentRendered;
        Closed += (_, _) => { ViewModel.HandleRelease(); };
    }

    private async void OnContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= OnContentRendered;
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        _wintabMessageSource = HwndSource.FromHwnd(hwnd);
        _wintabMessageSource?.AddHook(_tabletService.HandleWndProc);
        await ViewModel.InitializeAsync(hwnd);
        ViewModel.PropertyChanged += (s, ev) => { if (ev.PropertyName == nameof(MainViewModel.Zones) || ev.PropertyName == nameof(MainViewModel.SelectedZoneIndex)) DrawZoneOverlay(); };
        DrawZoneOverlay();
    }

    private void MenuRefreshWacom_Click(object sender, RoutedEventArgs e) { /* Re-check service status if needed, or bind to VM */ }

    private void MenuPreferences_Click(object sender, RoutedEventArgs e)
    {
        ((App)Application.Current).ShowPreferences();
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e) => Close();

    private void MenuAbout_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Tablet MIDI Pad\n\nTurns a Wacom tablet into a MIDI XY controller with configurable zones.", "About Tablet MIDI Pad", MessageBoxButton.OK, MessageBoxImage.Information);

    private void ToolbarPressureCurveDropdown_Click(object sender, RoutedEventArgs e) { if (sender is Button b && b.ContextMenu != null) { b.ContextMenu.PlacementTarget = b; b.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom; b.ContextMenu.IsOpen = true; } }

    private void ToolbarInputBackendDropdown_Click(object sender, RoutedEventArgs e) { if (sender is Button b && b.ContextMenu != null) { b.ContextMenu.PlacementTarget = b; b.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom; b.ContextMenu.IsOpen = true; } }

    private void Pad_SizeChanged(object sender, SizeChangedEventArgs e) => DrawZoneOverlay();

    private void Pad_StylusDown(object sender, StylusDownEventArgs e) { e.Handled = true; _isActive = true; Pad.CaptureStylus(); HandleStylus(e); }
    private void Pad_StylusMove(object sender, StylusEventArgs e) { if (!_isActive) return; e.Handled = true; HandleStylus(e); }
    private void Pad_StylusUp(object sender, StylusEventArgs e) { if (!_isActive) return; e.Handled = true; HandleStylus(e); _isActive = false; Pad.ReleaseStylusCapture(); CursorDot.Visibility = Visibility.Collapsed; ViewModel.HandleRelease(); }

    private void HandleStylus(StylusEventArgs e)
    {
        var pos = e.GetPosition(Pad);
        if (Pad.ActualWidth <= 0 || Pad.ActualHeight <= 0) return;
        int pressure = 127;
        var points = e.GetStylusPoints(Pad);
        if (points.Count > 0)
        {
            var sp = points[0];
            if (sp.HasProperty(StylusPointProperties.NormalPressure))
            {
                int raw = sp.GetPropertyValue(StylusPointProperties.NormalPressure);
                int max = sp.Description.GetPropertyInfo(StylusPointProperties.NormalPressure).Maximum;
                pressure = max > 0 ? (int)Math.Round(raw * 127.0 / max) : 127;
            }
        }
        UpdateInteraction(pos, pressure, "Stylus");
    }

    private void Pad_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.StylusDevice != null) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(Pad);
        var hit = HitTestZoneEdge(pos);
        if (hit.zone >= 0) { StartZoneResize(hit.zone, hit.edge, pos); Pad.CaptureMouse(); e.Handled = true; return; }

        int zoneIndex = HitTestZoneBody(pos);
        if (zoneIndex >= 0) ViewModel.SelectedZoneIndex = zoneIndex;

        _isActive = true; Pad.CaptureMouse(); HandleMouse(e);
    }

    private void Pad_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.StylusDevice != null) return;
        var pos = e.GetPosition(Pad);
        if (_isResizingZone) { UpdateZoneResize(pos); e.Handled = true; return; }
        var hit = HitTestZoneEdge(pos);
        Pad.Cursor = ResizeEdgeToCursor(hit.edge);
        if (!_isActive || e.LeftButton != MouseButtonState.Pressed) return;
        HandleMouse(e);
    }

    private void Pad_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.StylusDevice != null) return;
        if (_isResizingZone) { FinishZoneResize(); Pad.ReleaseMouseCapture(); e.Handled = true; return; }
        if (!_isActive) return;
        HandleMouse(e); _isActive = false; Pad.ReleaseMouseCapture(); CursorDot.Visibility = Visibility.Collapsed; ViewModel.HandleRelease();
    }

    private void HandleMouse(MouseEventArgs e)
    {
        var pos = e.GetPosition(Pad);
        if (Pad.ActualWidth <= 0 || Pad.ActualHeight <= 0) return;
        UpdateInteraction(pos, ViewModel.Config.MousePressureValue, "Mouse");
    }

    private void UpdateInteraction(Point pos, int pressure, string backend)
    {
        double xNorm = Math.Clamp(pos.X / Pad.ActualWidth, 0, 1);
        double yNorm = Math.Clamp(pos.Y / Pad.ActualHeight, 0, 1);
        ViewModel.OnPoint(xNorm, yNorm, pressure, backend);

        CursorDot.Visibility = Visibility.Visible;
        Canvas.SetLeft(CursorDot, pos.X - CursorDot.Width / 2);
        Canvas.SetTop(CursorDot, pos.Y - CursorDot.Height / 2);
        DrawZoneOverlay();
    }

    private void DrawZoneOverlay()
    {
        ZoneOverlayCanvas.Children.Clear();
        double w = Pad.ActualWidth, h = Pad.ActualHeight;
        if (w <= 0 || h <= 0) return;
        ZoneOverlayCanvas.Width = w; ZoneOverlayCanvas.Height = h;

        for (int i = 0; i < ViewModel.Config.Zones.Count; i++)
        {
            var zone = ViewModel.Config.Zones[i];
            double rl = zone.Left * w, rt = zone.Top * h, rw = zone.Width * w, rh = zone.Height * h;
            bool selected = i == ViewModel.SelectedZoneIndex;
            var color = GetZonePaletteColor(i);
            var rect = new Rectangle { Width = rw, Height = rh, StrokeThickness = 2, RadiusX = 8, RadiusY = 8,
                Fill = new SolidColorBrush(Color.FromArgb(selected ? (byte)100 : (byte)56, color.R, color.G, color.B)),
                Stroke = new SolidColorBrush(Color.FromArgb(selected ? (byte)240 : (byte)170, color.R, color.G, color.B)) };
            Canvas.SetLeft(rect, rl); Canvas.SetTop(rect, rt);
            ZoneOverlayCanvas.Children.Add(rect);
            if (selected) AddResizeHandles(rl, rt, rw, rh, color);
        }
    }

    private static Color GetZonePaletteColor(int index)
    {
        var colors = new[] { Color.FromRgb(0x67, 0xD6, 0x8A), Color.FromRgb(0x4D, 0xAF, 0xFF), Color.FromRgb(0xFF, 0x8C, 0x00), Color.FromRgb(0xFF, 0x4D, 0x4D), Color.FromRgb(0xBD, 0x93, 0xF9), Color.FromRgb(0xFF, 0x79, 0xC6) };
        return colors[index % colors.Length];
    }

    private void AddResizeHandles(double rl, double rt, double rw, double rh, Color color)
    {
        const double sz = 10; double h = sz / 2;
        var brush = new SolidColorBrush(Color.FromArgb(235, color.R, color.G, color.B));
        foreach (var p in new[] { new Point(rl, rt), new Point(rl + rw / 2, rt), new Point(rl + rw, rt), new Point(rl, rt + rh / 2), new Point(rl + rw, rt + rh / 2), new Point(rl, rt + rh), new Point(rl + rw / 2, rt + rh), new Point(rl + rw, rt + rh) })
        {
            var r = new Rectangle { Width = sz, Height = sz, RadiusX = 3, RadiusY = 3, Fill = Brushes.Black, Stroke = brush, StrokeThickness = 1.5 };
            Canvas.SetLeft(r, p.X - h); Canvas.SetTop(r, p.Y - h);
            ZoneOverlayCanvas.Children.Add(r);
        }
    }

    private (int zone, ResizeEdge edge) HitTestZoneEdge(Point pos)
    {
        if (ViewModel.SelectedZoneIndex < 0) return (-1, ResizeEdge.None);
        var z = ViewModel.Config.Zones[ViewModel.SelectedZoneIndex];
        double w = Pad.ActualWidth, h = Pad.ActualHeight;
        double zl = z.Left * w, zt = z.Top * h, zr = (z.Left + z.Width) * w, zb = (z.Top + z.Height) * h;
        if (pos.X < zl - ResizeHitMargin || pos.X > zr + ResizeHitMargin || pos.Y < zt - ResizeHitMargin || pos.Y > zb + ResizeHitMargin) return (-1, ResizeEdge.None);
        ResizeEdge edge = ResizeEdge.None;
        if (Math.Abs(pos.X - zl) <= ResizeHitMargin) edge |= ResizeEdge.Left; else if (Math.Abs(pos.X - zr) <= ResizeHitMargin) edge |= ResizeEdge.Right;
        if (Math.Abs(pos.Y - zt) <= ResizeHitMargin) edge |= ResizeEdge.Top; else if (Math.Abs(pos.Y - zb) <= ResizeHitMargin) edge |= ResizeEdge.Bottom;
        return edge != ResizeEdge.None ? (ViewModel.SelectedZoneIndex, edge) : (-1, ResizeEdge.None);
    }

    private int HitTestZoneBody(Point pos)
    {
        double w = Pad.ActualWidth, h = Pad.ActualHeight;
        for (int i = 0; i < ViewModel.Config.Zones.Count; i++) {
            var z = ViewModel.Config.Zones[i];
            double ix = Math.Min(ZoneSelectionInset, z.Width / 2), iy = Math.Min(ZoneSelectionInset, z.Height / 2);
            if (pos.X >= (z.Left + ix) * w && pos.X <= (z.Left + z.Width - ix) * w && pos.Y >= (z.Top + iy) * h && pos.Y <= (z.Top + z.Height - iy) * h) return i;
        }
        return -1;
    }

    private void StartZoneResize(int index, ResizeEdge edge, Point pos)
    {
        _isResizingZone = true; _resizeZoneIndex = index; _resizeEdge = edge; _resizeDragStart = pos;
        var z = ViewModel.Config.Zones[index]; _resizeOrigLeft = z.Left; _resizeOrigTop = z.Top; _resizeOrigWidth = z.Width; _resizeOrigHeight = z.Height;
        ViewModel.SelectedZoneIndex = index;
    }

    private void UpdateZoneResize(Point pos)
    {
        if (_resizeZoneIndex < 0) return;
        double w = Pad.ActualWidth, h = Pad.ActualHeight; if (w <= 0 || h <= 0) return;
        double dx = (pos.X - _resizeDragStart.X) / w, dy = (pos.Y - _resizeDragStart.Y) / h;
        const double snap = 0.05, min = 0.05;
        var z = ViewModel.Config.Zones[_resizeZoneIndex];
        if ((_resizeEdge & ResizeEdge.Left) != 0) { z.Left = Math.Round((_resizeOrigLeft + dx) / snap) * snap; z.Width = _resizeOrigLeft + _resizeOrigWidth - z.Left; }
        else if ((_resizeEdge & ResizeEdge.Right) != 0) { z.Width = Math.Round((_resizeOrigWidth + dx) / snap) * snap; }
        if ((_resizeEdge & ResizeEdge.Top) != 0) { z.Top = Math.Round((_resizeOrigTop + dy) / snap) * snap; z.Height = _resizeOrigTop + _resizeOrigHeight - z.Top; }
        else if ((_resizeEdge & ResizeEdge.Bottom) != 0) { z.Height = Math.Round((_resizeOrigHeight + dy) / snap) * snap; }
        z.Width = Math.Max(min, z.Width); z.Height = Math.Max(min, z.Height);
        DrawZoneOverlay();
    }

    private void FinishZoneResize() { _isResizingZone = false; Pad.Cursor = null; DrawZoneOverlay(); }
    private static Cursor ResizeEdgeToCursor(ResizeEdge edge) => edge switch { ResizeEdge.Left or ResizeEdge.Right => Cursors.SizeWE, ResizeEdge.Top or ResizeEdge.Bottom => Cursors.SizeNS, ResizeEdge.Left | ResizeEdge.Top => Cursors.SizeNWSE, ResizeEdge.Right | ResizeEdge.Bottom => Cursors.SizeNWSE, ResizeEdge.Left | ResizeEdge.Bottom => Cursors.SizeNESW, ResizeEdge.Right | ResizeEdge.Top => Cursors.SizeNESW, _ => Cursors.Arrow };
}
