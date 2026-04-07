using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using TabletMidiPad.Services;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad;

public partial class MainWindow : Window
{
    private readonly IMidiService _midiService;
    private readonly IInputMappingService _inputMappingService;
    private readonly IWacomService _wacomService;
    private readonly MainViewModel _viewModel;
    private readonly Dictionary<ZoneConfig, Point> _lastZonePoints = new();
    private readonly Dictionary<ZoneConfig, Point> _smoothedZonePoints = new();
    private bool _isActive;
    private bool _isLoadingUi;
    private int _selectedZoneIndex = -1;
    private bool _useWintab;
    private int _selectedDeviceIndex = -1;
    private HwndSource? _wintabMessageSource;
    private readonly Dictionary<uint, WintabContextInfo> _wintabContexts = new();
    private int _wintabPacketMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_PACKET;
    private int _wintabPacketExtMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_PACKETEXT;
    private int _wintabContextOverlapMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_CTXOVERLAP;
    private int _wintabProximityMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_PROXIMITY;
    private int _wintabInfoChangeMessage = WintabInterop.WT_DEFBASE + WintabInterop.WT_INFOCHANGE;
    private uint _expressKeysMask;
    private int _expressKeyCount;
    private string _expressKeyDiagnostics = "";

    private AppConfig _config = AppConfig.Default;
    private ZoneConfig? _activeZone;
    private string _lastInputBackend = "Idle";
    private string _wintabDiagnostics = "Wintab not initialized yet";
    private uint _lastWintabButtons;
    private bool _lowerPenButtonHeld;
    private bool _upperPenButtonHeld;
    private bool _lowerPenButtonToggleState;
    private bool _upperPenButtonToggleState;
    private bool _hasLockedPosition;
    private Point _lockedLocalPosition;
    private readonly Dictionary<int, bool> _expressKeyHeld = new();
    private readonly Dictionary<int, bool> _expressKeyToggleState = new();

    // Zone resize drag state
    private const double ResizeHitMargin = 8.0;
    private const double ZoneSelectionInset = 0.02;
    private bool _isResizingZone;
    private int _resizeZoneIndex = -1;
    private ResizeEdge _resizeEdge;
    private Point _resizeDragStart;
    private double _resizeOrigLeft, _resizeOrigTop, _resizeOrigWidth, _resizeOrigHeight;

    [Flags]
    private enum ResizeEdge
    {
        None = 0,
        Left = 1,
        Right = 2,
        Top = 4,
        Bottom = 8
    }

    public MainWindow(
        IMidiService midiService,
        IInputMappingService inputMappingService,
        IWacomService wacomService,
        MainViewModel viewModel)
    {
        _midiService = midiService;
        _inputMappingService = inputMappingService;
        _wacomService = wacomService;
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;
        WindowChromeTheme.ApplyDarkNonClientChrome(this);
        ConfigureNumericInputs();
        ContentRendered += OnContentRendered;
        Closed += (_, _) =>
        {
            ShutdownMidi();
            ShutdownWintab();
        };
    }

    private void ConfigureNumericInputs()
    {
        foreach (TextBox textBox in new[]
        {
            MaxZoneRowsTextBox,
            MaxZonesPerRowTextBox,
            ZoneCcXTextBox,
            ZoneCcYTextBox,
            ZoneCcPressureTextBox
        })
        {
            NumericTextBoxBehavior.SetMode(textBox, NumericTextMode.Integer);
            textBox.HorizontalContentAlignment = HorizontalAlignment.Right;
        }

        foreach (TextBox textBox in new[]
        {
            ZoneLeftTextBox,
            ZoneTopTextBox,
            ZoneWidthTextBox,
            ZoneHeightTextBox
        })
        {
            NumericTextBoxBehavior.SetMode(textBox, NumericTextMode.Decimal);
            textBox.HorizontalContentAlignment = HorizontalAlignment.Right;
        }
    }

