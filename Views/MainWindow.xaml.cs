using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Extensions.DependencyInjection;
using TabletMidiPad.Behaviors;
using TabletMidiPad.Infrastructure;
using TabletMidiPad.Models;
using TabletMidiPad.Services;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ITabletService _tabletService;
    private const string WacomServiceName = "WTabletServicePro";

    private readonly Dictionary<ZoneConfig, Point> _lastZonePoints = new();
    private bool _isLoadingUi;
    private int _selectedZoneIndex = -1;

    private const double ResizeHitMargin = 8.0;
    private bool _isResizingZone;
    private int _resizeZoneIndex = -1;
    private ResizeEdge _resizeEdge;
    private Point _resizeDragStart;
    private double _resizeOrigLeft, _resizeOrigTop, _resizeOrigWidth, _resizeOrigHeight;

    [Flags]
    private enum ResizeEdge { None = 0, Left = 1, Right = 2, Top = 4, Bottom = 8 }

    public MainWindow(MainViewModel viewModel, ITabletService tabletService)
    {
        _viewModel = viewModel;
        _tabletService = tabletService;
        DataContext = _viewModel;
        InitializeComponent();
        WindowChromeTheme.ApplyDarkNonClientChrome(this);
        ConfigureNumericInputs();
        ContentRendered += OnContentRendered;

        _tabletService.PacketReceived += (p, i) => {
            var zone = _viewModel.Zones.FirstOrDefault(z => z.Enabled && (p.X/(double)i.OutputMaxX) >= z.Left && (p.X/(double)i.OutputMaxX) <= z.Left + z.Width && (p.Y/(double)i.OutputMaxY) >= z.Top && (p.Y/(double)i.OutputMaxY) <= z.Top + z.Height);
            if (zone != null) {
                _lastZonePoints[zone] = new Point((p.X/(double)i.OutputMaxX - zone.Left)/zone.Width, (p.Y/(double)i.OutputMaxY - zone.Top)/zone.Height);
                Dispatcher.Invoke(DrawZoneOverlay);
            }
        };

        Closed += (_, _) => {
            App.ServiceProvider.GetRequiredService<IMidiService>().Shutdown();
            _tabletService.Shutdown();
        };
    }

    private void ConfigureNumericInputs()
    {
        foreach (TextBox textBox in new[] { MaxZoneRowsTextBox, MaxZonesPerRowTextBox, ZoneCcXTextBox, ZoneCcYTextBox, ZoneCcPressureTextBox })
        {
            NumericTextBoxBehavior.SetMode(textBox, NumericTextMode.Integer);
            textBox.HorizontalContentAlignment = HorizontalAlignment.Right;
        }
        foreach (TextBox textBox in new[] { ZoneLeftTextBox, ZoneTopTextBox, ZoneWidthTextBox, ZoneHeightTextBox })
        {
            NumericTextBoxBehavior.SetMode(textBox, NumericTextMode.Decimal);
            textBox.HorizontalContentAlignment = HorizontalAlignment.Right;
        }
    }

    private async void OnContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= OnContentRendered;
        PopulateChannelList(ZoneChannelComboBox);
        PopulateReleaseBehaviorList(ZoneReleaseBehaviorComboBox);
        LoadConfigIntoUi(_viewModel.Config);
        RefreshWacomServiceStatus();
        PopulateTabletDeviceList();

        var source = (HwndSource)PresentationSource.FromVisual(this);
        if (source != null) source.AddHook(_tabletService.HandleWndProc);

        await _viewModel.InitializeAsync(new WindowInteropHelper(this).Handle);
    }

    private static void PopulateChannelList(ComboBox cb) { cb.Items.Clear(); for (int i = 1; i <= 16; i++) cb.Items.Add(i); }
    private static void PopulateReleaseBehaviorList(ComboBox cb) { cb.Items.Clear(); cb.Items.Add(ZoneReleaseBehaviors.NoSendOnRelease); cb.Items.Add(ZoneReleaseBehaviors.Hold); cb.Items.Add(ZoneReleaseBehaviors.ResetPressureOnly); cb.Items.Add(ZoneReleaseBehaviors.ResetAll); }

    public void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e) => ApplySettings();
    public void MenuSaveSettings_Click(object sender, RoutedEventArgs e) => ApplySettings();
    public void MenuResetDefaults_Click(object sender, RoutedEventArgs e) { _viewModel.ResetToDefaults(); LoadConfigIntoUi(_viewModel.Config); ApplySettings(); }
    public void MenuPreferences_Click(object sender, RoutedEventArgs e)
    {
        var factory = App.ServiceProvider.GetRequiredService<Func<int, PreferencesWindow>>();
        var prefs = factory(_tabletService.GetExpressKeyCount());
        prefs.Owner = this;
        prefs.LoadFrom(_viewModel.Config);
        if (prefs.ShowDialog() == true) {
            CommitCurrentZoneEdits();
            _viewModel.LoadFromConfig();
            _viewModel.RestartMidiAsync();
            SyncToolbar();
        }
    }

    public void MenuExit_Click(object sender, RoutedEventArgs e) => Close();
    public void MenuRefreshWacom_Click(object sender, RoutedEventArgs e) => RefreshWacomServiceStatus();
    public void MenuReconnectPen_Click(object sender, RoutedEventArgs e) { _tabletService.Reinitialize(); }

    public async void MenuStartWacomService_Click(object sender, RoutedEventArgs e)
    {
        MenuStartWacomService.IsEnabled = false;
        try { await Task.Run(() => {
            using var service = new ServiceController(WacomServiceName);
            if (service.Status != ServiceControllerStatus.Running) { service.Start(); service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10)); }
        }); }
        catch { }
        finally { RefreshWacomServiceStatus(); _tabletService.Reinitialize(); }
    }

    private void RefreshWacomServiceStatus()
    {
        try {
            using var service = new ServiceController(WacomServiceName);
            MenuWacomServiceStatus.Header = $"Service: {service.Status}";
            MenuStartWacomService.IsEnabled = service.Status != ServiceControllerStatus.Running;
        } catch { MenuWacomServiceStatus.Header = "Service: not found"; MenuStartWacomService.IsEnabled = false; }
    }

    public void ToolbarPressureToggle_Click(object sender, RoutedEventArgs e) { _viewModel.Config.SendPressureGlobally = ToolbarPressureToggle.IsChecked == true; SyncToolbar(); }
    public void ToolbarZoneSwitchToggle_Click(object sender, RoutedEventArgs e) { _viewModel.Config.AllowZoneSwitchWhilePressed = ToolbarZoneSwitchToggle.IsChecked == true; }
    public void ToolbarPressureCurveDropdown_Click(object sender, RoutedEventArgs e) { if (sender is Button b && b.ContextMenu != null) { b.ContextMenu.PlacementTarget = b; b.ContextMenu.IsOpen = true; } }

    private void SetPressureCurve(string curve) { _viewModel.Config.PressureCurve = curve; SyncToolbar(); }
    public void PressureCurveLinear_Click(object sender, RoutedEventArgs e) => SetPressureCurve("Linear");
    public void PressureCurveLogarithmic_Click(object sender, RoutedEventArgs e) => SetPressureCurve("Logarithmic");
    public void PressureCurveExponential_Click(object sender, RoutedEventArgs e) => SetPressureCurve("Exponential");

    public void ToolbarInputBackendCycle_Click(object sender, RoutedEventArgs e) {
        _viewModel.Config.InputBackendMode = _viewModel.Config.InputBackendMode switch { "Wintab Only" => "Stylus/Mouse Only", "Stylus/Mouse Only" => "Auto", _ => "Wintab Only" };
        _tabletService.InputBackendMode = _viewModel.Config.InputBackendMode;
        _tabletService.Reinitialize();
        SyncToolbar();
    }
    public void ToolbarInputBackendDropdown_Click(object sender, RoutedEventArgs e) { if (sender is Button b && b.ContextMenu != null) { b.ContextMenu.PlacementTarget = b; b.ContextMenu.IsOpen = true; } }
    public void InputBackendAuto_Click(object sender, RoutedEventArgs e) { _viewModel.Config.InputBackendMode = "Auto"; _tabletService.InputBackendMode = "Auto"; _tabletService.Reinitialize(); SyncToolbar(); }
    public void InputBackendWintab_Click(object sender, RoutedEventArgs e) { _viewModel.Config.InputBackendMode = "Wintab Only"; _tabletService.InputBackendMode = "Wintab Only"; _tabletService.Reinitialize(); SyncToolbar(); }
    public void InputBackendStylus_Click(object sender, RoutedEventArgs e) { _viewModel.Config.InputBackendMode = "Stylus/Mouse Only"; _tabletService.InputBackendMode = "Stylus/Mouse Only"; _tabletService.Reinitialize(); SyncToolbar(); }

    public void MenuAbout_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Tablet MIDI Pad", "About");

    private void SyncToolbar() {
        ToolbarPressureToggle.IsChecked = _viewModel.Config.SendPressureGlobally;
        ToolbarZoneSwitchToggle.IsChecked = _viewModel.Config.AllowZoneSwitchWhilePressed;
        ToolbarPressureLabel.Text = $"Pressure ({(_viewModel.Config.PressureCurve.Length >= 3 ? _viewModel.Config.PressureCurve.Substring(0, 3) : _viewModel.Config.PressureCurve)})";
        ToolbarInputBackendLabel.Text = $"Input ({_viewModel.Config.InputBackendMode})";
    }

    public void AddZoneButton_Click(object sender, RoutedEventArgs e)
    {
        CommitCurrentZoneEdits();
        var zone = ZoneConfig.CreateDefault();
        zone.Name = $"Zone {_viewModel.Zones.Count + 1}";
        _viewModel.Zones.Add(zone);
        AutoTileZones(_viewModel.Config);
        RefreshZoneList();
        ZoneListBox.SelectedIndex = _viewModel.Zones.Count - 1;
        LoadSelectedZoneIntoEditor();
        DrawZoneOverlay();
    }

    public void RemoveZoneButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedZoneIndex >= 0 && _selectedZoneIndex < _viewModel.Zones.Count) {
            _viewModel.Zones.RemoveAt(_selectedZoneIndex);
            if (_viewModel.Zones.Count == 0) _viewModel.Zones.Add(ZoneConfig.CreateDefault());
            AutoTileZones(_viewModel.Config);
            RefreshZoneList();
            ZoneListBox.SelectedIndex = 0;
            LoadSelectedZoneIntoEditor();
            DrawZoneOverlay();
        }
    }

    public void AutoLayoutButton_Click(object sender, RoutedEventArgs e) { CommitCurrentZoneEdits(); AutoTileZones(_viewModel.Config); RefreshZoneList(); LoadSelectedZoneIntoEditor(); DrawZoneOverlay(); }

    private void ApplySettings() { CommitCurrentZoneEdits(); _viewModel.SaveSettings(); _viewModel.RestartMidiAsync(); DrawZoneOverlay(); }

    private void LoadConfigIntoUi(AppConfig config)
    {
        _isLoadingUi = true;
        MaxZoneRowsTextBox.Text = config.MaxZoneRows.ToString();
        MaxZonesPerRowTextBox.Text = config.MaxZonesPerRow.ToString();
        RefreshZoneList();
        ZoneListBox.SelectedIndex = config.Zones.Count > 0 ? 0 : -1;
        _selectedZoneIndex = ZoneListBox.SelectedIndex;
        LoadSelectedZoneIntoEditor();
        SyncToolbar();
        _isLoadingUi = false;
        DrawZoneOverlay();
    }

    private void RefreshZoneList() { ZoneListBox.Items.Clear(); for (int i = 0; i < _viewModel.Zones.Count; i++) ZoneListBox.Items.Add($"{i + 1}. {_viewModel.Zones[i].Name} [{( _viewModel.Zones[i].Enabled ? "on" : "off")}]"); }

    public void ZoneListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingUi) return;
        _selectedZoneIndex = ZoneListBox.SelectedIndex;
        LoadSelectedZoneIntoEditor();
        DrawZoneOverlay();
    }

    private void LoadSelectedZoneIntoEditor()
    {
        _isLoadingUi = true;
        if (_selectedZoneIndex < 0 || _selectedZoneIndex >= _viewModel.Zones.Count) { _isLoadingUi = false; return; }
        var zone = _viewModel.Zones[_selectedZoneIndex];
        ZoneNameTextBox.Text = zone.Name;
        ZoneEnabledCheckBox.IsChecked = zone.Enabled;
        ZoneChannelComboBox.SelectedItem = zone.MidiChannel;
        ZoneCcXTextBox.Text = zone.CcX.ToString();
        ZoneCcYTextBox.Text = zone.CcY.ToString();
        ZoneCcPressureTextBox.Text = zone.CcPressure.ToString();
        ZoneLeftTextBox.Text = zone.Left.ToString("0.###");
        ZoneTopTextBox.Text = zone.Top.ToString("0.###");
        ZoneWidthTextBox.Text = zone.Width.ToString("0.###");
        ZoneHeightTextBox.Text = zone.Height.ToString("0.###");
        ZoneSendXCheckBox.IsChecked = zone.SendX;
        ZoneSendYCheckBox.IsChecked = zone.SendY;
        ZoneInvertXCheckBox.IsChecked = zone.InvertX;
        ZoneInvertYCheckBox.IsChecked = zone.InvertY;
        ZoneSendPressureCheckBox.IsChecked = zone.SendPressure;
        ZoneReleaseBehaviorComboBox.SelectedItem = zone.ReleaseBehavior;
        _isLoadingUi = false;
    }

    private void CommitCurrentZoneEdits()
    {
        if (_selectedZoneIndex < 0 || _selectedZoneIndex >= _viewModel.Zones.Count) return;
        var zone = _viewModel.Zones[_selectedZoneIndex];
        zone.Name = ZoneNameTextBox.Text;
        zone.Enabled = ZoneEnabledCheckBox.IsChecked == true;
        if (int.TryParse(ZoneChannelComboBox.Text, out int ch)) zone.MidiChannel = ch;
        if (int.TryParse(ZoneCcXTextBox.Text, out int ccx)) zone.CcX = ccx;
        if (int.TryParse(ZoneCcYTextBox.Text, out int ccy)) zone.CcY = ccy;
        if (int.TryParse(ZoneCcPressureTextBox.Text, out int ccp)) zone.CcPressure = ccp;
        if (double.TryParse(ZoneLeftTextBox.Text, out double l)) zone.Left = l;
        if (double.TryParse(ZoneTopTextBox.Text, out double t)) zone.Top = t;
        if (double.TryParse(ZoneWidthTextBox.Text, out double w)) zone.Width = w;
        if (double.TryParse(ZoneHeightTextBox.Text, out double h)) zone.Height = h;
        zone.SendX = ZoneSendXCheckBox.IsChecked == true;
        zone.SendY = ZoneSendYCheckBox.IsChecked == true;
        zone.InvertX = ZoneInvertXCheckBox.IsChecked == true;
        zone.InvertY = ZoneInvertYCheckBox.IsChecked == true;
        zone.SendPressure = ZoneSendPressureCheckBox.IsChecked == true;
        zone.ReleaseBehavior = ZoneReleaseBehaviorComboBox.Text;
        RefreshZoneList();
    }

    public void Pad_SizeChanged(object sender, SizeChangedEventArgs e) => DrawZoneOverlay();

    private void DrawZoneOverlay()
    {
        ZoneOverlayCanvas.Children.Clear();
        double w = Pad.ActualWidth, h = Pad.ActualHeight;
        if (w <= 0 || h <= 0) return;
        for (int i = 0; i < _viewModel.Zones.Count; i++) {
            var zone = _viewModel.Zones[i];
            var rect = new Rectangle { Width = zone.Width * w, Height = zone.Height * h, Stroke = Brushes.Green, StrokeThickness = 2, Fill = new SolidColorBrush(Color.FromArgb(50, 0, 255, 0)), RadiusX = 8, RadiusY = 8 };
            Canvas.SetLeft(rect, zone.Left * w); Canvas.SetTop(rect, zone.Top * h);
            ZoneOverlayCanvas.Children.Add(rect);
            if (_lastZonePoints.TryGetValue(zone, out Point p)) {
                var el = new Ellipse { Width = 10, Height = 10, Fill = Brushes.Red };
                Canvas.SetLeft(el, (zone.Left + p.X * zone.Width) * w - 5);
                Canvas.SetTop(el, (zone.Top + p.Y * zone.Height) * h - 5);
                ZoneOverlayCanvas.Children.Add(el);
            }
        }
    }

    public void Pad_StylusDown(object sender, StylusDownEventArgs e) { if (_tabletService.IsWintabEnabled) return; e.Handled = true; Pad.CaptureStylus(); ProcessStylus(e); }
    public void Pad_StylusMove(object sender, StylusEventArgs e) { if (_tabletService.IsWintabEnabled) return; e.Handled = true; ProcessStylus(e); }
    public void Pad_StylusUp(object sender, StylusEventArgs e) { if (_tabletService.IsWintabEnabled) return; e.Handled = true; Pad.ReleaseStylusCapture(); _viewModel.ResetInteraction(); }

    public void Pad_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(Pad);
        int hit = HitTestZoneBody(pos);
        if (hit >= 0) { _selectedZoneIndex = hit; ZoneListBox.SelectedIndex = hit; LoadSelectedZoneIntoEditor(); DrawZoneOverlay(); }
        var edgeHit = HitTestZoneEdge(pos);
        if (edgeHit.zone >= 0) { StartZoneResize(edgeHit.zone, edgeHit.edge, pos); Pad.CaptureMouse(); e.Handled = true; return; }
        if (!_tabletService.IsWintabEnabled) { Pad.CaptureMouse(); ProcessMouse(e); }
    }

    public void Pad_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(Pad);
        var hit = HitTestZoneEdge(pos);
        Pad.Cursor = hit.zone >= 0 ? GetResizeCursor(hit.edge) : Cursors.Arrow;

        if (_isResizingZone) { UpdateZoneResize(pos); e.Handled = true; return; }
        if (!_tabletService.IsWintabEnabled && e.LeftButton == MouseButtonState.Pressed) ProcessMouse(e);
    }

    public void Pad_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isResizingZone) { FinishZoneResize(); Pad.ReleaseMouseCapture(); e.Handled = true; return; }
        if (!_tabletService.IsWintabEnabled) { Pad.ReleaseMouseCapture(); _viewModel.ResetInteraction(); }
    }

    private void ProcessStylus(StylusEventArgs e) {
        var p = e.GetPosition(Pad);
        double w = Pad.ActualWidth, h = Pad.ActualHeight;
        if (w <= 0 || h <= 0) return;
        int pressure = 127;
        var points = e.GetStylusPoints(Pad);
        if (points.Count > 0 && points[0].HasProperty(StylusPointProperties.NormalPressure))
            pressure = (int)(points[0].GetPropertyValue(StylusPointProperties.NormalPressure) * 127.0 / points[0].Description.GetPropertyInfo(StylusPointProperties.NormalPressure).Maximum);
        _tabletService.ProcessExternalPacket(new TabletPacket(0, (int)p.X, (int)p.Y, pressure, 0), new TabletContextInfo(0, 0, (int)w, 0, (int)h, 127, false));
    }

    private void ProcessMouse(MouseEventArgs e) {
        var p = e.GetPosition(Pad);
        double w = Pad.ActualWidth, h = Pad.ActualHeight;
        if (w <= 0 || h <= 0) return;
        _tabletService.ProcessExternalPacket(new TabletPacket(0, (int)p.X, (int)p.Y, 127, 0), new TabletContextInfo(0, 0, (int)w, 0, (int)h, 127, false));
    }

    private int HitTestZoneBody(Point pos)
    {
        double w = Pad.ActualWidth, h = Pad.ActualHeight;
        for (int i = 0; i < _viewModel.Zones.Count; i++) {
            var z = _viewModel.Zones[i];
            if (pos.X >= z.Left * w && pos.X <= (z.Left + z.Width) * w && pos.Y >= z.Top * h && pos.Y <= (z.Top + z.Height) * h) return i;
        }
        return -1;
    }

    private (int zone, ResizeEdge edge) HitTestZoneEdge(Point pos)
    {
        if (_selectedZoneIndex < 0) return (-1, ResizeEdge.None);
        double w = Pad.ActualWidth, h = Pad.ActualHeight;
        var z = _viewModel.Zones[_selectedZoneIndex];
        double zl = z.Left * w, zt = z.Top * h, zr = (z.Left + z.Width) * w, zb = (z.Top + z.Height) * h;
        ResizeEdge edge = ResizeEdge.None;
        if (Math.Abs(pos.X - zl) < ResizeHitMargin) edge |= ResizeEdge.Left;
        if (Math.Abs(pos.X - zr) < ResizeHitMargin) edge |= ResizeEdge.Right;
        if (Math.Abs(pos.Y - zt) < ResizeHitMargin) edge |= ResizeEdge.Top;
        if (Math.Abs(pos.Y - zb) < ResizeHitMargin) edge |= ResizeEdge.Bottom;
        return edge != ResizeEdge.None ? (_selectedZoneIndex, edge) : (-1, ResizeEdge.None);
    }

    private void StartZoneResize(int i, ResizeEdge e, Point p) { _isResizingZone = true; _resizeZoneIndex = i; _resizeEdge = e; _resizeDragStart = p; var z = _viewModel.Zones[i]; _resizeOrigLeft = z.Left; _resizeOrigTop = z.Top; _resizeOrigWidth = z.Width; _resizeOrigHeight = z.Height; }

    private void UpdateZoneResize(Point pos)
    {
        double w = Pad.ActualWidth, h = Pad.ActualHeight;
        double dx = (pos.X - _resizeDragStart.X) / w, dy = (pos.Y - _resizeDragStart.Y) / h;
        var z = _viewModel.Zones[_resizeZoneIndex];
        if (_resizeEdge.HasFlag(ResizeEdge.Left)) { z.Left = Math.Clamp(_resizeOrigLeft + dx, 0, _resizeOrigLeft + _resizeOrigWidth - 0.05); z.Width = _resizeOrigWidth - (z.Left - _resizeOrigLeft); }
        if (_resizeEdge.HasFlag(ResizeEdge.Right)) { z.Width = Math.Clamp(_resizeOrigWidth + dx, 0.05, 1 - z.Left); }
        if (_resizeEdge.HasFlag(ResizeEdge.Top)) { z.Top = Math.Clamp(_resizeOrigTop + dy, 0, _resizeOrigTop + _resizeOrigHeight - 0.05); z.Height = _resizeOrigHeight - (z.Top - _resizeOrigTop); }
        if (_resizeEdge.HasFlag(ResizeEdge.Bottom)) { z.Height = Math.Clamp(_resizeOrigHeight + dy, 0.05, 1 - z.Top); }
        LoadSelectedZoneIntoEditor(); DrawZoneOverlay();
    }

    private void FinishZoneResize() { _isResizingZone = false; }

    private Cursor GetResizeCursor(ResizeEdge edge) {
        if (edge.HasFlag(ResizeEdge.Left) && edge.HasFlag(ResizeEdge.Top)) return Cursors.SizeNWSE;
        if (edge.HasFlag(ResizeEdge.Right) && edge.HasFlag(ResizeEdge.Bottom)) return Cursors.SizeNWSE;
        if (edge.HasFlag(ResizeEdge.Left) && edge.HasFlag(ResizeEdge.Bottom)) return Cursors.SizeNESW;
        if (edge.HasFlag(ResizeEdge.Right) && edge.HasFlag(ResizeEdge.Top)) return Cursors.SizeNESW;
        if (edge.HasFlag(ResizeEdge.Left) || edge.HasFlag(ResizeEdge.Right)) return Cursors.SizeWE;
        if (edge.HasFlag(ResizeEdge.Top) || edge.HasFlag(ResizeEdge.Bottom)) return Cursors.SizeNS;
        return Cursors.Arrow;
    }

    public void TabletDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (TabletDeviceComboBox.SelectedItem is TabletDeviceItem item) {
            // Target device handling could be added here
        }
    }

    private void PopulateTabletDeviceList()
    {
        TabletDeviceComboBox.Items.Clear();
        foreach (var device in _tabletService.GetAvailableDevices()) TabletDeviceComboBox.Items.Add(device);
        if (TabletDeviceComboBox.Items.Count > 0) TabletDeviceComboBox.SelectedIndex = 0;
    }

    private static void AutoTileZones(AppConfig config)
    {
        if (config.Zones.Count == 0) return;
        int count = config.Zones.Count;
        int maxPerRow = Math.Max(1, config.MaxZonesPerRow);
        int rows = (int)Math.Ceiling((double)count / maxPerRow);
        double cellHeight = 1.0 / rows;
        for (int i = 0; i < count; i++) {
            int row = i / maxPerRow;
            int col = i % maxPerRow;
            int colsInRow = Math.Min(maxPerRow, count - row * maxPerRow);
            double cellWidth = 1.0 / colsInRow;
            config.Zones[i].Left = col * cellWidth;
            config.Zones[i].Top = row * cellHeight;
            config.Zones[i].Width = cellWidth;
            config.Zones[i].Height = cellHeight;
        }
    }
}
