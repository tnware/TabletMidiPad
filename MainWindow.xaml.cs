using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TabletMidiPad;

public partial class MainWindow : Window
{
    private const string MidiConsolePath = @"C:\Program Files\Windows MIDI Services\Tools\Console\midi.exe";

    private readonly Dictionary<string, int> _lastSentValues = new();

    private IntPtr _midiOutHandle;
    private bool _isActive;
    private bool _isLoadingUi;
    private int _selectedZoneIndex = -1;
    private bool _useWintab;
    private HwndSource? _wintabMessageSource;
    private readonly Dictionary<uint, WintabContextInfo> _wintabContexts = new();
    private int _wintabPacketMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_PACKET;
    private int _wintabProximityMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_PROXIMITY;
    private int _wintabInfoChangeMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_INFOCHANGE;

    private AppConfig _config = AppConfig.Default;
    private ZoneConfig? _activeZone;
    private string _lastInputBackend = "Idle";
    private string _wintabDiagnostics = "Wintab not initialized yet";

    public MainWindow()
    {
        InitializeComponent();
        ContentRendered += OnContentRendered;
        Closed += (_, _) =>
        {
            ShutdownMidi();
            ShutdownWintab();
        };
    }

    private async void OnContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= OnContentRendered;
        await InitializeAppAsync();
    }

    private async Task InitializeAppAsync()
    {
        SetLoadingProgress(8);
        SetLoadingState("Booting application", "Creating the interface");
        await Task.Yield();

        SetLoadingProgress(18);
        SetLoadingState("Loading configuration", "Reading saved routing and zone settings");
        PopulateChannelList(ZoneChannelComboBox);
        _config = AppConfig.Load();
        LoadConfigIntoUi(_config);
        await Task.Yield();

        SetLoadingProgress(34);
        SetLoadingState("Preparing tablet input", "Checking for direct tablet capture");
        InitializeWintab();
        await Task.Yield();

        await InitializeMidiAsync();

        SetLoadingProgress(100);
        SetLoadingState("Ready", "Controller surface is live");
        await Task.Delay(220);
        LoadingOverlay.Visibility = Visibility.Collapsed;
    }

    private static void PopulateChannelList(ComboBox comboBox)
    {
        comboBox.Items.Clear();
        for (int channel = 1; channel <= 16; channel++)
        {
            comboBox.Items.Add(channel);
        }
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        ApplySettings();
    }

    private void ResetDefaultsButton_Click(object sender, RoutedEventArgs e)
    {
        _config = AppConfig.Default;
        AutoTileZones(_config.Zones);
        LoadConfigIntoUi(_config);
        ApplySettings();
    }

    private void AutoLayoutButton_Click(object sender, RoutedEventArgs e)
    {
        CommitCurrentZoneEdits();
        AutoTileZones(_config.Zones);
        RefreshZoneList();
        ZoneListBox.SelectedIndex = Math.Clamp(_selectedZoneIndex, 0, _config.Zones.Count - 1);
        LoadSelectedZoneIntoEditor();
        DrawZoneOverlay();
        SettingsResultText.Foreground = Brushes.DarkSeaGreen;
        SettingsResultText.Text = "Zones re-tiled across the pad. Click Apply Settings to save.";
    }

    private void AddZoneButton_Click(object sender, RoutedEventArgs e)
    {
        CommitCurrentZoneEdits();

        int nextCcStart = GetNextAvailableCcStart();

        var zone = new ZoneConfig
        {
            Name = $"Zone {_config.Zones.Count + 1}",
            Enabled = true,
            MidiChannel = 1,
            CcX = nextCcStart,
            CcY = nextCcStart + 1,
            CcPressure = nextCcStart + 2,
            InvertY = true,
            SendPressure = true,
            ResetPressureOnRelease = true,
            Left = 0.0,
            Top = 0.0,
            Width = 0.5,
            Height = 0.5
        };

        _config.Zones.Add(zone);
        AutoTileZones(_config.Zones);
        RefreshZoneList();
        ZoneListBox.SelectedIndex = _config.Zones.Count - 1;
        LoadSelectedZoneIntoEditor();
        DrawZoneOverlay();
        SettingsResultText.Foreground = Brushes.DarkSeaGreen;
        SettingsResultText.Text = "Zone added. Click Apply Settings to save.";
    }

    private int GetNextAvailableCcStart()
    {
        var used = new HashSet<int>(
            _config.Zones.SelectMany(zone => new[] { zone.CcX, zone.CcY, zone.CcPressure })
        );

        int preferredStart = _config.Zones.Count == 0
            ? 16
            : _config.Zones.Max(zone => Math.Max(zone.CcX, Math.Max(zone.CcY, zone.CcPressure))) + 1;

        for (int start = preferredStart; start <= 125; start++)
        {
            if (!used.Contains(start) && !used.Contains(start + 1) && !used.Contains(start + 2))
            {
                return start;
            }
        }

        for (int start = 16; start <= 125; start++)
        {
            if (!used.Contains(start) && !used.Contains(start + 1) && !used.Contains(start + 2))
            {
                return start;
            }
        }

        throw new InvalidOperationException("No free 3-CC block is available for a new zone.");
    }

    private void RemoveZoneButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedZoneIndex < 0 || _selectedZoneIndex >= _config.Zones.Count)
        {
            return;
        }

        _config.Zones.RemoveAt(_selectedZoneIndex);
        if (_config.Zones.Count == 0)
        {
            _config.Zones.Add(ZoneConfig.CreateDefault());
        }

        AutoTileZones(_config.Zones);
        RefreshZoneList();
        ZoneListBox.SelectedIndex = Math.Clamp(_selectedZoneIndex, 0, _config.Zones.Count - 1);
        LoadSelectedZoneIntoEditor();
        DrawZoneOverlay();
        SettingsResultText.Foreground = Brushes.DarkSeaGreen;
        SettingsResultText.Text = "Zone removed. Click Apply Settings to save.";
    }

    private void ZoneListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingUi)
        {
            return;
        }

        _selectedZoneIndex = ZoneListBox.SelectedIndex;
        LoadSelectedZoneIntoEditor();
        DrawZoneOverlay();
    }

    private void Pad_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawZoneOverlay();
    }

    private void ApplySettings()
    {
        try
        {
            string previousAppPortName = _config.AppPortName;
            string previousDawPortName = _config.DawPortName;
            string previousAssociationId = _config.LoopbackAssociationId;

            CommitCurrentZoneEdits();
            ValidateConfig(_config);
            _config.Save();

            bool routingChanged =
                !string.Equals(previousAppPortName, _config.AppPortName, StringComparison.Ordinal) ||
                !string.Equals(previousDawPortName, _config.DawPortName, StringComparison.Ordinal) ||
                !string.Equals(previousAssociationId, _config.LoopbackAssociationId, StringComparison.Ordinal);

            if (routingChanged || _midiOutHandle == IntPtr.Zero)
            {
                RestartMidi();
            }
            else
            {
                _lastSentValues.Clear();
                _activeZone = null;
                UpdateUiForCurrentConfig();
                DrawZoneOverlay();
            }

            SettingsResultText.Foreground = Brushes.DarkSeaGreen;
            SettingsResultText.Text = "Settings applied and saved.";
        }
        catch (Exception ex)
        {
            SettingsResultText.Foreground = Brushes.IndianRed;
            SettingsResultText.Text = ex.Message;
        }
    }

    private void RestartMidi()
    {
        CloseMidiOut();
        RemoveLoopbackPair();
        InitMidi();
        DrawZoneOverlay();
    }

    private void LoadConfigIntoUi(AppConfig config)
    {
        _isLoadingUi = true;

        AppPortNameTextBox.Text = config.AppPortName;
        DawPortNameTextBox.Text = config.DawPortName;
        AssociationIdTextBox.Text = config.LoopbackAssociationId;
        MousePressureTextBox.Text = config.MousePressureValue.ToString();
        AllowZoneSwitchCheckBox.IsChecked = config.AllowZoneSwitchWhilePressed;

        RefreshZoneList();
        ZoneListBox.SelectedIndex = config.Zones.Count > 0 ? 0 : -1;
        _selectedZoneIndex = ZoneListBox.SelectedIndex;
        LoadSelectedZoneIntoEditor();

        _isLoadingUi = false;
        DrawZoneOverlay();
    }

    private void RefreshZoneList()
    {
        ZoneListBox.Items.Clear();

        for (int i = 0; i < _config.Zones.Count; i++)
        {
            var zone = _config.Zones[i];
            string enabled = zone.Enabled ? "on" : "off";
            ZoneListBox.Items.Add($"{i + 1}. {zone.Name} [{enabled}]");
        }
    }

    private void LoadSelectedZoneIntoEditor()
    {
        _isLoadingUi = true;

        if (_selectedZoneIndex < 0 || _selectedZoneIndex >= _config.Zones.Count)
        {
            ZoneNameTextBox.Text = string.Empty;
            ZoneEnabledCheckBox.IsChecked = false;
            ZoneChannelComboBox.SelectedIndex = -1;
            ZoneCcXTextBox.Text = string.Empty;
            ZoneCcYTextBox.Text = string.Empty;
            ZoneCcPressureTextBox.Text = string.Empty;
            ZoneLeftTextBox.Text = string.Empty;
            ZoneTopTextBox.Text = string.Empty;
            ZoneWidthTextBox.Text = string.Empty;
            ZoneHeightTextBox.Text = string.Empty;
            ZoneInvertXCheckBox.IsChecked = false;
            ZoneInvertYCheckBox.IsChecked = false;
            ZoneSendPressureCheckBox.IsChecked = false;
            ZoneResetPressureCheckBox.IsChecked = false;
            _isLoadingUi = false;
            return;
        }

        var zone = _config.Zones[_selectedZoneIndex];
        ZoneNameTextBox.Text = zone.Name;
        ZoneEnabledCheckBox.IsChecked = zone.Enabled;
        ZoneChannelComboBox.SelectedItem = zone.MidiChannel;
        ZoneChannelComboBox.Text = zone.MidiChannel.ToString();
        ZoneCcXTextBox.Text = zone.CcX.ToString();
        ZoneCcYTextBox.Text = zone.CcY.ToString();
        ZoneCcPressureTextBox.Text = zone.CcPressure.ToString();
        ZoneLeftTextBox.Text = zone.Left.ToString("0.###");
        ZoneTopTextBox.Text = zone.Top.ToString("0.###");
        ZoneWidthTextBox.Text = zone.Width.ToString("0.###");
        ZoneHeightTextBox.Text = zone.Height.ToString("0.###");
        ZoneInvertXCheckBox.IsChecked = zone.InvertX;
        ZoneInvertYCheckBox.IsChecked = zone.InvertY;
        ZoneSendPressureCheckBox.IsChecked = zone.SendPressure;
        ZoneResetPressureCheckBox.IsChecked = zone.ResetPressureOnRelease;

        _isLoadingUi = false;
    }

    private void CommitCurrentZoneEdits()
    {
        _config.AppPortName = RequireText(AppPortNameTextBox.Text, "Internal App Port");
        _config.DawPortName = RequireText(DawPortNameTextBox.Text, "DAW Input Port");
        _config.LoopbackAssociationId = RequireGuidText(AssociationIdTextBox.Text, "Association Id");
        _config.MousePressureValue = ReadInt(MousePressureTextBox.Text, 0, 127, "Mouse Pressure Fallback");
        _config.AllowZoneSwitchWhilePressed = AllowZoneSwitchCheckBox.IsChecked == true;

        if (_selectedZoneIndex < 0 || _selectedZoneIndex >= _config.Zones.Count)
        {
            return;
        }

        var zone = _config.Zones[_selectedZoneIndex];
        zone.Name = RequireText(ZoneNameTextBox.Text, "Zone Name");
        zone.Enabled = ZoneEnabledCheckBox.IsChecked == true;
        zone.MidiChannel = ReadInt(ZoneChannelComboBox.Text, 1, 16, "Zone MIDI Channel");
        zone.CcX = ReadInt(ZoneCcXTextBox.Text, 0, 127, "Zone CC X");
        zone.CcY = ReadInt(ZoneCcYTextBox.Text, 0, 127, "Zone CC Y");
        zone.CcPressure = ReadInt(ZoneCcPressureTextBox.Text, 0, 127, "Zone Pressure CC");
        zone.Left = ReadDouble(ZoneLeftTextBox.Text, 0.0, 1.0, "Zone Left");
        zone.Top = ReadDouble(ZoneTopTextBox.Text, 0.0, 1.0, "Zone Top");
        zone.Width = ReadDouble(ZoneWidthTextBox.Text, 0.0, 1.0, "Zone Width");
        zone.Height = ReadDouble(ZoneHeightTextBox.Text, 0.0, 1.0, "Zone Height");
        zone.InvertX = ZoneInvertXCheckBox.IsChecked == true;
        zone.InvertY = ZoneInvertYCheckBox.IsChecked == true;
        zone.SendPressure = ZoneSendPressureCheckBox.IsChecked == true;
        zone.ResetPressureOnRelease = ZoneResetPressureCheckBox.IsChecked == true;

        RefreshZoneList();
        ZoneListBox.SelectedIndex = _selectedZoneIndex;
    }

    private static void ValidateConfig(AppConfig config)
    {
        if (config.Zones.Count == 0)
        {
            throw new InvalidOperationException("At least one zone is required.");
        }

        foreach (var zone in config.Zones)
        {
            if (zone.Width <= 0 || zone.Height <= 0)
            {
                throw new InvalidOperationException($"Zone '{zone.Name}' must have width and height greater than 0.");
            }

            if (zone.Left + zone.Width > 1.0 || zone.Top + zone.Height > 1.0)
            {
                throw new InvalidOperationException($"Zone '{zone.Name}' must fit within the pad bounds.");
            }
        }
    }

    private static void AutoTileZones(IList<ZoneConfig> zones)
    {
        if (zones.Count == 0)
        {
            return;
        }

        int count = zones.Count;
        int columns = (int)Math.Ceiling(Math.Sqrt(count));
        int rows = (int)Math.Ceiling((double)count / columns);

        double cellWidth = 1.0 / columns;
        double cellHeight = 1.0 / rows;

        for (int index = 0; index < count; index++)
        {
            int row = index / columns;
            int column = index % columns;

            zones[index].Left = column * cellWidth;
            zones[index].Top = row * cellHeight;
            zones[index].Width = cellWidth;
            zones[index].Height = cellHeight;
        }
    }

    private void InitMidi()
    {
        try
        {
            RemoveLoopbackPair();
            CreateLoopbackPair();
            OpenMidiOutWithRetry(_config.AppPortName);
            _lastSentValues.Clear();
            _activeZone = null;
            UpdateUiForCurrentConfig();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Windows MIDI Services init failed";
            RoutingText.Text = string.Empty;
            InputBackendText.Text = string.Empty;
            ActiveZoneText.Text = string.Empty;
            ConfigSummaryText.Text = string.Empty;
            ValueText.Text = ex.Message;
            throw;
        }
    }

    private async Task InitializeMidiAsync()
    {
        try
        {
            SetLoadingProgress(52);
            SetLoadingState("Creating MIDI endpoints", "Removing any stale loopback pair");
            await Task.Run(RemoveLoopbackPair);

            SetLoadingProgress(68);
            SetLoadingState("Creating MIDI endpoints", "Creating a fresh loopback pair");
            await Task.Run(CreateLoopbackPair);

            SetLoadingProgress(86);
            SetLoadingState("Opening MIDI output", $"Opening '{_config.AppPortName}'");
            await Task.Run(() => OpenMidiOutWithRetry(_config.AppPortName));

            _lastSentValues.Clear();
            _activeZone = null;
            UpdateUiForCurrentConfig();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Windows MIDI Services init failed";
            RoutingText.Text = string.Empty;
            InputBackendText.Text = string.Empty;
            ActiveZoneText.Text = string.Empty;
            ConfigSummaryText.Text = string.Empty;
            ValueText.Text = ex.Message;
            throw;
        }
    }

    private void UpdateUiForCurrentConfig()
    {
        StatusText.Text = $"Send to '{_config.DawPortName}' in Ableton/Waveform";
        RoutingText.Text = $"App '{_config.AppPortName}' -> DAW '{_config.DawPortName}'";
        UpdateInputBackendStatus();
        ActiveZoneText.Text = "Active zone: none";
        string switchMode = _config.AllowZoneSwitchWhilePressed ? "free switch" : "locked until release";
        ConfigSummaryText.Text = $"{_config.Zones.Count} zone(s) configured | {switchMode}";
        ValueText.Text = "X: -, Y: -, Pressure: -";
        DebugInputText.Text = "Raw input: -";
    }

    private void ShutdownMidi()
    {
        try
        {
            CloseMidiOut();
            RemoveLoopbackPair();
        }
        catch
        {
        }
    }

    private void Pad_StylusDown(object sender, StylusDownEventArgs e)
    {
        if (_useWintab) return;
        e.Handled = true;
        _isActive = true;
        Pad.CaptureStylus();
        HandleStylus(e);
    }

    private void Pad_StylusMove(object sender, StylusEventArgs e)
    {
        if (_useWintab) return;
        if (!_isActive) return;
        e.Handled = true;
        HandleStylus(e);
    }

    private void Pad_StylusUp(object sender, StylusEventArgs e)
    {
        if (_useWintab) return;
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
        if (_useWintab) return;
        if (IsPromotedStylusMouseEvent(e)) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;
        _isActive = true;
        Pad.CaptureMouse();
        HandleMouse(e);
    }

    private void Pad_MouseMove(object sender, MouseEventArgs e)
    {
        if (_useWintab) return;
        if (IsPromotedStylusMouseEvent(e)) return;
        if (!_isActive || e.LeftButton != MouseButtonState.Pressed) return;
        HandleMouse(e);
    }

    private void Pad_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_useWintab) return;
        if (IsPromotedStylusMouseEvent(e)) return;
        if (!_isActive) return;
        HandleMouse(e);
        _isActive = false;
        Pad.ReleaseMouseCapture();
        CursorDot.Visibility = Visibility.Collapsed;
        HandleRelease();
    }

    private void HandleStylus(StylusEventArgs e)
    {
        MarkInputBackend("Stylus");
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

        DebugInputText.Text = $"Raw stylus: px=({point.X:0.0},{point.Y:0.0}) norm=({xNorm:0.000},{yNorm:0.000}) pressure={pressure}";
        HandlePoint(point.X, point.Y, xNorm, yNorm, pressure);
    }

    private void HandleMouse(MouseEventArgs e)
    {
        MarkInputBackend("Mouse");
        var point = e.GetPosition(Pad);
        if (!TryGetPadCoordinates(point, out double xNorm, out double yNorm))
        {
            return;
        }

        DebugInputText.Text = $"Raw mouse: px=({point.X:0.0},{point.Y:0.0}) norm=({xNorm:0.000},{yNorm:0.000}) pressure={_config.MousePressureValue}";
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
            ActiveZoneText.Text = "Active zone: none";
            ValueText.Text = "Outside configured zones";
            return;
        }

        var local = NormalizeWithinZone(zone, xNorm, yNorm);
        int x = ToCc(local.x, zone.InvertX);
        int y = ToCc(local.y, zone.InvertY);

        ActiveZoneText.Text = $"Active zone: {zone.Name}";
        ValueText.Text = zone.SendPressure
            ? $"{zone.Name} -> X: {x}, Y: {y}, Pressure: {pressure}"
            : $"{zone.Name} -> X: {x}, Y: {y}, Pressure: off";

        SendCcIfChanged(zone.MidiChannel, zone.CcX, x);
        SendCcIfChanged(zone.MidiChannel, zone.CcY, y);

        if (zone.SendPressure)
        {
            SendCcIfChanged(zone.MidiChannel, zone.CcPressure, pressure);
        }
    }

    private void HandleRelease()
    {
        SendZoneRelease(_activeZone);
        _activeZone = null;
        ActiveZoneText.Text = "Active zone: none";
    }

    private void SendZoneRelease(ZoneConfig? zone)
    {
        if (zone is null || !zone.Enabled)
        {
            return;
        }

        if (zone.SendPressure && zone.ResetPressureOnRelease)
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

    private void DrawZoneOverlay()
    {
        ZoneOverlayCanvas.Children.Clear();

        double width = Pad.ActualWidth;
        double height = Pad.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        ZoneOverlayCanvas.Width = width;
        ZoneOverlayCanvas.Height = height;

        for (int i = 0; i < _config.Zones.Count; i++)
        {
            var zone = _config.Zones[i];
            double rectLeft = zone.Left * width;
            double rectTop = zone.Top * height;
            double rectWidth = zone.Width * width;
            double rectHeight = zone.Height * height;

            byte alpha = zone.Enabled ? (byte)70 : (byte)30;
            byte outlineAlpha = zone.Enabled ? (byte)180 : (byte)90;
            Color fillColor = Color.FromArgb(alpha, 90, 150, 220);
            Color strokeColor = Color.FromArgb(outlineAlpha, 180, 220, 255);

            if (i == _selectedZoneIndex)
            {
                fillColor = Color.FromArgb(100, 200, 120, 60);
                strokeColor = Color.FromArgb(220, 255, 180, 110);
            }

            var rect = new Rectangle
            {
                Width = rectWidth,
                Height = rectHeight,
                Fill = new SolidColorBrush(fillColor),
                Stroke = new SolidColorBrush(strokeColor),
                StrokeThickness = 2,
                RadiusX = 8,
                RadiusY = 8
            };

            var label = new TextBlock
            {
                Text = zone.Name,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
                Padding = new Thickness(6, 2, 6, 2)
            };

            Canvas.SetLeft(rect, rectLeft);
            Canvas.SetTop(rect, rectTop);
            Canvas.SetLeft(label, rectLeft + 8);
            Canvas.SetTop(label, rectTop + 8);

            ZoneOverlayCanvas.Children.Add(rect);
            ZoneOverlayCanvas.Children.Add(label);
        }
    }

    private void SendCcIfChanged(int channel, int cc, int value, bool force = false)
    {
        if (_midiOutHandle == IntPtr.Zero) return;

        string key = $"{channel}:{cc}";
        value = Math.Clamp(value, 0, 127);

        if (!force && _lastSentValues.TryGetValue(key, out int lastValue) && lastValue == value)
        {
            return;
        }

        byte status = (byte)(0xB0 | ((channel - 1) & 0x0F));
        byte controller = (byte)(cc & 0x7F);
        byte data = (byte)(value & 0x7F);

        int packedMessage = status | (controller << 8) | (data << 16);
        int result = midiOutShortMsg(_midiOutHandle, packedMessage);
        if (result != 0)
        {
            throw new Win32Exception(result, $"midiOutShortMsg failed with code {result}.");
        }

        _lastSentValues[key] = value;
    }

    private void CreateLoopbackPair()
    {
        RunMidiConsole(
            $"loopback create --name-a \"{_config.AppPortName}\" --name-b \"{_config.DawPortName}\" --association-id \"{_config.LoopbackAssociationId}\""
        );
        Thread.Sleep(300);
    }

    private void RemoveLoopbackPair()
    {
        RunMidiConsole(
            $"loopback remove --association-id \"{_config.LoopbackAssociationId}\"",
            allowFailure: true
        );
    }

    private void OpenMidiOutWithRetry(string portName)
    {
        for (int attempt = 0; attempt < 60; attempt++)
        {
            if (TryOpenMidiOut(portName, out _midiOutHandle))
            {
                return;
            }

            Thread.Sleep(100);
        }

        string available = string.Join(", ", EnumerateMidiOutPortNames());
        throw new InvalidOperationException(
            $"Could not open MIDI destination '{portName}'. Available MIDI outputs: {available}."
        );
    }

    private static bool TryOpenMidiOut(string targetName, out IntPtr handle)
    {
        handle = IntPtr.Zero;

        int count = midiOutGetNumDevs();
        for (int deviceId = 0; deviceId < count; deviceId++)
        {
            if (midiOutGetDevCaps((UIntPtr)deviceId, out MidiOutCaps caps, (uint)Marshal.SizeOf<MidiOutCaps>()) != 0)
            {
                continue;
            }

            if (!string.Equals(caps.Name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int result = midiOutOpen(out handle, deviceId, IntPtr.Zero, IntPtr.Zero, 0);
            return result == 0;
        }

        return false;
    }

    private void CloseMidiOut()
    {
        if (_midiOutHandle == IntPtr.Zero)
        {
            return;
        }

        midiOutReset(_midiOutHandle);
        midiOutClose(_midiOutHandle);
        _midiOutHandle = IntPtr.Zero;
    }

    private static string[] EnumerateMidiOutPortNames()
    {
        int count = midiOutGetNumDevs();
        var names = new string[count];

        for (int deviceId = 0; deviceId < count; deviceId++)
        {
            names[deviceId] = midiOutGetDevCaps((UIntPtr)deviceId, out MidiOutCaps caps, (uint)Marshal.SizeOf<MidiOutCaps>()) == 0
                ? caps.Name
                : $"<error:{deviceId}>";
        }

        return names.Where(name => !string.IsNullOrWhiteSpace(name)).ToArray();
    }

    private static void RunMidiConsole(string arguments, bool allowFailure = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = MidiConsolePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the Windows MIDI Services console.");

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (allowFailure || process.ExitCode == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"midi.exe {arguments} failed with exit code {process.ExitCode}. {output} {error}".Trim()
        );
    }

    private static int ToCc(double normalized, bool invert)
    {
        normalized = Math.Clamp(normalized, 0.0, 1.0);
        if (invert)
        {
            normalized = 1.0 - normalized;
        }

        return (int)Math.Round(normalized * 127.0);
    }

    private static int ReadInt(string? text, int min, int max, string fieldName)
    {
        if (!int.TryParse(text, out int value))
        {
            throw new InvalidOperationException($"{fieldName} must be a whole number.");
        }

        if (value < min || value > max)
        {
            throw new InvalidOperationException($"{fieldName} must be between {min} and {max}.");
        }

        return value;
    }

    private static double ReadDouble(string? text, double min, double max, string fieldName)
    {
        if (!double.TryParse(text, out double value))
        {
            throw new InvalidOperationException($"{fieldName} must be a number.");
        }

        if (value < min || value > max)
        {
            throw new InvalidOperationException($"{fieldName} must be between {min:0.###} and {max:0.###}.");
        }

        return value;
    }

    private static string RequireText(string? text, string fieldName)
    {
        string value = (text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return value;
    }

    private static string RequireGuidText(string? text, string fieldName)
    {
        string value = RequireText(text, fieldName);
        if (!Guid.TryParse(value, out Guid parsed))
        {
            throw new InvalidOperationException($"{fieldName} must be a valid GUID.");
        }

        return parsed.ToString("B");
    }

    private void InitializeWintab()
    {
        if (!WintabInterop.IsAvailable())
        {
            _useWintab = false;
            _wintabDiagnostics = "Wintab32.dll not available";
            SetLoadingState("Preparing tablet input", "Wintab not found, using stylus and mouse fallback");
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
            _lastInputBackend = _useWintab ? "Wintab" : "Stylus/Mouse Fallback";
            if (_useWintab)
            {
                _wintabDiagnostics = $"Opened {openedContexts} Wintab context(s)";
            }

            SetLoadingState(
                "Preparing tablet input",
                _useWintab
                    ? $"Wintab digitizer input active on {openedContexts} tablet context(s)"
                    : "Wintab detected, but no digitizer contexts could open"
            );
        }
        catch (Exception ex)
        {
            _useWintab = false;
            _lastInputBackend = "Stylus/Mouse Fallback";
            _wintabDiagnostics = ex.Message;
            SetLoadingState("Preparing tablet input", "Wintab failed, using stylus and mouse fallback");
        }
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
        _wintabProximityMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_PROXIMITY;
        _wintabInfoChangeMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_INFOCHANGE;
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
        else if (msg == _wintabProximityMessage)
        {
            if (unchecked((uint)wParam.ToInt32()) == 0)
            {
                if (_isActive)
                {
                    _isActive = false;
                    CursorDot.Visibility = Visibility.Collapsed;
                    HandleRelease();
                }
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
        bool messageBaseInitialized = false;

        try
        {
            var defaultContext = WintabInterop.GetDefaultDigitizingContext();
            defaultContext.Name = "TabletMidiPad Default Digitizer";
            defaultContext.Options |= WintabInterop.CXO_MESSAGES;
            defaultContext.Options &= ~WintabInterop.CXO_SYSTEM;
            defaultContext.PktData = WintabInterop.PK_X | WintabInterop.PK_Y | WintabInterop.PK_NORMAL_PRESSURE | WintabInterop.PK_BUTTONS;
            defaultContext.PktMode = 0;
            defaultContext.MoveMask = defaultContext.PktData;
            defaultContext.BtnUpMask = defaultContext.BtnDnMask;

            int defaultDeviceIndex = (int)defaultContext.Device;
            var xAxis = WintabInterop.GetXAxis(defaultDeviceIndex);
            var yAxis = WintabInterop.GetYAxis(defaultDeviceIndex);
            var pressureAxis = WintabInterop.GetPressureAxis(defaultDeviceIndex);

            if (xAxis.Max > xAxis.Min && yAxis.Max > yAxis.Min)
            {
                defaultContext.OutOrgX = 0;
                defaultContext.OutOrgY = 0;
                defaultContext.OutExtX = xAxis.Max - xAxis.Min + 1;
                defaultContext.OutExtY = -(yAxis.Max - yAxis.Min + 1);
                defaultContext.SysMode = false;

                _wintabPacketMessage = unchecked((int)defaultContext.MsgBase + WintabInterop.WT_PACKET);
                _wintabProximityMessage = unchecked((int)defaultContext.MsgBase + WintabInterop.WT_PROXIMITY);
                _wintabInfoChangeMessage = unchecked((int)defaultContext.MsgBase + WintabInterop.WT_INFOCHANGE);
                messageBaseInitialized = true;

                uint handle = WintabInterop.WTOpenA(_wintabMessageSource!.Handle, ref defaultContext, true);
                if (handle != 0)
                {
                    _wintabDiagnostics = $"Default digitizing context opened on device {defaultDeviceIndex}";
                    _wintabContexts[handle] = new WintabContextInfo(
                        DeviceIndex: defaultDeviceIndex,
                        OutputMinX: 0,
                        OutputMaxX: Math.Max(1, defaultContext.OutExtX - 1),
                        OutputMinY: 0,
                        OutputMaxY: Math.Max(1, Math.Abs(defaultContext.OutExtY) - 1),
                        PressureMax: Math.Max(1, pressureAxis.Max),
                        IsIntegrated: defaultDeviceIndex >= 0 && WintabInterop.IsIntegratedTablet(defaultDeviceIndex)
                    );

                    return _wintabContexts.Count;
                }

                _wintabDiagnostics = $"WTOpenA failed for default digitizing context on device {defaultDeviceIndex}";
            }
            else
            {
                _wintabDiagnostics = $"Default digitizing context axes invalid for device {defaultDeviceIndex}: X {xAxis.Min}-{xAxis.Max}, Y {yAxis.Min}-{yAxis.Max}";
            }
        }
        catch (Exception ex)
        {
            _wintabDiagnostics = $"Default digitizing context failed: {ex.Message}";
        }

        int deviceCount = Math.Max(1, WintabInterop.GetAttachedDeviceCount());
        _wintabDiagnostics = $"Trying {deviceCount} enumerated digitizer context(s)";

        for (int deviceIndex = 0; deviceIndex < deviceCount; deviceIndex++)
        {
            WintabInterop.LogContext context;
            try
            {
                context = WintabInterop.GetDigitizerContext(deviceIndex);
            }
            catch (Exception ex)
            {
                _wintabDiagnostics = $"WTI_DDCTXS {deviceIndex} failed: {ex.Message}";
                continue;
            }

            var xAxis = WintabInterop.GetXAxis(deviceIndex);
            var yAxis = WintabInterop.GetYAxis(deviceIndex);
            var pressureAxis = WintabInterop.GetPressureAxis(deviceIndex);

            if (xAxis.Max <= xAxis.Min || yAxis.Max <= yAxis.Min)
            {
                _wintabDiagnostics = $"Device {deviceIndex} axes invalid: X {xAxis.Min}-{xAxis.Max}, Y {yAxis.Min}-{yAxis.Max}";
                continue;
            }

            context.Name = $"TabletMidiPad Digitizer {deviceIndex}";
            context.Options |= WintabInterop.CXO_MESSAGES;
            context.Options &= ~WintabInterop.CXO_SYSTEM;
            context.PktData = WintabInterop.PK_X | WintabInterop.PK_Y | WintabInterop.PK_NORMAL_PRESSURE | WintabInterop.PK_BUTTONS;
            context.PktMode = 0;
            context.MoveMask = context.PktData;
            context.BtnUpMask = context.BtnDnMask;
            context.OutOrgX = 0;
            context.OutOrgY = 0;
            context.OutExtX = xAxis.Max - xAxis.Min + 1;
            context.OutExtY = -(yAxis.Max - yAxis.Min + 1);
            context.SysMode = false;

            if (!messageBaseInitialized)
            {
                _wintabPacketMessage = unchecked((int)context.MsgBase + WintabInterop.WT_PACKET);
                _wintabProximityMessage = unchecked((int)context.MsgBase + WintabInterop.WT_PROXIMITY);
                _wintabInfoChangeMessage = unchecked((int)context.MsgBase + WintabInterop.WT_INFOCHANGE);
                messageBaseInitialized = true;
            }

            uint handle = WintabInterop.WTOpenA(_wintabMessageSource!.Handle, ref context, true);
            if (handle == 0)
            {
                _wintabDiagnostics = $"WTOpenA failed for digitizer context {deviceIndex}";
                continue;
            }

            _wintabDiagnostics = $"Digitizer context {deviceIndex} opened";
            _wintabContexts[handle] = new WintabContextInfo(
                DeviceIndex: deviceIndex,
                OutputMinX: 0,
                OutputMaxX: Math.Max(1, context.OutExtX - 1),
                OutputMinY: 0,
                OutputMaxY: Math.Max(1, Math.Abs(context.OutExtY) - 1),
                PressureMax: Math.Max(1, pressureAxis.Max),
                IsIntegrated: WintabInterop.IsIntegratedTablet(deviceIndex)
            );
        }

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
        _lastInputBackend = _useWintab ? "Wintab" : "Stylus/Mouse Fallback";
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

        MarkInputBackend($"Wintab D{context.DeviceIndex + 1}");

        double xNorm = NormalizeWintabCoordinate(packet.X, context.OutputMinX, context.OutputMaxX);
        double yNorm = NormalizeWintabCoordinate(packet.Y, context.OutputMinY, context.OutputMaxY);
        double xPx = xNorm * width;
        double yPx = yNorm * height;
        int pressure = context.PressureMax > 0
            ? (int)Math.Round(packet.NormalPressure * 127.0 / context.PressureMax)
            : 127;

        DebugInputText.Text =
            $"Raw Wintab D{context.DeviceIndex + 1}: x={packet.X} y={packet.Y} p={packet.NormalPressure}/{context.PressureMax} " +
            $"buttons=0x{packet.Buttons:X} norm=({xNorm:0.000},{yNorm:0.000})";

        if (pressure <= 0)
        {
            if (_isActive)
            {
                _isActive = false;
                CursorDot.Visibility = Visibility.Collapsed;
                HandleRelease();
            }

            return;
        }

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

    private void MarkInputBackend(string backend)
    {
        _lastInputBackend = backend;
        UpdateInputBackendStatus();
    }

    private void UpdateInputBackendStatus()
    {
        string configured = _useWintab
            ? $"Wintab enabled, {_wintabContexts.Count} context(s)"
            : $"Stylus/mouse fallback | {_wintabDiagnostics}";

        InputBackendText.Text = $"Input: {_lastInputBackend} | {configured}";
    }

    private void SetLoadingState(string title, string detail)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetLoadingState(title, detail));
            return;
        }

        LoadingTitleText.Text = title;
        LoadingDetailText.Text = detail;
    }

    private void SetLoadingProgress(double value)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetLoadingProgress(value));
            return;
        }

        LoadingProgressBar.Value = Math.Clamp(value, LoadingProgressBar.Minimum, LoadingProgressBar.Maximum);
    }

    private readonly record struct WintabContextInfo(
        int DeviceIndex,
        int OutputMinX,
        int OutputMaxX,
        int OutputMinY,
        int OutputMaxY,
        int PressureMax,
        bool IsIntegrated
    );

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MidiOutCaps
    {
        public ushort ManufacturerId;
        public ushort ProductId;
        public uint DriverVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string Name;

        public ushort Technology;
        public ushort Voices;
        public ushort Notes;
        public ushort ChannelMask;
        public uint Support;
    }

    [DllImport("winmm.dll", CharSet = CharSet.Auto)]
    private static extern int midiOutGetDevCaps(UIntPtr uDeviceId, out MidiOutCaps caps, uint cbMidiOutCaps);

    [DllImport("winmm.dll")]
    private static extern int midiOutGetNumDevs();

    [DllImport("winmm.dll")]
    private static extern int midiOutOpen(out IntPtr handle, int deviceId, IntPtr callback, IntPtr instance, int flags);

    [DllImport("winmm.dll")]
    private static extern int midiOutShortMsg(IntPtr handle, int message);

    [DllImport("winmm.dll")]
    private static extern int midiOutReset(IntPtr handle);

    [DllImport("winmm.dll")]
    private static extern int midiOutClose(IntPtr handle);
}
