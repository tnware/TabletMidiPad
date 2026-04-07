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

namespace TabletMidiPad;

public partial class MainWindow : Window
{
    private const string MidiConsolePath = @"C:\Program Files\Windows MIDI Services\Tools\Console\midi.exe";
    private const string WacomServiceName = "WTabletServicePro";

    private readonly Dictionary<string, int> _lastSentValues = new();

    private IntPtr _midiOutHandle;
    private readonly Dictionary<ZoneConfig, Point> _lastZonePoints = new();
    private readonly Dictionary<ZoneConfig, Point> _smoothedZonePoints = new();
    private bool _isActive;
    private bool _isLoadingUi;
    private int _selectedZoneIndex = -1;
    private bool _useWintab;
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

    public MainWindow()
    {
        InitializeComponent();
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
        SetLoadingProgress(8);
        SetLoadingState("Booting application", "Creating the interface");
        await Task.Yield();

        SetLoadingProgress(18);
        SetLoadingState("Loading configuration", "Reading saved routing and zone settings");
        PopulateChannelList(ZoneChannelComboBox);
        PopulateReleaseBehaviorList(ZoneReleaseBehaviorComboBox);
        _config = AppConfig.Load();
        LoadConfigIntoUi(_config);
        RefreshWacomServiceStatus();
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
        AutoTileZones(_config);
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
            ValidateConfig(_config);
            _config.Save();
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
            await Task.Run(StartWacomService);
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
                    using var service = new ServiceController(WacomServiceName);
                    MenuStartWacomService.IsEnabled = service.Status != ServiceControllerStatus.Running;
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
        string next = NormalizeInputBackendMode(_config.InputBackendMode) switch
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
        string normalized = NormalizeInputBackendMode(mode);
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
        AutoTileZones(_config);
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
        AutoTileZones(_config);
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

        AutoTileZones(_config);
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
            CommitCurrentZoneEdits();
            ValidateConfig(_config);
            _config.Save();

            _lastSentValues.Clear();
            _activeZone = null;
            UpdateUiForCurrentConfig();
            DrawZoneOverlay();

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

    private void SyncToolbar()
    {
        ToolbarPressureToggle.IsChecked = _config.SendPressureGlobally;
        ToolbarZoneSwitchToggle.IsChecked = _config.AllowZoneSwitchWhilePressed;
        SyncPressureSplitBorder();
        SyncInputBackendSplitBorder();

        string curveShort = _config.PressureCurve switch
        {
            "Logarithmic" => "Log",
            "Exponential" => "Exp",
            _ => "Lin"
        };
        ToolbarPressureLabel.Text = $"Pressure ({curveShort})";

        foreach (var item in PressureCurveMenu.Items)
        {
            if (item is MenuItem mi)
            {
                bool active = string.Equals(mi.Header as string, _config.PressureCurve, StringComparison.Ordinal);
                mi.Icon = active ? new System.Windows.Controls.TextBlock { Text = "\u2713", Foreground = Brushes.White, FontSize = 11 } : null;
            }
        }

        string backendLabel = NormalizeInputBackendMode(_config.InputBackendMode) switch
        {
            "Wintab Only" => "Input (Wintab)",
            "Stylus/Mouse Only" => "Input (Mouse)",
            _ => "Input (Auto)"
        };

        ToolbarInputBackendLabel.Text = backendLabel;

        foreach (var item in InputBackendMenu.Items)
        {
            if (item is MenuItem mi)
            {
                bool active = (mi.Header as string) switch
                {
                    "Wintab" => string.Equals(_config.InputBackendMode, "Wintab Only", StringComparison.Ordinal),
                    "Mouse" => string.Equals(_config.InputBackendMode, "Stylus/Mouse Only", StringComparison.Ordinal),
                    _ => string.Equals(NormalizeInputBackendMode(_config.InputBackendMode), "Auto", StringComparison.Ordinal)
                };

                mi.Icon = active ? new System.Windows.Controls.TextBlock { Text = "\u2713", Foreground = Brushes.White, FontSize = 11 } : null;
            }
        }
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
            ZoneSendXCheckBox.IsChecked = false;
            ZoneSendYCheckBox.IsChecked = false;
            ZoneInvertXCheckBox.IsChecked = false;
            ZoneInvertYCheckBox.IsChecked = false;
            ZoneSendPressureCheckBox.IsChecked = false;
            ZoneReleaseBehaviorComboBox.SelectedIndex = -1;
            ZoneReleaseBehaviorComboBox.Text = string.Empty;
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
        ZoneSendXCheckBox.IsChecked = zone.SendX;
        ZoneSendYCheckBox.IsChecked = zone.SendY;
        ZoneInvertXCheckBox.IsChecked = zone.InvertX;
        ZoneInvertYCheckBox.IsChecked = zone.InvertY;
        ZoneSendPressureCheckBox.IsChecked = zone.SendPressure;
        ZoneReleaseBehaviorComboBox.SelectedItem = NormalizeReleaseBehavior(zone.ReleaseBehavior);
        ZoneReleaseBehaviorComboBox.Text = NormalizeReleaseBehavior(zone.ReleaseBehavior);

        _isLoadingUi = false;
    }

    private void CommitCurrentZoneEdits()
    {
        if (_selectedZoneIndex < 0 || _selectedZoneIndex >= _config.Zones.Count)
        {
            return;
        }

        var zone = _config.Zones[_selectedZoneIndex];
        _config.MaxZoneRows = ReadInt(MaxZoneRowsTextBox.Text, 1, 16, "Max Zone Rows");
        _config.MaxZonesPerRow = ReadInt(MaxZonesPerRowTextBox.Text, 1, 32, "Max Zones Per Row");
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
        zone.SendX = ZoneSendXCheckBox.IsChecked != false;
        zone.SendY = ZoneSendYCheckBox.IsChecked != false;
        zone.InvertX = ZoneInvertXCheckBox.IsChecked == true;
        zone.InvertY = ZoneInvertYCheckBox.IsChecked == true;
        zone.SendPressure = ZoneSendPressureCheckBox.IsChecked == true;
        zone.ReleaseBehavior = NormalizeReleaseBehavior(ZoneReleaseBehaviorComboBox.Text);
        zone.ResetPressureOnRelease = string.Equals(zone.ReleaseBehavior, ZoneReleaseBehaviors.ResetPressureOnly, StringComparison.Ordinal);

        RefreshZoneList();
        ZoneListBox.SelectedIndex = _selectedZoneIndex;
    }

    private static void ValidateConfig(AppConfig config)
    {
        if (config.Zones.Count == 0)
        {
            throw new InvalidOperationException("At least one zone is required.");
        }

        if (config.MaxZoneRows < 1)
        {
            throw new InvalidOperationException("Max rows must be at least 1.");
        }

        if (config.MaxZonesPerRow < 1)
        {
            throw new InvalidOperationException("Max zones per row must be at least 1.");
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

            _ = NormalizeReleaseBehavior(zone.ReleaseBehavior);
        }
    }

    private static void AutoTileZones(AppConfig config)
    {
        IList<ZoneConfig> zones = config.Zones;
        if (zones.Count == 0)
        {
            return;
        }

        int count = zones.Count;
        int maxRows = Math.Max(1, config.MaxZoneRows);
        int maxPerRow = Math.Max(1, config.MaxZonesPerRow);
        int capacity = maxRows * maxPerRow;
        int rows = count <= capacity
            ? Math.Min(maxRows, Math.Max(1, (int)Math.Ceiling((double)count / maxPerRow)))
            : Math.Max(1, (int)Math.Ceiling((double)count / maxPerRow));

        double cellHeight = 1.0 / rows;

        for (int index = 0; index < count; index++)
        {
            int row = index / maxPerRow;
            int colsInThisRow = Math.Min(maxPerRow, count - row * maxPerRow);
            int column = index % maxPerRow;
            double cellWidth = 1.0 / colsInThisRow;

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
        StatusText.Text = "Connected";
        RoutingText.Text = $"Send to '{_config.DawPortName}' via '{_config.AppPortName}'";
        UpdateInputBackendStatus();
        ActiveZoneText.Text = "Active zone: none";
        string switchMode = _config.AllowZoneSwitchWhilePressed ? "latch on enter" : "lock zone until release";
        ConfigSummaryText.Text = $"{_config.Zones.Count} zone(s) configured | layout {_config.MaxZoneRows} row(s) max / {_config.MaxZonesPerRow} per row | {switchMode}";
        ValueText.Text = "X: -, Y: -, Pressure: -";
        PenButtonsText.Text = "Pen buttons: -";
        ModifiersText.Text = "Modifiers: none";
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
            newLeft = Snap(Math.Clamp(_resizeOrigLeft + dxNorm, 0.0, _resizeOrigLeft + _resizeOrigWidth - minSize), snap);
            newWidth = (_resizeOrigLeft + _resizeOrigWidth) - newLeft;
        }
        else if ((_resizeEdge & ResizeEdge.Right) != 0)
        {
            double newRight = Snap(Math.Clamp(_resizeOrigLeft + _resizeOrigWidth + dxNorm, _resizeOrigLeft + minSize, 1.0), snap);
            newWidth = newRight - _resizeOrigLeft;
        }

        if ((_resizeEdge & ResizeEdge.Top) != 0)
        {
            newTop = Snap(Math.Clamp(_resizeOrigTop + dyNorm, 0.0, _resizeOrigTop + _resizeOrigHeight - minSize), snap);
            newHeight = (_resizeOrigTop + _resizeOrigHeight) - newTop;
        }
        else if ((_resizeEdge & ResizeEdge.Bottom) != 0)
        {
            double newBottom = Snap(Math.Clamp(_resizeOrigTop + _resizeOrigHeight + dyNorm, _resizeOrigTop + minSize, 1.0), snap);
            newHeight = newBottom - _resizeOrigTop;
        }

        newLeft = Math.Clamp(newLeft, 0.0, 1.0);
        newTop = Math.Clamp(newTop, 0.0, 1.0);
        newWidth = Snap(Math.Clamp(newWidth, minSize, 1.0 - newLeft), snap);
        newHeight = Snap(Math.Clamp(newHeight, minSize, 1.0 - newTop), snap);

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
        SettingsResultText.Foreground = Brushes.DarkSeaGreen;
        SettingsResultText.Text = "Zone resized. Click Apply Settings to save.";
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
        PenButtonsText.Text = "Pen buttons: stylus fallback";
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
        PenButtonsText.Text = "Pen buttons: mouse fallback";
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
            ActiveZoneText.Text = "Active zone: none";
            ValueText.Text = "Outside configured zones";
            return;
        }

        var local = NormalizeWithinZone(zone, xNorm, yNorm);
        local = ApplyPositionConditioning(zone, local.x, local.y);
        string lowerAction = NormalizePenButtonAction(_config.LowerPenButtonAction);
        string upperAction = NormalizePenButtonAction(_config.UpperPenButtonAction);
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
        int x = ToCc(local.x, zone.InvertX);
        int y = ToCc(local.y, zone.InvertY);
        int curvedPressure = ApplyPressureCurve(pressure);

        ActiveZoneText.Text = $"Active zone: {zone.Name}";
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
        ValueText.Text = $"{zone.Name} -> X: {xText}, Y: {yText}, Pressure: {pressureText}";

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
        ActiveZoneText.Text = "Active zone: none";
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

        string behavior = NormalizeReleaseBehavior(zone.ReleaseBehavior);

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

    private void SendNoteIfChanged(int channel, int note, bool on, int velocity, bool force = false)
    {
        if (_midiOutHandle == IntPtr.Zero) return;

        string key = $"note:{channel}:{note}";
        int state = on ? 1 : 0;
        velocity = Math.Clamp(velocity, 0, 127);

        if (!force && _lastSentValues.TryGetValue(key, out int lastState) && lastState == state)
        {
            return;
        }

        byte status = (byte)((on ? 0x90 : 0x80) | ((channel - 1) & 0x0F));
        byte noteByte = (byte)(note & 0x7F);
        byte velocityByte = (byte)(velocity & 0x7F);

        int packedMessage = status | (noteByte << 8) | (velocityByte << 16);
        int result = midiOutShortMsg(_midiOutHandle, packedMessage);
        if (result != 0)
        {
            throw new Win32Exception(result, $"midiOutShortMsg failed with code {result}.");
        }

        _lastSentValues[key] = state;
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
        string backendMode = NormalizeInputBackendMode(_config.InputBackendMode);
        if (string.Equals(backendMode, "Stylus/Mouse Only", StringComparison.Ordinal))
        {
            ShutdownWintab();
            _wintabDiagnostics = "Manual stylus/mouse mode selected";
            _lastInputBackend = "Stylus/Mouse Fallback";
            UpdateInputBackendStatus();
            return;
        }

        if (!WintabInterop.IsAvailable())
        {
            _useWintab = false;
            _wintabDiagnostics = "Wintab32.dll not available";
            _lastInputBackend = string.Equals(backendMode, "Wintab Only", StringComparison.Ordinal)
                ? "Wintab unavailable"
                : "Stylus/Mouse Fallback";
            SetLoadingState(
                "Preparing tablet input",
                string.Equals(backendMode, "Wintab Only", StringComparison.Ordinal)
                    ? "Wintab requested, but Wintab32.dll is not available"
                    : "Wintab not found, using stylus and mouse fallback");
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

            SetLoadingState(
                "Preparing tablet input",
                _useWintab
                    ? $"Wintab digitizer input active on {openedContexts} tablet context(s)"
                    : string.Equals(backendMode, "Wintab Only", StringComparison.Ordinal)
                        ? "Wintab requested, but no digitizer contexts could open"
                        : "Wintab detected, but no digitizer contexts could open"
            );
            UpdateInputBackendStatus();
        }
        catch (Exception ex)
        {
            _useWintab = false;
            _lastInputBackend = string.Equals(backendMode, "Wintab Only", StringComparison.Ordinal)
                ? "Wintab unavailable"
                : "Stylus/Mouse Fallback";
            _wintabDiagnostics = ex.Message;
            SetLoadingState(
                "Preparing tablet input",
                string.Equals(backendMode, "Wintab Only", StringComparison.Ordinal)
                    ? "Wintab failed to initialize"
                    : "Wintab failed, using stylus and mouse fallback");
            UpdateInputBackendStatus();
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
        _expressKeyHeld.Clear();
        _expressKeyToggleState.Clear();
        bool messageBaseInitialized = false;

        _expressKeysMask = WintabInterop.GetExtensionMask(WintabInterop.WTX_EXPKEYS2);

        try
        {
            var defaultContext = WintabInterop.GetDefaultDigitizingContext();
            defaultContext.Name = "TabletMidiPad Default Digitizer";
            defaultContext.Options |= WintabInterop.CXO_MESSAGES;
            defaultContext.Options &= ~WintabInterop.CXO_SYSTEM;
            defaultContext.Locks =
                WintabInterop.CXL_INSIZE |
                WintabInterop.CXL_INASPECT |
                WintabInterop.CXL_SENSITIVITY |
                WintabInterop.CXL_MARGIN |
                WintabInterop.CXL_SYSOUT;
            defaultContext.PktData = WintabInterop.PK_X | WintabInterop.PK_Y | WintabInterop.PK_NORMAL_PRESSURE | WintabInterop.PK_BUTTONS;
            if (_expressKeysMask > 0)
                defaultContext.PktData |= _expressKeysMask;
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
                _wintabPacketExtMessage = unchecked((int)defaultContext.MsgBase + WintabInterop.WT_PACKETEXT);
                _wintabContextOverlapMessage = unchecked((int)defaultContext.MsgBase + WintabInterop.WT_CTXOVERLAP);
                _wintabProximityMessage = unchecked((int)defaultContext.MsgBase + WintabInterop.WT_PROXIMITY);
                _wintabInfoChangeMessage = unchecked((int)defaultContext.MsgBase + WintabInterop.WT_INFOCHANGE);
                messageBaseInitialized = true;

                uint handle = WintabInterop.WTOpenA(_wintabMessageSource!.Handle, ref defaultContext, true);
                if (handle != 0)
                {
                    SetupExpressKeyOverrides(handle, (byte)Math.Max(0, defaultDeviceIndex));
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
            context.Locks =
                WintabInterop.CXL_INSIZE |
                WintabInterop.CXL_INASPECT |
                WintabInterop.CXL_SENSITIVITY |
                WintabInterop.CXL_MARGIN |
                WintabInterop.CXL_SYSOUT;
            context.PktData = WintabInterop.PK_X | WintabInterop.PK_Y | WintabInterop.PK_NORMAL_PRESSURE | WintabInterop.PK_BUTTONS;
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

            if (!messageBaseInitialized)
            {
                _wintabPacketMessage = unchecked((int)context.MsgBase + WintabInterop.WT_PACKET);
                _wintabPacketExtMessage = unchecked((int)context.MsgBase + WintabInterop.WT_PACKETEXT);
                _wintabContextOverlapMessage = unchecked((int)context.MsgBase + WintabInterop.WT_CTXOVERLAP);
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

            SetupExpressKeyOverrides(handle, (byte)deviceIndex);
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
        _lastInputBackend = _useWintab
            ? "Wintab"
            : string.Equals(NormalizeInputBackendMode(_config.InputBackendMode), "Wintab Only", StringComparison.Ordinal)
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
        string action = NormalizePenButtonAction(keyConfig.Action);
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

    private bool AllowsStylusMouseFallback()
    {
        return !string.Equals(NormalizeInputBackendMode(_config.InputBackendMode), "Wintab Only", StringComparison.Ordinal);
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

    private int ApplyPressureCurve(int pressure)
    {
        double normalized = Math.Clamp(pressure / 127.0, 0.0, 1.0);
        double curved = _config.PressureCurve switch
        {
            "Logarithmic" => Math.Sqrt(normalized),
            "Exponential" => Math.Pow(normalized, 2.0),
            _ => normalized
        };

        return Math.Clamp((int)Math.Round(curved * 127.0), 0, 127);
    }

    private static double Snap(double value, double increment)
    {
        return Math.Round(value / increment) * increment;
    }

    private static string NormalizeInputBackendMode(string? text)
    {
        string value = (text ?? string.Empty).Trim();
        return value switch
        {
            "Wintab" => "Wintab Only",
            "Mouse" => "Stylus/Mouse Only",
            "Wintab Only" => value,
            "Stylus/Mouse Only" => value,
            _ => "Auto"
        };
    }

    private static string NormalizeReleaseBehavior(string? text)
    {
        string value = (text ?? string.Empty).Trim();
        return value switch
        {
            "Hold Last Values" => ZoneReleaseBehaviors.Hold,
            ZoneReleaseBehaviors.Hold => value,
            ZoneReleaseBehaviors.ResetAll => value,
            ZoneReleaseBehaviors.NoSendOnRelease => value,
            _ => ZoneReleaseBehaviors.ResetPressureOnly
        };
    }

    private static string NormalizePenButtonAction(string? text)
    {
        string value = (text ?? string.Empty).Trim();
        return value switch
        {
            PenButtonActions.None => value,
            PenButtonActions.LockXY => value,
            PenButtonActions.LockX => value,
            PenButtonActions.LockY => value,
            PenButtonActions.DisablePressure => value,
            PenButtonActions.EnablePressure => value,
            PenButtonActions.CcMomentary => value,
            PenButtonActions.CcToggle => value,
            PenButtonActions.NoteMomentary => value,
            PenButtonActions.NoteToggle => value,
            _ => PenButtonActions.None
        };
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
        try
        {
            using var service = new ServiceController(WacomServiceName);
            string displayName = string.IsNullOrWhiteSpace(service.DisplayName) ? WacomServiceName : service.DisplayName;
            bool running = service.Status == ServiceControllerStatus.Running;
            MenuWacomServiceStatus.Header = running
                ? $"Service: {displayName} running"
                : $"Service: {displayName} {service.Status}";
            MenuStartWacomService.IsEnabled = !running;
        }
        catch (InvalidOperationException)
        {
            MenuWacomServiceStatus.Header = "Service: not found";
            MenuStartWacomService.IsEnabled = false;
        }
        catch (Exception ex)
        {
            MenuWacomServiceStatus.Header = $"Service: {ex.Message}";
            MenuStartWacomService.IsEnabled = false;
        }
    }

    private static void StartWacomService()
    {
        using var service = new ServiceController(WacomServiceName);

        if (service.Status == ServiceControllerStatus.Running)
        {
            return;
        }

        service.Start();
        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
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
            NormalizePenButtonAction(_config.LowerPenButtonAction),
            _config.LowerPenButtonMidiChannel,
            _config.LowerPenButtonCc,
            _config.LowerPenButtonNote);

        HandlePenButtonAction(
            isHeld: (buttons & 0x0004) != 0,
            ref _upperPenButtonHeld,
            ref _upperPenButtonToggleState,
            NormalizePenButtonAction(_config.UpperPenButtonAction),
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

        AppendPenButtonModifier(parts, "lower", NormalizePenButtonAction(_config.LowerPenButtonAction), _lowerPenButtonHeld, _lowerPenButtonToggleState);
        AppendPenButtonModifier(parts, "upper", NormalizePenButtonAction(_config.UpperPenButtonAction), _upperPenButtonHeld, _upperPenButtonToggleState);

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
