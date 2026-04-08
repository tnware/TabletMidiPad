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
using TabletMidiPad.Core.Interaction;
using TabletMidiPad.Services;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Views;

public partial class MainWindow : Window
{
    private const string MidiConsolePath = @"C:\Program Files\Windows MIDI Services\Tools\Console\midi.exe";
    private readonly Dictionary<string, int> _lastSentValues = new();

    private IntPtr _midiOutHandle;
    private readonly Dictionary<ZoneConfig, Point> _lastZonePoints = new();
    private bool _isActive;
    private bool _isLoadingUi;
    private int _selectedZoneIndex = -1;
    private bool _useWintab;
    private int _selectedDeviceIndex = -1;
    private int _expressKeyCount;
    private string _expressKeyDiagnostics = "";

    private readonly IConfigStore _configStore;
    private readonly IMidiEngine _midiEngine;
    private readonly IWacomServiceController _wacomServiceController;
    private readonly IWintabInputBackend _wintabInputBackend;
    private readonly IPreferencesDialogService _preferencesDialogService;
    private readonly IZoneConfigurationService _zoneConfigurationService;
    private readonly IZoneEditorService _zoneEditorService;
    private readonly IZoneOverlayRenderer _zoneOverlayRenderer;
    private readonly IConfigValueParser _configValueParser;
    private readonly IPadInteractionEngine _padInteractionEngine;
    private readonly MainViewModel _viewModel;
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
    private readonly InteractionState _interactionState = new();

    private PadInteractionConfig BuildInteractionConfig()
    {
        return new PadInteractionConfig(
            SendPressureGlobally: _config.SendPressureGlobally,
            AllowZoneSwitchWhilePressed: _config.AllowZoneSwitchWhilePressed,
            MousePressureValue: _config.MousePressureValue,
            PositionDeadZone: _config.PositionDeadZone,
            PositionSmoothing: _config.PositionSmoothing,
            PressureCurve: _config.PressureCurve,
            LowerPenButtonAction: _config.LowerPenButtonAction,
            LowerPenButtonMidiChannel: _config.LowerPenButtonMidiChannel,
            LowerPenButtonCc: _config.LowerPenButtonCc,
            LowerPenButtonNote: _config.LowerPenButtonNote,
            UpperPenButtonAction: _config.UpperPenButtonAction,
            UpperPenButtonMidiChannel: _config.UpperPenButtonMidiChannel,
            UpperPenButtonCc: _config.UpperPenButtonCc,
            UpperPenButtonNote: _config.UpperPenButtonNote,
            ExpressKeys: _config.ExpressKeys.Select(k => new ExpressKeyConfigSnapshot(k.Action, k.MidiChannel, k.Cc, k.Note)).ToList());
    }

    private IReadOnlyList<ZoneSnapshot> BuildZoneSnapshots()
    {
        return _config.Zones.Select(z => new ZoneSnapshot(
            Name: z.Name,
            Enabled: z.Enabled,
            MidiChannel: z.MidiChannel,
            CcX: z.CcX,
            CcY: z.CcY,
            CcPressure: z.CcPressure,
            SendX: z.SendX,
            SendY: z.SendY,
            InvertX: z.InvertX,
            InvertY: z.InvertY,
            SendPressure: z.SendPressure,
            ReleaseBehavior: z.ReleaseBehavior,
            Left: z.Left,
            Top: z.Top,
            Width: z.Width,
            Height: z.Height)).ToList();
    }