    private async void OnContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= OnContentRendered;
        await InitializeAppAsync();
    }

    private async Task InitializeAppAsync()
    {
        PopulateChannelList(ZoneChannelComboBox);
        PopulateReleaseBehaviorList(ZoneReleaseBehaviorComboBox);
        _viewModel.Load();
        _config = _viewModel.Config;
        LoadConfigIntoUi(_config);
        RefreshWacomServiceStatus();

        InitializeWintab();

        MidiStatusPanel.Visibility = Visibility.Visible;
        MidiStatusText.Text = "Connecting MIDI...";
        await Task.Yield();

        try
        {
            await InitializeMidiAsync();
            MidiStatusPanel.Visibility = Visibility.Collapsed;
        }
        catch
        {
            MidiProgressBar.Visibility = Visibility.Collapsed;
            MidiStatusText.Foreground = FindResource("DangerBrush") as Brush ?? Brushes.IndianRed;
            MidiStatusText.Text = "MIDI init failed";
        }
    }

    private static void PopulateChannelList(ComboBox comboBox)
    {
        comboBox.Items.Clear();
        for (int channel = 1; channel <= 16; channel++)
        {
            comboBox.Items.Add(channel);
        }
    }

    private static void PopulateReleaseBehaviorList(ComboBox comboBox)
    {
        comboBox.Items.Clear();
        comboBox.Items.Add(ZoneReleaseBehaviors.NoSendOnRelease);
        comboBox.Items.Add(ZoneReleaseBehaviors.Hold);
        comboBox.Items.Add(ZoneReleaseBehaviors.ResetPressureOnly);
        comboBox.Items.Add(ZoneReleaseBehaviors.ResetAll);
    }

    private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ApplySettings();
    }

    private void MenuSaveSettings_Click(object sender, RoutedEventArgs e)
    {
        ApplySettings();
    }

    private void MenuResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        _config = AppConfig.Default;
        _viewModel.AutoTileZones();
        LoadConfigIntoUi(_config);
        ApplySettings();
    }

    private void MenuPreferences_Click(object sender, RoutedEventArgs e)
    {
        ExpressKeyConfig.EnsureCount(_config.ExpressKeys, _expressKeyCount);
        var prefs = new PreferencesWindow(_expressKeyCount) { Owner = this };
        prefs.LoadFrom(_config);

        prefs.ShowDialog();

        if (!prefs.Accepted)
        {
            return;
        }

        string previousAppPortName = _config.AppPortName;
        string previousDawPortName = _config.DawPortName;
        string previousAssociationId = _config.LoopbackAssociationId;
        string previousInputBackendMode = _config.InputBackendMode;

        _config.DawPortName = prefs.DawPortName;
        _config.AppPortName = prefs.AppPortName;
        _config.LoopbackAssociationId = prefs.AssociationId;
        _config.MousePressureValue = prefs.MousePressureValue;
        _config.PositionDeadZone = prefs.PositionDeadZone;
        _config.PositionSmoothing = prefs.PositionSmoothing;
        _config.PressureCurve = prefs.PressureCurve;
        _config.InputBackendMode = prefs.InputBackendMode;
        _config.LowerPenButtonAction = prefs.LowerPenButtonAction;
        _config.LowerPenButtonMidiChannel = prefs.LowerPenButtonMidiChannel;
        _config.LowerPenButtonCc = prefs.LowerPenButtonCc;
        _config.LowerPenButtonNote = prefs.LowerPenButtonNote;
        _config.UpperPenButtonAction = prefs.UpperPenButtonAction;
        _config.UpperPenButtonMidiChannel = prefs.UpperPenButtonMidiChannel;
        _config.UpperPenButtonCc = prefs.UpperPenButtonCc;
        _config.UpperPenButtonNote = prefs.UpperPenButtonNote;
        _config.SendPressureGlobally = prefs.SendPressureGlobally;
        _config.AllowZoneSwitchWhilePressed = prefs.AllowZoneSwitchWhilePressed;
        _config.ExpressKeys = prefs.GetExpressKeyConfigs();

        CommitCurrentZoneEdits();

        try
        {
            _viewModel.Save();
        }
        catch (Exception ex)
        {
            SettingsResultText.Foreground = Brushes.IndianRed;
            SettingsResultText.Text = ex.Message;
            return;
        }

        bool routingChanged =
            !string.Equals(previousAppPortName, _config.AppPortName, StringComparison.Ordinal) ||
            !string.Equals(previousDawPortName, _config.DawPortName, StringComparison.Ordinal) ||
            !string.Equals(previousAssociationId, _config.LoopbackAssociationId, StringComparison.Ordinal);
        bool inputBackendChanged =
            !string.Equals(previousInputBackendMode, _config.InputBackendMode, StringComparison.Ordinal);

        if (routingChanged || !_midiService.IsConnected)
        {
            RestartMidi();
        }
        else
        {
            _activeZone = null;
            UpdateUiForCurrentConfig();
            DrawZoneOverlay();
        }

        if (inputBackendChanged)
        {
            RebindInputBackend();
        }

        SyncToolbar();
        SettingsResultText.Foreground = Brushes.DarkSeaGreen;
        SettingsResultText.Text = "Preferences applied and saved.";
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MenuRefreshWacom_Click(object sender, RoutedEventArgs e)
    {
        RefreshWacomServiceStatus();
    }

    private void MenuReconnectPen_Click(object sender, RoutedEventArgs e)
    {
        CommitCurrentZoneEdits();
        RebindInputBackend();
        SettingsResultText.Foreground = Brushes.DarkSeaGreen;
        SettingsResultText.Text = "Input backend reconnected.";
    }

    private async void MenuStartWacomService_Click(object sender, RoutedEventArgs e)
    {
        MenuStartWacomService.IsEnabled = false;
        MenuWacomServiceStatus.Header = "Service: starting...";
        bool started = false;
        string? failureText = null;

        try
        {
            await Task.Run(_wacomService.Start);
            started = true;
        }
        catch (Win32Exception ex)
        {
            failureText = $"Could not start Wacom Professional Service. Try running TabletMidiPad as administrator. {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            failureText = $"Could not start Wacom Professional Service. Try running TabletMidiPad as administrator. {ex.Message}";
        }
        catch (Exception ex)
        {
            failureText = $"Could not start Wacom Professional Service: {ex.Message}";
        }
        finally
        {
            if (started)
            {
                RefreshWacomServiceStatus();
                RebindInputBackend();
            }
            else
            {
                MenuWacomServiceStatus.Header = $"Service: {(failureText ?? "failed to start")}";

                try
                {
                    (_, bool canStart) = _wacomService.GetStatus();
                    MenuStartWacomService.IsEnabled = canStart;
                }
                catch
                {
                    MenuStartWacomService.IsEnabled = false;
                }
            }
        }
    }

    private void ToolbarPressureToggle_Click(object sender, RoutedEventArgs e)
    {
        _config.SendPressureGlobally = ToolbarPressureToggle.IsChecked == true;
        SyncPressureSplitBorder();
    }

    private static readonly string[] ZonePaletteColorKeys =
    {
        "ZonePalette1Color",
        "ZonePalette2Color",
        "ZonePalette3Color",
        "ZonePalette4Color",
        "ZonePalette5Color",
        "ZonePalette6Color"
    };

    private static Brush ThemeBrush(string key, Brush fallback)
        => Application.Current?.Resources[key] as Brush ?? fallback;

    private static Color ThemeColor(string key, Color fallback)
    {
        if (Application.Current?.Resources[key] is Color color)
        {
            return color;
        }

        if (Application.Current?.Resources[key] is SolidColorBrush brush)
        {
            return brush.Color;
        }

        return fallback;
    }

    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    private static Color GetZonePaletteColor(int index)
        => ThemeColor(ZonePaletteColorKeys[index % ZonePaletteColorKeys.Length], Color.FromRgb(0x67, 0xD6, 0x8A));

    private void SyncPressureSplitBorder()
    {
        bool on = ToolbarPressureToggle.IsChecked == true;
        PressureSplitBorder.Background = on ? ThemeBrush("AccentSurfaceBrush", Brushes.Transparent) : Brushes.Transparent;
        PressureSplitBorder.BorderBrush = on ? ThemeBrush("AccentBrush", Brushes.Transparent) : Brushes.Transparent;
    }

    private void SyncInputBackendSplitBorder()
    {
        InputBackendSplitBorder.Background = ThemeBrush("AccentSurfaceBrush", Brushes.Transparent);
        InputBackendSplitBorder.BorderBrush = ThemeBrush("AccentBrush", Brushes.Transparent);
    }

    private void ToolbarZoneSwitchToggle_Click(object sender, RoutedEventArgs e)
    {
        _config.AllowZoneSwitchWhilePressed = ToolbarZoneSwitchToggle.IsChecked == true;
        UpdateUiForCurrentConfig();
    }

    private void ToolbarPressureCurveDropdown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.ContextMenu is not null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        }
    }

    private void ToolbarInputBackendCycle_Click(object sender, RoutedEventArgs e)
    {
        string next = _inputMappingService.NormalizeInputBackendMode(_config.InputBackendMode) switch
        {
            "Wintab Only" => "Mouse",
            "Stylus/Mouse Only" => "Auto",
            _ => "Wintab Only"
        };

        SetInputBackendMode(next);
    }

    private void ToolbarInputBackendDropdown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.ContextMenu is not null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        }
    }

    private void PressureCurveLinear_Click(object sender, RoutedEventArgs e)
    {
        SetPressureCurve("Linear");
    }

    private void PressureCurveLogarithmic_Click(object sender, RoutedEventArgs e)
    {
        SetPressureCurve("Logarithmic");
    }

    private void PressureCurveExponential_Click(object sender, RoutedEventArgs e)
    {
        SetPressureCurve("Exponential");
    }

    private void SetPressureCurve(string curve)
    {
        _config.PressureCurve = curve;
        SyncToolbar();
    }

    private void InputBackendAuto_Click(object sender, RoutedEventArgs e)
    {
        SetInputBackendMode("Auto");
    }

    private void InputBackendWintab_Click(object sender, RoutedEventArgs e)
    {
        SetInputBackendMode("Wintab Only");
    }

    private void InputBackendStylus_Click(object sender, RoutedEventArgs e)
    {
        SetInputBackendMode("Mouse");
    }

    private void SetInputBackendMode(string mode)
    {
        string normalized = _inputMappingService.NormalizeInputBackendMode(mode);
        if (string.Equals(_config.InputBackendMode, normalized, StringComparison.Ordinal))
        {
            SyncToolbar();
            return;
        }

        _config.InputBackendMode = normalized;
        RebindInputBackend();
        SyncToolbar();
    }

    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Tablet MIDI Pad\n\nTurns a Wacom tablet into a MIDI XY controller with configurable zones.",
            "About Tablet MIDI Pad",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void AutoLayoutButton_Click(object sender, RoutedEventArgs e)
    {
        CommitCurrentZoneEdits();
        _viewModel.AutoTileZones();
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
            SendX = true,
            SendY = true,
            InvertY = true,
            SendPressure = true,
            ReleaseBehavior = ZoneReleaseBehaviors.NoSendOnRelease,
            ResetPressureOnRelease = false,
            Left = 0.0,
            Top = 0.0,
            Width = 0.5,
            Height = 0.5
        };

        _config.Zones.Add(zone);
        _viewModel.AutoTileZones();
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

        _viewModel.AutoTileZones();
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

            Color baseColor = GetZonePaletteColor(i);
            bool selected = i == _selectedZoneIndex;
            byte fillAlpha = selected ? (byte)(zone.Enabled ? 100 : 56) : (byte)(zone.Enabled ? 56 : 28);
            byte outlineAlpha = selected ? (byte)240 : (byte)(zone.Enabled ? 170 : 85);
            Color fillColor = WithAlpha(baseColor, fillAlpha);
            Color strokeColor = WithAlpha(baseColor, outlineAlpha);

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

            string bounds = $"({zone.Left:0.##},{zone.Top:0.##}) {zone.Width:0.##}\u00D7{zone.Height:0.##}";
            string axes = $"  [{(zone.SendX ? "X" : "-")}{(zone.SendY ? "Y" : "-")}{(zone.SendPressure ? "P" : "-")}]";
            string pressureFlag = zone.SendPressure ? "" : "  [no pressure]";
            string enabledFlag = zone.Enabled ? "" : "  [off]";

            var label = new TextBlock
            {
                FontSize = 11,
                Padding = new Thickness(6, 3, 6, 3),
                Background = new SolidColorBrush(WithAlpha(ThemeColor("ColorWindow", Colors.Black), 180)),
            };
            label.Inlines.Add(new System.Windows.Documents.Run(zone.Name)
            {
                Foreground = ThemeBrush("TextPrimaryBrush", Brushes.White),
                FontWeight = FontWeights.SemiBold
            });
            label.Inlines.Add(new System.Windows.Documents.Run($"  {bounds}")
            {
                Foreground = ThemeBrush("TextMutedBrush", Brushes.Gainsboro)
            });
            label.Inlines.Add(new System.Windows.Documents.Run(axes)
            {
                Foreground = new SolidColorBrush(WithAlpha(baseColor, 240))
            });
            if (pressureFlag.Length > 0)
            {
                label.Inlines.Add(new System.Windows.Documents.Run(pressureFlag)
                {
                    Foreground = ThemeBrush("DangerBrush", Brushes.IndianRed)
                });
            }
            if (enabledFlag.Length > 0)
            {
                label.Inlines.Add(new System.Windows.Documents.Run(enabledFlag)
                {
                    Foreground = ThemeBrush("TextSoftBrush", Brushes.Gray)
                });
            }

            Canvas.SetLeft(rect, rectLeft);
            Canvas.SetTop(rect, rectTop);
            Canvas.SetLeft(label, rectLeft + 6);
            Canvas.SetTop(label, rectTop + 6);

            ZoneOverlayCanvas.Children.Add(rect);
            ZoneOverlayCanvas.Children.Add(label);

            if (i == _selectedZoneIndex)
            {
                AddResizeHandles(rectLeft, rectTop, rectWidth, rectHeight, strokeColor);
            }

            if (_lastZonePoints.TryGetValue(zone, out Point lastPoint))
            {
                double markerX = zone.SendX ? lastPoint.X : 0.5;
                double markerY = zone.SendY ? lastPoint.Y : 0.5;
                double markerCenterX = rectLeft + (markerX * rectWidth);
                double markerCenterY = rectTop + (markerY * rectHeight);

                var markerRing = new Ellipse
                {
                    Width = 18,
                    Height = 18,
                    Stroke = new SolidColorBrush(WithAlpha(baseColor, 235)),
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent
                };

                var markerDot = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = ThemeBrush("AccentBrushHover", Brushes.White),
                    Stroke = ThemeBrush("SurfaceBrush", Brushes.Black),
                    StrokeThickness = 1
                };

                Canvas.SetLeft(markerRing, markerCenterX - (markerRing.Width / 2));
                Canvas.SetTop(markerRing, markerCenterY - (markerRing.Height / 2));
                Canvas.SetLeft(markerDot, markerCenterX - (markerDot.Width / 2));
                Canvas.SetTop(markerDot, markerCenterY - (markerDot.Height / 2));

                ZoneOverlayCanvas.Children.Add(markerRing);
                ZoneOverlayCanvas.Children.Add(markerDot);
            }
        }
    }

    private void AddResizeHandles(double rectLeft, double rectTop, double rectWidth, double rectHeight, Color strokeColor)
    {
        const double handleSize = 10;
        double half = handleSize / 2;
        var brush = new SolidColorBrush(Color.FromArgb(235, strokeColor.R, strokeColor.G, strokeColor.B));
        var fill = ThemeBrush("SurfaceBrush", Brushes.Black);

        foreach (Point handleCenter in new[]
        {
            new Point(rectLeft, rectTop),
            new Point(rectLeft + rectWidth / 2, rectTop),
            new Point(rectLeft + rectWidth, rectTop),
            new Point(rectLeft, rectTop + rectHeight / 2),
            new Point(rectLeft + rectWidth, rectTop + rectHeight / 2),
            new Point(rectLeft, rectTop + rectHeight),
            new Point(rectLeft + rectWidth / 2, rectTop + rectHeight),
            new Point(rectLeft + rectWidth, rectTop + rectHeight)
        })
        {
            var handle = new Rectangle
            {
                Width = handleSize,
                Height = handleSize,
                RadiusX = 3,
                RadiusY = 3,
                Fill = fill,
                Stroke = brush,
                StrokeThickness = 1.5
            };

            Canvas.SetLeft(handle, handleCenter.X - half);
            Canvas.SetTop(handle, handleCenter.Y - half);
            ZoneOverlayCanvas.Children.Add(handle);
        }
    }

    private void SendCcIfChanged(int channel, int cc, int value, bool force = false)
    {
        _midiService.SendCcIfChanged(channel, cc, value, force);
    }

    private void SendNoteIfChanged(int channel, int note, bool on, int velocity, bool force = false)
    {
        _midiService.SendNoteIfChanged(channel, note, on, velocity, force);
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
                ActiveZoneText.Text = "Active zone: none";
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
        ActiveZoneText.Text = "Active zone: none";
        ValueText.Text = "X: -, Y: -, Pressure: -";
        PenButtonsText.Text = "Pen buttons: -";
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
            ActiveZoneText.Text = hoverZone is not null
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

    private void MarkInputBackend(string backend)
    {
        _lastInputBackend = backend;
        UpdateInputBackendStatus();
    }

    private void UpdateInputBackendStatus()
    {
        string ekPart = string.IsNullOrEmpty(_expressKeyDiagnostics) ? "" : $" | {_expressKeyDiagnostics}";
        string configured = _useWintab
            ? $"Wintab enabled, {_wintabContexts.Count} context(s){ekPart}"
            : $"Stylus/mouse fallback | {_wintabDiagnostics}";

        InputBackendText.Text = $"Input: {_lastInputBackend} | {configured}";
    }

    private void RefreshWacomServiceStatus()
    {
        (string header, bool canStart) = _wacomService.GetStatus();
        MenuWacomServiceStatus.Header = header;
        MenuStartWacomService.IsEnabled = canStart;
    }

    private void UpdatePenButtonsDebug(uint buttons)
    {
        _lastWintabButtons = buttons;
        uint buttonMask = buttons & 0xFFFF;
        uint transition = (buttons >> 16) & 0xFFFF;

        string transitionText = transition switch
        {
            1 => "up",
            2 => "down",
            _ => "none"
        };

        PenButtonsText.Text = $"Pen buttons: mask=0x{buttonMask:X4} pressed={FormatPressedButtons(buttonMask)} transition={transitionText}";
    }

    private void UpdatePenButtonActionState(uint buttons)
    {
        HandlePenButtonAction(
            isHeld: (buttons & 0x0002) != 0,
            ref _lowerPenButtonHeld,
            ref _lowerPenButtonToggleState,
            _inputMappingService.NormalizePenButtonAction(_config.LowerPenButtonAction),
            _config.LowerPenButtonMidiChannel,
            _config.LowerPenButtonCc,
            _config.LowerPenButtonNote);

        HandlePenButtonAction(
            isHeld: (buttons & 0x0004) != 0,
            ref _upperPenButtonHeld,
            ref _upperPenButtonToggleState,
            _inputMappingService.NormalizePenButtonAction(_config.UpperPenButtonAction),
            _config.UpperPenButtonMidiChannel,
            _config.UpperPenButtonCc,
            _config.UpperPenButtonNote);

        UpdateModifiersText();
    }

    private void HandlePenButtonAction(
        bool isHeld,
        ref bool heldState,
        ref bool toggleState,
        string action,
        int midiChannel,
        int cc,
        int note)
    {
        bool justPressed = isHeld && !heldState;
        bool justReleased = !isHeld && heldState;

        if (justPressed)
        {
            _hasLockedPosition = false;
        }

        if (string.Equals(action, PenButtonActions.CcMomentary, StringComparison.Ordinal))
        {
            if (justPressed)
            {
                SendCcIfChanged(midiChannel, cc, 127, force: true);
            }
            else if (justReleased)
            {
                SendCcIfChanged(midiChannel, cc, 0, force: true);
            }
        }
        else if (string.Equals(action, PenButtonActions.CcToggle, StringComparison.Ordinal))
        {
            if (justPressed)
            {
                toggleState = !toggleState;
                SendCcIfChanged(midiChannel, cc, toggleState ? 127 : 0, force: true);
            }
        }
        else if (string.Equals(action, PenButtonActions.NoteMomentary, StringComparison.Ordinal))
        {
            if (justPressed)
            {
                SendNoteIfChanged(midiChannel, note, true, 127, force: true);
            }
            else if (justReleased)
            {
                SendNoteIfChanged(midiChannel, note, false, 0, force: true);
            }
        }
        else if (string.Equals(action, PenButtonActions.NoteToggle, StringComparison.Ordinal))
        {
            if (justPressed)
            {
                toggleState = !toggleState;
                SendNoteIfChanged(midiChannel, note, toggleState, toggleState ? 127 : 0, force: true);
            }
        }

        if (justReleased)
        {
            _hasLockedPosition = false;
        }

        heldState = isHeld;
    }

    private void UpdateModifiersText()
    {
        var parts = new List<string>();

        AppendPenButtonModifier(parts, "lower", _inputMappingService.NormalizePenButtonAction(_config.LowerPenButtonAction), _lowerPenButtonHeld, _lowerPenButtonToggleState);
        AppendPenButtonModifier(parts, "upper", _inputMappingService.NormalizePenButtonAction(_config.UpperPenButtonAction), _upperPenButtonHeld, _upperPenButtonToggleState);

        ModifiersText.Text = parts.Count > 0
            ? $"Modifiers: {string.Join(" | ", parts)}"
            : "Modifiers: none";
    }

    private static void AppendPenButtonModifier(List<string> parts, string label, string action, bool held, bool toggled)
    {
        string? text = action switch
        {
            var a when held && string.Equals(a, PenButtonActions.LockXY, StringComparison.Ordinal) => $"{label} XY lock",
            var a when held && string.Equals(a, PenButtonActions.LockX, StringComparison.Ordinal) => $"{label} X lock",
            var a when held && string.Equals(a, PenButtonActions.LockY, StringComparison.Ordinal) => $"{label} Y lock",
            var a when held && string.Equals(a, PenButtonActions.DisablePressure, StringComparison.Ordinal) => $"{label} pressure off",
            var a when held && string.Equals(a, PenButtonActions.EnablePressure, StringComparison.Ordinal) => $"{label} pressure on",
            var a when string.Equals(a, PenButtonActions.CcToggle, StringComparison.Ordinal) => $"{label} CC {(toggled ? "on" : "off")}",
            var a when string.Equals(a, PenButtonActions.NoteToggle, StringComparison.Ordinal) => $"{label} note {(toggled ? "on" : "off")}",
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(text))
        {
            parts.Add(text);
        }
    }

    private static string FormatPressedButtons(uint buttonMask)
    {
        if (buttonMask == 0)
        {
            return "none";
        }

        var pressed = new List<string>();
        for (int bit = 0; bit < 16; bit++)
        {
            if ((buttonMask & (1u << bit)) != 0)
            {
                pressed.Add((bit + 1).ToString());
            }
        }

        return string.Join(",", pressed);
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

}