    private void ApplyInteractionResult(InteractionResult result)
    {
        foreach (MidiMessage message in result.MidiMessages)
        {
            if (message.Kind == MidiMessageKind.ControlChange)
            {
                SendCcIfChanged(message.Channel, message.Number, message.Value, force: message.Force);
            }
            else
            {
                // For notes, Value encodes velocity for "on" and 0 for "off".
                bool on = message.Value > 0;
                SendNoteIfChanged(message.Channel, message.Number, on, message.Value, force: message.Force);
            }
        }

        _viewModel.ActiveZoneText = result.ActiveZoneLabel;
        _viewModel.ValueText = result.ValueLabel;
        _viewModel.ModifiersText = result.ModifiersLabel;

        // Mirror engine state back into overlay marker map so overlay rendering can remain unchanged for now.
        _lastZonePoints.Clear();
        foreach ((int zoneIndex, (double x, double y) local) in _interactionState.LastLocalByZone)
        {
            if (zoneIndex < 0 || zoneIndex >= _config.Zones.Count)
            {
                continue;
            }

            _lastZonePoints[_config.Zones[zoneIndex]] = new Point(local.x, local.y);
        }
    }

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
        : this(
            new JsonConfigStore(AppConfig.GetConfigPath()),
            new WinMmMidiEngine(),
            new WacomServiceController(),
            new WintabInputBackend(),
            new PreferencesDialogService(),
            new ZoneConfigurationService(),
            new ZoneEditorService(),
            new ZoneOverlayRenderer(),
            new ConfigValueParser(),
            new PadInteractionEngine())
    {
    }

    internal MainWindow(
        IConfigStore configStore,
        IMidiEngine midiEngine,
        IWacomServiceController wacomServiceController,
        IWintabInputBackend wintabInputBackend,
        IPreferencesDialogService preferencesDialogService,
        IZoneConfigurationService zoneConfigurationService,
        IZoneEditorService zoneEditorService,
        IZoneOverlayRenderer zoneOverlayRenderer,
        IConfigValueParser configValueParser,
        IPadInteractionEngine padInteractionEngine)
    {
        _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        _midiEngine = midiEngine ?? throw new ArgumentNullException(nameof(midiEngine));
        _wacomServiceController = wacomServiceController ?? throw new ArgumentNullException(nameof(wacomServiceController));
        _wintabInputBackend = wintabInputBackend ?? throw new ArgumentNullException(nameof(wintabInputBackend));
        _preferencesDialogService = preferencesDialogService ?? throw new ArgumentNullException(nameof(preferencesDialogService));
        _zoneConfigurationService = zoneConfigurationService ?? throw new ArgumentNullException(nameof(zoneConfigurationService));
        _zoneEditorService = zoneEditorService ?? throw new ArgumentNullException(nameof(zoneEditorService));
        _zoneOverlayRenderer = zoneOverlayRenderer ?? throw new ArgumentNullException(nameof(zoneOverlayRenderer));
        _configValueParser = configValueParser ?? throw new ArgumentNullException(nameof(configValueParser));
        _padInteractionEngine = padInteractionEngine ?? throw new ArgumentNullException(nameof(padInteractionEngine));
        _wintabInputBackend.PacketReceived += (_, e) => HandleWintabPacket(e);
        _wintabInputBackend.ExpressKeyReceived += (_, e) => HandleExpressKeyPacket(e);
        _wintabInputBackend.ProximityChanged += (_, e) =>
        {
            _wintabDiagnostics = e.Diagnostics;
            UpdateInputBackendStatus();
            if (!e.Entering)
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
        };
        _wintabInputBackend.DiagnosticsChanged += (_, diagnostics) =>
        {
            _wintabDiagnostics = diagnostics;
            UpdateInputBackendStatus();
        };
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        InitializeCommands();
        WindowChromeTheme.ApplyDarkNonClientChrome(this);
        ConfigureNumericInputs();
        ContentRendered += OnContentRendered;
        Closed += (_, _) =>
        {
            ShutdownMidi();
            ShutdownWintab();
            _wintabInputBackend.Dispose();
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

    private void InitializeCommands()
    {
        _viewModel.ConfigureCommands(
            saveSettings: ApplySettings,
            resetDefaults: MenuResetDefaults,
            openPreferences: MenuPreferences,
            exit: Close,
            refreshWacomStatus: RefreshWacomServiceStatus,
            reconnectPen: ReconnectPenCore,
            startWacomService: () => _ = StartWacomServiceAsync(),
            showAbout: MenuAbout,
            addZone: AddZone,
            removeZone: RemoveZone,
            autoLayout: AutoLayoutZones);
    }

    private async void OnContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= OnContentRendered;
        await InitializeAppAsync();
    }

    private void MenuResetDefaults()
    {
        _config = AppConfig.Default;
        _zoneConfigurationService.AutoTileZones(_config);
        LoadConfigIntoUi(_config);
        ApplySettings();
    }

    private void MenuPreferences()
    {
        int effectiveExpressKeyCount = Math.Max(_expressKeyCount, _config.ExpressKeys.Count);
        ExpressKeyConfig.EnsureCount(_config.ExpressKeys, effectiveExpressKeyCount);
        PreferencesDialogResult? result = _preferencesDialogService.ShowDialog(this, _config, effectiveExpressKeyCount);
        if (result is null)
        {
            return;
        }

        string previousAppPortName = _config.AppPortName;
        string previousDawPortName = _config.DawPortName;
        string previousAssociationId = _config.LoopbackAssociationId;
        string previousInputBackendMode = _config.InputBackendMode;

        _config.DawPortName = result.DawPortName;
        _config.AppPortName = result.AppPortName;
        _config.LoopbackAssociationId = result.AssociationId;
        _config.MousePressureValue = result.MousePressureValue;
        _config.PositionDeadZone = result.PositionDeadZone;
        _config.PositionSmoothing = result.PositionSmoothing;
        _config.PressureCurve = result.PressureCurve;
        _config.InputBackendMode = result.InputBackendMode;
        _config.LowerPenButtonAction = result.LowerPenButtonAction;
        _config.LowerPenButtonMidiChannel = result.LowerPenButtonMidiChannel;
        _config.LowerPenButtonCc = result.LowerPenButtonCc;
        _config.LowerPenButtonNote = result.LowerPenButtonNote;
        _config.UpperPenButtonAction = result.UpperPenButtonAction;
        _config.UpperPenButtonMidiChannel = result.UpperPenButtonMidiChannel;
        _config.UpperPenButtonCc = result.UpperPenButtonCc;
        _config.UpperPenButtonNote = result.UpperPenButtonNote;
        _config.SendPressureGlobally = result.SendPressureGlobally;
        _config.AllowZoneSwitchWhilePressed = result.AllowZoneSwitchWhilePressed;
        _config.ExpressKeys = result.ExpressKeys;

        CommitCurrentZoneEdits();

        try
        {
            ValidateConfig(_config);
            _configStore.Save(_config);
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

    private async Task StartWacomServiceAsync()
    {
        MenuStartWacomService.IsEnabled = false;
        MenuWacomServiceStatus.Header = "Service: starting...";
        bool started = false;
        string? failureText = null;

        try
        {
            await Task.Run(() => _wacomServiceController.Start());
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
                RefreshWacomServiceStatus();
            }
        }
    }

    private void MenuAbout()
    {
        MessageBox.Show(
            "Tablet MIDI Pad\n\nTurns a Wacom tablet into a MIDI XY controller with configurable zones.",
            "About Tablet MIDI Pad",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void AutoLayoutZones()
    {
        CommitCurrentZoneEdits();
        _zoneConfigurationService.AutoTileZones(_config);
        RefreshZoneList();
        ZoneListBox.SelectedIndex = Math.Clamp(_selectedZoneIndex, 0, _config.Zones.Count - 1);
        LoadSelectedZoneIntoEditor();
        DrawZoneOverlay();
        SettingsResultText.Foreground = Brushes.DarkSeaGreen;
        SettingsResultText.Text = "Zones re-tiled across the pad. Click Apply Settings to save.";
    }

    private void AddZone()
    {
        CommitCurrentZoneEdits();

        int nextCcStart = _zoneConfigurationService.GetNextAvailableCcStart(_config);

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
        _zoneConfigurationService.AutoTileZones(_config);
        RefreshZoneList();
        ZoneListBox.SelectedIndex = _config.Zones.Count - 1;
        LoadSelectedZoneIntoEditor();
        DrawZoneOverlay();
        SettingsResultText.Foreground = Brushes.DarkSeaGreen;
        SettingsResultText.Text = "Zone added. Click Apply Settings to save.";
    }

    private void RemoveZone()
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

        _zoneConfigurationService.AutoTileZones(_config);
        RefreshZoneList();
        ZoneListBox.SelectedIndex = Math.Clamp(_selectedZoneIndex, 0, _config.Zones.Count - 1);
        LoadSelectedZoneIntoEditor();
        DrawZoneOverlay();
        SettingsResultText.Foreground = Brushes.DarkSeaGreen;
        SettingsResultText.Text = "Zone removed. Click Apply Settings to save.";
    }

    private async Task InitializeAppAsync()
    {
        PopulateChannelList(ZoneChannelComboBox);
        PopulateReleaseBehaviorList(ZoneReleaseBehaviorComboBox);
        _config = _configStore.Load();
        LoadConfigIntoUi(_config);
        RefreshWacomServiceStatus();

        InitializeWintab();

        MidiStatusPanel.Visibility = Visibility.Visible;
        _viewModel.MidiStatusText = "Connecting MIDI...";
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
            _viewModel.MidiStatusText = "MIDI init failed";
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

    private void ReconnectPenCore()
    {
        CommitCurrentZoneEdits();
        RebindInputBackend();
        SettingsResultText.Foreground = Brushes.DarkSeaGreen;
        SettingsResultText.Text = "Input backend reconnected.";
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
            _zoneConfigurationService.ValidateConfig(_config);
            _configStore.Save(_config);

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
        _midiEngine.Restart(_config);
        _midiEngine.ClearCachedValues();
        _activeZone = null;
        UpdateUiForCurrentConfig();
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
        _viewModel.ToolbarPressureLabelText = $"Pressure ({curveShort})";
        _viewModel.SelectedPressureCurve = _config.PressureCurve;

        string backendLabel = NormalizeInputBackendMode(_config.InputBackendMode) switch
        {
            "Wintab Only" => "Input (Wintab)",
            "Stylus/Mouse Only" => "Input (Mouse)",
            _ => "Input (Auto)"
        };

        _viewModel.ToolbarInputBackendLabelText = backendLabel;
        _viewModel.SelectedInputBackendMode = NormalizeInputBackendMode(_config.InputBackendMode) switch
        {
            "Wintab Only" => "Wintab",
            "Stylus/Mouse Only" => "Mouse",
            _ => "Auto"
        };
    }

    private void RefreshZoneList()
    {
        _zoneEditorService.RefreshZoneList(_viewModel.Zones, _config);
    }

    private void LoadSelectedZoneIntoEditor()
    {
        _isLoadingUi = true;

        _zoneEditorService.LoadSelectedZoneIntoEditor(_viewModel, _config, _selectedZoneIndex);
        ZoneChannelComboBox.SelectedItem = _selectedZoneIndex >= 0 && _selectedZoneIndex < _config.Zones.Count
            ? _config.Zones[_selectedZoneIndex].MidiChannel
            : null;
        ZoneReleaseBehaviorComboBox.SelectedItem = _viewModel.ZoneReleaseBehavior;

        _isLoadingUi = false;
    }

    private void CommitCurrentZoneEdits()
    {
        _zoneEditorService.CommitCurrentZoneEdits(
            _config,
            _viewModel,
            _selectedZoneIndex,
            MaxZoneRowsTextBox.Text,
            MaxZonesPerRowTextBox.Text);

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

    private async Task InitializeMidiAsync()
    {
        try
        {
            await _midiEngine.InitializeAsync(_config, text => Dispatcher.Invoke(() => _viewModel.MidiStatusText = text));
            _midiEngine.ClearCachedValues();
            _activeZone = null;
            UpdateUiForCurrentConfig();
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = "MIDI init failed";
            _viewModel.ValueText = ex.Message;
            throw;
        }
    }

    private void UpdateUiForCurrentConfig()
    {
        _viewModel.StatusText = "Connected";
        _viewModel.RoutingText = $"Send to '{_config.DawPortName}' via '{_config.AppPortName}'";
        UpdateInputBackendStatus();
        _viewModel.ActiveZoneText = "Active zone: none";
        string switchMode = _config.AllowZoneSwitchWhilePressed ? "latch on enter" : "lock zone until release";
        _viewModel.ConfigSummaryText = $"{_config.Zones.Count} zone(s) configured | layout {_config.MaxZoneRows} row(s) max / {_config.MaxZonesPerRow} per row | {switchMode}";
        _viewModel.ValueText = "X: -, Y: -, Pressure: -";
        _viewModel.PenButtonsText = "Pen buttons: -";
        _viewModel.ModifiersText = "Modifiers: none";
    }

    private void ShutdownMidi() => _midiEngine.Shutdown(_config);

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

        ProcessInteractionTick(
            xPx: point.X,
            yPx: point.Y,
            xNorm: xNorm,
            yNorm: yNorm,
            pressure: pressure,
            isDown: true,
            isHovering: false,
            penButtons: 0,
            backend: InteractionBackend.StylusFallback);
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

        ProcessInteractionTick(
            xPx: point.X,
            yPx: point.Y,
            xNorm: xNorm,
            yNorm: yNorm,
            pressure: _config.MousePressureValue,
            isDown: true,
            isHovering: false,
            penButtons: 0,
            backend: InteractionBackend.MouseFallback);
    }

    private void ProcessInteractionTick(
        double xPx,
        double yPx,
        double xNorm,
        double yNorm,
        int pressure,
        bool isDown,
        bool isHovering,
        uint penButtons,
        InteractionBackend backend)
    {
        // Cursor is purely UI; keep it here for now.
        if (isHovering || isDown)
        {
            CursorDot.Visibility = Visibility.Visible;
            Canvas.SetLeft(CursorDot, xPx - CursorDot.Width / 2);
            Canvas.SetTop(CursorDot, yPx - CursorDot.Height / 2);
        }

        InteractionResult result = _padInteractionEngine.Process(
            BuildInteractionConfig(),
            BuildZoneSnapshots(),
            _interactionState,
            new InteractionInput(
                XNorm: xNorm,
                YNorm: yNorm,
                Pressure: pressure,
                IsDown: isDown,
                IsHovering: isHovering,
                PenButtons: penButtons,
                Backend: backend));

        ApplyInteractionResult(result);

        _activeZone = result.ActiveZoneIndex >= 0 && result.ActiveZoneIndex < _config.Zones.Count
            ? _config.Zones[result.ActiveZoneIndex]
            : null;

        DrawZoneOverlay();
    }

    private void HandleRelease()
    {
        // Release tick goes through engine so release behavior stays centralized.
        ProcessInteractionTick(
            xPx: 0,
            yPx: 0,
            xNorm: 0,
            yNorm: 0,
            pressure: 0,
            isDown: false,
            isHovering: false,
            penButtons: _lastWintabButtons,
            backend: InteractionBackend.Unknown);

        CursorDot.Visibility = Visibility.Collapsed;
        CursorDot.Opacity = 0.85;
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
        IReadOnlyList<ZoneOverlayZone> zones = _config.Zones
            .Select(zone => new ZoneOverlayZone(
                Name: zone.Name,
                Enabled: zone.Enabled,
                SendX: zone.SendX,
                SendY: zone.SendY,
                SendPressure: zone.SendPressure,
                Left: zone.Left,
                Top: zone.Top,
                Width: zone.Width,
                Height: zone.Height))
            .ToList();

        var lastByIndex = new Dictionary<int, (double x, double y)>();
        foreach ((int index, ZoneConfig zone) in _config.Zones.Select((zone, index) => (index, zone)))
        {
            if (_lastZonePoints.TryGetValue(zone, out Point last))
            {
                lastByIndex[index] = (last.X, last.Y);
            }
        }

        var request = new ZoneOverlayRenderRequest(
            PadWidth: Pad.ActualWidth,
            PadHeight: Pad.ActualHeight,
            Zones: zones,
            SelectedZoneIndex: _selectedZoneIndex,
            LastLocalByZoneIndex: lastByIndex,
            ZonePalette: Enumerable.Range(0, zones.Count).Select(GetZonePaletteColor).ToList(),
            ColorWindow: ThemeColor("ColorWindow", Colors.Black),
            TextPrimaryBrush: ThemeBrush("TextPrimaryBrush", Brushes.White),
            TextMutedBrush: ThemeBrush("TextMutedBrush", Brushes.Gainsboro),
            TextSoftBrush: ThemeBrush("TextSoftBrush", Brushes.Gray),
            DangerBrush: ThemeBrush("DangerBrush", Brushes.IndianRed),
            AccentBrushHover: ThemeBrush("AccentBrushHover", Brushes.White),
            SurfaceBrush: ThemeBrush("SurfaceBrush", Brushes.Black));

        _zoneOverlayRenderer.Render(ZoneOverlayCanvas, request);
    }

    private void SendCcIfChanged(int channel, int cc, int value, bool force = false)
        => _midiEngine.SendCcIfChanged(channel, cc, value, force);

    private void SendNoteIfChanged(int channel, int note, bool on, int velocity, bool force = false)
        => _midiEngine.SendNoteIfChanged(channel, note, on, velocity, force);

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
        PopulateTabletDeviceList();

        if (string.Equals(backendMode, "Stylus/Mouse Only", StringComparison.Ordinal))
        {
            ShutdownWintab();
            _wintabDiagnostics = "Manual stylus/mouse mode selected";
            _lastInputBackend = "Stylus/Mouse Fallback";
            UpdateInputBackendStatus();
            return;
        }

        _wintabInputBackend.Initialize(_selectedDeviceIndex);
        _expressKeyCount = Math.Max(_expressKeyCount, _wintabInputBackend.ExpressKeyCount);
        _useWintab = _wintabInputBackend.IsActive;
        _wintabDiagnostics = _useWintab ? "Wintab initialized" : "Wintab32.dll not available";
        _lastInputBackend = _useWintab
            ? "Wintab"
            : string.Equals(backendMode, "Wintab Only", StringComparison.Ordinal)
                ? "Wintab unavailable"
                : "Stylus/Mouse Fallback";
        UpdateInputBackendStatus();
    }

    private void PopulateTabletDeviceList()
    {
        TabletDeviceComboBox.SelectionChanged -= TabletDeviceComboBox_SelectionChanged;
        TabletDeviceComboBox.Items.Clear();

        IReadOnlyList<WintabTabletDevice> devices = _wintabInputBackend.GetAvailableDevices();
        if (devices.Count <= 0)
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

        foreach (WintabTabletDevice device in devices)
        {
            TabletDeviceComboBox.Items.Add(new TabletDeviceItem(device.DeviceIndex, device.Label));

            if (_selectedDeviceIndex >= 0 && device.DeviceIndex == _selectedDeviceIndex)
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
        _wintabInputBackend.Shutdown();
        _useWintab = false;
        _expressKeyCount = 0;
        _lastInputBackend = "Stylus/Mouse Fallback";
        _wintabDiagnostics = "Wintab shut down";
    }

    // WndProc and context plumbing have been extracted to IWintabInputBackend.

    // Message window creation moved to IWintabInputBackend.

    // Context open/close moved to IWintabInputBackend.

    // Context open for device moved to IWintabInputBackend.

    // Reinitialize moved to IWintabInputBackend.

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
        _viewModel.ModifiersText = "Modifiers: none";
        HandleRelease();
        Pad.ReleaseMouseCapture();
        Pad.ReleaseStylusCapture();
    }

    // Express key override setup moved to IWintabInputBackend.

    private void HandleExpressKeyPacket(WintabExpressKeyEventArgs expressKey)
    {
        int control = expressKey.Control;
        bool isPressed = expressKey.IsPressed;
        // Keep editor capacity in sync with runtime-observed key indices.
        _expressKeyCount = Math.Max(_expressKeyCount, control + 1);

        // Engine expects (lower 16 bits = key index, upper 16 bits = pressed flag).
        uint encoded = (uint)(control & 0xFFFF) | (isPressed ? 0x0001_0000u : 0u);
        ApplyInteractionResult(_padInteractionEngine.Process(
            BuildInteractionConfig(),
            BuildZoneSnapshots(),
            _interactionState,
            new InteractionInput(
                XNorm: 0,
                YNorm: 0,
                Pressure: 0,
                IsDown: false,
                IsHovering: false,
                PenButtons: encoded,
                Backend: InteractionBackend.ExpressKey)));

        string action = control >= 0 && control < _config.ExpressKeys.Count
            ? NormalizePenButtonAction(_config.ExpressKeys[control].Action)
            : "Unknown";

        _expressKeyDiagnostics = $"EK{control}: {(isPressed ? "pressed" : "released")} [{action}]";
        MarkInputBackend($"Wintab EK{control}");
        UpdateInputBackendStatus();
    }

    private void HandleWintabPacket(WintabPacketEventArgs packet)
    {
        double width = Pad.ActualWidth;
        double height = Pad.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        double xNorm = packet.XNorm;
        double yNorm = packet.YNorm;
        MarkInputBackend($"Wintab D{packet.DeviceIndex + 1}");
        UpdatePenButtonsDebug(packet.Buttons);
        double xPx = xNorm * width;
        double yPx = yNorm * height;
        int pressure = packet.Pressure127;
        bool hovering = packet.IsHovering;

        _wintabDiagnostics = packet.PacketDiagnostics;
        UpdateInputBackendStatus();

        if (hovering)
        {
            if (_isActive)
            {
                _isActive = false;
                HandleRelease();
            }

            CursorDot.Opacity = 0.35;
            ProcessInteractionTick(
                xPx: xPx,
                yPx: yPx,
                xNorm: xNorm,
                yNorm: yNorm,
                pressure: 0,
                isDown: false,
                isHovering: true,
                penButtons: packet.Buttons,
                backend: InteractionBackend.Wintab);
            return;
        }

        CursorDot.Opacity = 0.85;
        _isActive = true;
        ProcessInteractionTick(
            xPx: xPx,
            yPx: yPx,
            xNorm: xNorm,
            yNorm: yNorm,
            pressure: pressure,
            isDown: true,
            isHovering: false,
            penButtons: packet.Buttons,
            backend: InteractionBackend.Wintab);
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
            ? $"Wintab enabled, {_wintabInputBackend.Contexts.Count} context(s){ekPart}"
            : $"Stylus/mouse fallback | {_wintabDiagnostics}";

        _viewModel.InputBackendText = $"Input: {_lastInputBackend} | {configured}";
    }

    private void RefreshWacomServiceStatus()
    {
        WacomServiceStatus status = _wacomServiceController.GetStatus();
        MenuWacomServiceStatus.Header = status.StatusText;
        MenuStartWacomService.IsEnabled = status.CanStart;
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

        _viewModel.PenButtonsText = $"Pen buttons: mask=0x{buttonMask:X4} pressed={FormatPressedButtons(buttonMask)} transition={transitionText}";
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
