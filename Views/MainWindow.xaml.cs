using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TabletMidiPad.Core.Interaction;
using TabletMidiPad.Services;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Views;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, int> _lastSentValues = new();

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
    private readonly IZoneResizeService _zoneResizeService;
    private readonly IInputFallbackService _inputFallbackService;
    private readonly IInputBackendStatusFormatter _inputBackendStatusFormatter;
    private readonly IWintabEventAdapter _wintabEventAdapter;
    private readonly IInputInteractionOrchestrator _inputInteractionOrchestrator;
    private readonly IMainWindowWorkflowCoordinator _workflowCoordinator;
    private readonly IMainWindowInputBackendCoordinator _inputBackendCoordinator;
    private readonly IWacomServiceWorkflowCoordinator _wacomServiceWorkflowCoordinator;
    private readonly IMainWindowInputEventFlowCoordinator _inputEventFlowCoordinator;
    private readonly IMainWindowPadPresentationCoordinator _padPresentationCoordinator;
    private readonly IMainWindowViewStateMapper _viewStateMapper;
    private readonly IZoneActionWorkflowCoordinator _zoneActionWorkflowCoordinator;
    private readonly IConfigValueParser _configValueParser;
    private readonly IPadInteractionEngine _padInteractionEngine;
    private readonly MainViewModel _viewModel;
    private AppConfig _config = AppConfig.Default;
    private string _lastInputBackend = "Idle";
    private string _wintabDiagnostics = "Wintab not initialized yet";
    private uint _lastWintabButtons;
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
    private bool _isResizingZone;
    private ZoneResizeSession? _zoneResizeSession;

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
            new PadInteractionEngine(),
            new ZoneResizeService(),
            new InputFallbackService(),
            new InputInteractionOrchestrator(new PadInteractionEngine()),
            new InputBackendStatusFormatter(),
            new WintabEventAdapter(),
            new MainWindowWorkflowCoordinator(),
            new MainWindowInputBackendCoordinator(),
            new WacomServiceWorkflowCoordinator(),
            new MainWindowInputEventFlowCoordinator(),
            new MainWindowPadPresentationCoordinator(),
            new MainWindowViewStateMapper(),
            new ZoneActionWorkflowCoordinator(new ZoneConfigurationService()))
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
        IPadInteractionEngine padInteractionEngine,
        IZoneResizeService zoneResizeService,
        IInputFallbackService inputFallbackService,
        IInputInteractionOrchestrator inputInteractionOrchestrator,
        IInputBackendStatusFormatter inputBackendStatusFormatter,
        IWintabEventAdapter wintabEventAdapter,
        IMainWindowWorkflowCoordinator workflowCoordinator,
        IMainWindowInputBackendCoordinator inputBackendCoordinator,
        IWacomServiceWorkflowCoordinator wacomServiceWorkflowCoordinator,
        IMainWindowInputEventFlowCoordinator inputEventFlowCoordinator,
        IMainWindowPadPresentationCoordinator padPresentationCoordinator,
        IMainWindowViewStateMapper viewStateMapper,
        IZoneActionWorkflowCoordinator zoneActionWorkflowCoordinator)
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
        _zoneResizeService = zoneResizeService ?? throw new ArgumentNullException(nameof(zoneResizeService));
        _inputFallbackService = inputFallbackService ?? throw new ArgumentNullException(nameof(inputFallbackService));
        _inputBackendStatusFormatter = inputBackendStatusFormatter ?? throw new ArgumentNullException(nameof(inputBackendStatusFormatter));
        _wintabEventAdapter = wintabEventAdapter ?? throw new ArgumentNullException(nameof(wintabEventAdapter));
        _inputInteractionOrchestrator = inputInteractionOrchestrator ?? throw new ArgumentNullException(nameof(inputInteractionOrchestrator));
        _workflowCoordinator = workflowCoordinator ?? throw new ArgumentNullException(nameof(workflowCoordinator));
        _inputBackendCoordinator = inputBackendCoordinator ?? throw new ArgumentNullException(nameof(inputBackendCoordinator));
        _wacomServiceWorkflowCoordinator = wacomServiceWorkflowCoordinator ?? throw new ArgumentNullException(nameof(wacomServiceWorkflowCoordinator));
        _inputEventFlowCoordinator = inputEventFlowCoordinator ?? throw new ArgumentNullException(nameof(inputEventFlowCoordinator));
        _padPresentationCoordinator = padPresentationCoordinator ?? throw new ArgumentNullException(nameof(padPresentationCoordinator));
        _viewStateMapper = viewStateMapper ?? throw new ArgumentNullException(nameof(viewStateMapper));
        _zoneActionWorkflowCoordinator = zoneActionWorkflowCoordinator ?? throw new ArgumentNullException(nameof(zoneActionWorkflowCoordinator));
        _wintabInputBackend.PacketReceived += (_, e) => HandleWintabPacket(e);
        _wintabInputBackend.ExpressKeyReceived += (_, e) => HandleExpressKeyPacket(e);
        _wintabInputBackend.ProximityChanged += (_, e) =>
        {
            _wintabDiagnostics = e.Diagnostics;
            UpdateInputBackendStatus();
            if (!e.Entering)
            {
                if (_inputEventFlowCoordinator.ShouldReleaseOnProximityLeave(_isActive))
                {
                    _isActive = false;
                    HandleRelease();
                }

                ApplyCursorChrome(_padPresentationCoordinator.ComputeCursorAfterRelease());
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
        int effectiveExpressKeyCount = _workflowCoordinator.GetEffectiveExpressKeyCount(_expressKeyCount, _config.ExpressKeys.Count);
        ExpressKeyConfig.EnsureCount(_config.ExpressKeys, effectiveExpressKeyCount);
        PreferencesDialogResult? result = _preferencesDialogService.ShowDialog(this, _config, effectiveExpressKeyCount);
        if (result is null)
        {
            return;
        }

        CommitCurrentZoneEdits();
        PreferencesWorkflowResult applyResult = _workflowCoordinator.ApplyPreferences(_config, result, IntPtr.Zero);
        if (!applyResult.SaveSucceeded)
        {
            SettingsResultText.Foreground = Brushes.IndianRed;
            SettingsResultText.Text = applyResult.ErrorMessage ?? "Preferences could not be applied.";
            return;
        }

        try
        {
            _configStore.Save(_config);
        }
        catch (Exception ex)
        {
            SettingsResultText.Foreground = Brushes.IndianRed;
            SettingsResultText.Text = ex.Message;
            return;
        }

        if (applyResult.ShouldRestartMidi)
        {
            RestartMidi();
        }
        else
        {
            _lastSentValues.Clear();
            UpdateUiForCurrentConfig();
            DrawZoneOverlay();
        }

        if (applyResult.ShouldRebindInputBackend)
        {
            RebindInputBackend();
        }

        SyncToolbar();
        SettingsResultText.Foreground = Brushes.DarkSeaGreen;
        SettingsResultText.Text = "Preferences applied and saved.";
    }

    private async Task StartWacomServiceAsync()
    {
        WacomServiceWorkflowState startingState = _wacomServiceWorkflowCoordinator.BuildStartingState();
        MenuStartWacomService.IsEnabled = startingState.StartEnabled;
        MenuWacomServiceStatus.Header = startingState.StatusHeader;

        WacomServiceStartResult result = await Task.Run(() => _wacomServiceWorkflowCoordinator.Start(_wacomServiceController));
        if (result.Started)
        {
            RefreshWacomServiceStatus(result.RefreshedStatus);
            if (result.ShouldRebindInputBackend)
            {
                RebindInputBackend();
            }
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.FailureStatusHeader))
        {
            MenuWacomServiceStatus.Header = result.FailureStatusHeader;
        }

        RefreshWacomServiceStatus(result.RefreshedStatus);
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
        ZoneActionWorkflowResult result = _zoneActionWorkflowCoordinator.AutoLayout(_config, _selectedZoneIndex);
        RefreshZoneList();
        ZoneListBox.SelectedIndex = result.SelectedZoneIndex;
        _selectedZoneIndex = result.SelectedZoneIndex;
        LoadSelectedZoneIntoEditor();
        DrawZoneOverlay();
        SettingsResultText.Foreground = Brushes.DarkSeaGreen;
        SettingsResultText.Text = result.StatusMessage;
    }

    private void AddZone()
    {
        CommitCurrentZoneEdits();
        ZoneActionWorkflowResult result = _zoneActionWorkflowCoordinator.AddZone(_config);
        RefreshZoneList();
        ZoneListBox.SelectedIndex = result.SelectedZoneIndex;
        _selectedZoneIndex = result.SelectedZoneIndex;
        LoadSelectedZoneIntoEditor();
        DrawZoneOverlay();
        SettingsResultText.Foreground = Brushes.DarkSeaGreen;
        SettingsResultText.Text = result.StatusMessage;
    }

    private void RemoveZone()
    {
        ZoneActionWorkflowResult result = _zoneActionWorkflowCoordinator.RemoveZone(_config, _selectedZoneIndex);
        if (!result.Changed)
        {
            return;
        }

        RefreshZoneList();
        ZoneListBox.SelectedIndex = result.SelectedZoneIndex;
        _selectedZoneIndex = result.SelectedZoneIndex;
        LoadSelectedZoneIntoEditor();
        DrawZoneOverlay();
        SettingsResultText.Foreground = Brushes.DarkSeaGreen;
        SettingsResultText.Text = result.StatusMessage;
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
        string next = InputBackendModeNormalizer.Normalize(_config.InputBackendMode) switch
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
        string normalized = InputBackendModeNormalizer.Normalize(mode);
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
            WorkflowSaveResult applyResult = _workflowCoordinator.ValidateAndSave(_config);
            if (!applyResult.Success)
            {
                SettingsResultText.Foreground = Brushes.IndianRed;
                SettingsResultText.Text = applyResult.ErrorMessage ?? "Settings could not be applied.";
                return;
            }

            _configStore.Save(_config);
            _lastSentValues.Clear();
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
        MainWindowToolbarState toolbarState = _viewStateMapper.BuildToolbarState(_config);
        _viewModel.ToolbarPressureLabelText = toolbarState.ToolbarPressureLabelText;
        _viewModel.SelectedPressureCurve = toolbarState.SelectedPressureCurve;
        _viewModel.ToolbarInputBackendLabelText = toolbarState.ToolbarInputBackendLabelText;
        _viewModel.SelectedInputBackendMode = toolbarState.SelectedInputBackendMode;
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

    private async Task InitializeMidiAsync()
    {
        try
        {
            await _midiEngine.InitializeAsync(_config, text => Dispatcher.Invoke(() => _viewModel.MidiStatusText = text));
            _midiEngine.ClearCachedValues();
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
        MainWindowConnectedState statusState = _viewStateMapper.BuildConnectedState(_config);
        _viewModel.StatusText = statusState.StatusText;
        _viewModel.RoutingText = statusState.RoutingText;
        UpdateInputBackendStatus();
        _viewModel.ActiveZoneText = statusState.ActiveZoneText;
        _viewModel.ConfigSummaryText = statusState.ConfigSummaryText;
        _viewModel.ValueText = statusState.ValueText;
        _viewModel.PenButtonsText = statusState.PenButtonsText;
        _viewModel.ModifiersText = statusState.ModifiersText;
    }

    private void ShutdownMidi() => _midiEngine.Shutdown(_config);

    private void Pad_StylusDown(object sender, StylusDownEventArgs e)
    {
        if (_inputEventFlowCoordinator.IsStylusMouseFallbackSuppressed(_useWintab, _config.InputBackendMode)) return;
        e.Handled = true;
        _isActive = true;
        Pad.CaptureStylus();
        HandleStylus(e);
    }

    private void Pad_StylusMove(object sender, StylusEventArgs e)
    {
        if (_inputEventFlowCoordinator.IsStylusMouseFallbackSuppressed(_useWintab, _config.InputBackendMode)) return;
        if (!_isActive) return;
        e.Handled = true;
        HandleStylus(e);
    }

    private void Pad_StylusUp(object sender, StylusEventArgs e)
    {
        if (_inputEventFlowCoordinator.IsStylusMouseFallbackSuppressed(_useWintab, _config.InputBackendMode)) return;
        if (!_isActive) return;
        e.Handled = true;
        HandleStylus(e);
        _isActive = false;
        Pad.ReleaseStylusCapture();
        ApplyCursorChrome(_padPresentationCoordinator.ComputeCursorAfterRelease());
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

        if (_inputEventFlowCoordinator.IsStylusMouseFallbackSuppressed(_useWintab, _config.InputBackendMode)) return;
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

        if (_inputEventFlowCoordinator.IsStylusMouseFallbackSuppressed(_useWintab, _config.InputBackendMode)) return;
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

        if (_inputEventFlowCoordinator.IsStylusMouseFallbackSuppressed(_useWintab, _config.InputBackendMode)) return;
        if (!_isActive) return;
        HandleMouse(e);
        _isActive = false;
        Pad.ReleaseMouseCapture();
        ApplyCursorChrome(_padPresentationCoordinator.ComputeCursorAfterRelease());
        HandleRelease();
    }

    private (int zone, ZoneResizeEdge edge) HitTestZoneEdge(Point pos)
        => _zoneResizeService.HitTestZoneEdge(_config.Zones, _selectedZoneIndex, Pad.ActualWidth, Pad.ActualHeight, pos);

    private static Cursor? ResizeEdgeToCursor(ZoneResizeEdge edge)
    {
        return edge switch
        {
            ZoneResizeEdge.Left or ZoneResizeEdge.Right => Cursors.SizeWE,
            ZoneResizeEdge.Top or ZoneResizeEdge.Bottom => Cursors.SizeNS,
            ZoneResizeEdge.Left | ZoneResizeEdge.Top => Cursors.SizeNWSE,
            ZoneResizeEdge.Right | ZoneResizeEdge.Bottom => Cursors.SizeNWSE,
            ZoneResizeEdge.Left | ZoneResizeEdge.Bottom => Cursors.SizeNESW,
            ZoneResizeEdge.Right | ZoneResizeEdge.Top => Cursors.SizeNESW,
            _ => null
        };
    }

    private void StartZoneResize(int zoneIndex, ZoneResizeEdge edge, Point pos)
    {
        _zoneResizeSession = _zoneResizeService.StartResizeSession(_config.Zones, zoneIndex, edge, pos);
        _isResizingZone = _zoneResizeSession is not null;
        if (!_isResizingZone)
        {
            return;
        }

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
        int zoneIndex = _zoneResizeService.HitTestZoneBody(_config.Zones, Pad.ActualWidth, Pad.ActualHeight, pos);
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

    private void UpdateZoneResize(Point pos)
    {
        if (_zoneResizeSession is null)
        {
            return;
        }

        ZoneResizeUpdate? update = _zoneResizeService.ComputeResizeUpdate(
            _config.Zones,
            _zoneResizeSession,
            Pad.ActualWidth,
            Pad.ActualHeight,
            pos);
        if (update is null)
        {
            return;
        }

        ZoneConfig zone = _config.Zones[update.ZoneIndex];
        zone.Left = update.Left;
        zone.Top = update.Top;
        zone.Width = update.Width;
        zone.Height = update.Height;

        LoadSelectedZoneIntoEditor();
        DrawZoneOverlay();
    }

    private void FinishZoneResize()
    {
        _isResizingZone = false;
        _zoneResizeSession = null;
        Pad.Cursor = null;
        SettingsResultText.Foreground = Brushes.DarkSeaGreen;
        SettingsResultText.Text = "Zone resized. Click Apply Settings to save.";
    }

    private void HandleStylus(StylusEventArgs e)
    {
        MarkInputBackend("Stylus");
        _viewModel.PenButtonsText = "Pen buttons: stylus fallback";
        if (!_inputFallbackService.TryBuildStylusSample(
            e,
            Pad,
            Pad.ActualWidth,
            Pad.ActualHeight,
            _config.MousePressureValue,
            out InputFallbackSample sample))
        {
            return;
        }

        ProcessInteractionTick(
            new InteractionInput(
                XNorm: sample.XNorm,
                YNorm: sample.YNorm,
                Pressure: sample.Pressure127,
                IsDown: true,
                IsHovering: false,
                PenButtons: 0,
                Backend: InteractionBackend.StylusFallback),
            sample.PixelPosition);
    }

    private void HandleMouse(MouseEventArgs e)
    {
        MarkInputBackend("Mouse");
        _viewModel.PenButtonsText = "Pen buttons: mouse fallback";
        if (!_inputFallbackService.TryBuildMouseSample(
            e,
            Pad,
            Pad.ActualWidth,
            Pad.ActualHeight,
            _config.MousePressureValue,
            out InputFallbackSample sample))
        {
            return;
        }

        ProcessInteractionTick(
            new InteractionInput(
                XNorm: sample.XNorm,
                YNorm: sample.YNorm,
                Pressure: sample.Pressure127,
                IsDown: true,
                IsHovering: false,
                PenButtons: 0,
                Backend: InteractionBackend.MouseFallback),
            sample.PixelPosition);
    }

    private void ProcessInteractionTick(InteractionInput input, Point pixelPosition, double? cursorOpacityOverride = null)
    {
        ApplyCursorChrome(_padPresentationCoordinator.ComputeCursorForInteractionTick(
            input,
            pixelPosition,
            CursorDot.Width,
            CursorDot.Height,
            cursorOpacityOverride));

        InteractionResult result = _inputInteractionOrchestrator.ProcessTick(
            BuildInteractionConfig(),
            BuildZoneSnapshots(),
            _interactionState,
            input);

        ApplyInteractionResult(result);

        DrawZoneOverlay();
    }

    private void HandleRelease()
    {
        // Release tick goes through engine so release behavior stays centralized.
        InteractionResult releaseResult = _inputInteractionOrchestrator.ProcessRelease(
            BuildInteractionConfig(),
            BuildZoneSnapshots(),
            _interactionState,
            _lastWintabButtons);
        ApplyInteractionResult(releaseResult);
        DrawZoneOverlay();

        ApplyCursorChrome(_padPresentationCoordinator.ComputeCursorAfterRelease());
    }

    private void DrawZoneOverlay()
    {
        var theme = new MainWindowPadOverlayTheme(
            ZonePalette: Enumerable.Range(0, _config.Zones.Count).Select(GetZonePaletteColor).ToList(),
            ColorWindow: ThemeColor("ColorWindow", Colors.Black),
            TextPrimaryBrush: ThemeBrush("TextPrimaryBrush", Brushes.White),
            TextMutedBrush: ThemeBrush("TextMutedBrush", Brushes.Gainsboro),
            TextSoftBrush: ThemeBrush("TextSoftBrush", Brushes.Gray),
            DangerBrush: ThemeBrush("DangerBrush", Brushes.IndianRed),
            AccentBrushHover: ThemeBrush("AccentBrushHover", Brushes.White),
            SurfaceBrush: ThemeBrush("SurfaceBrush", Brushes.Black));

        ZoneOverlayRenderRequest request = _padPresentationCoordinator.BuildZoneOverlayRequest(
            _config.Zones,
            _lastZonePoints,
            _selectedZoneIndex,
            Pad.ActualWidth,
            Pad.ActualHeight,
            theme);

        _zoneOverlayRenderer.Render(ZoneOverlayCanvas, request);
    }

    private void ApplyCursorChrome(CursorTickChrome chrome)
    {
        if (!chrome.ShouldApply)
        {
            return;
        }

        CursorDot.Visibility = chrome.Visibility;
        Canvas.SetLeft(CursorDot, chrome.Left);
        Canvas.SetTop(CursorDot, chrome.Top);
        if (chrome.Opacity.HasValue)
        {
            CursorDot.Opacity = chrome.Opacity.Value;
        }
    }

    private void SendCcIfChanged(int channel, int cc, int value, bool force = false)
        => _midiEngine.SendCcIfChanged(channel, cc, value, force);

    private void SendNoteIfChanged(int channel, int note, bool on, int velocity, bool force = false)
        => _midiEngine.SendNoteIfChanged(channel, note, on, velocity, force);

    private void InitializeWintab()
    {
        PopulateTabletDeviceList();
        InputBackendLifecycleResult lifecycle = _inputBackendCoordinator.Initialize(
            _config.InputBackendMode,
            _wintabInputBackend,
            _selectedDeviceIndex,
            _expressKeyCount);
        _useWintab = lifecycle.UseWintab;
        _expressKeyCount = lifecycle.ExpressKeyCount;
        _lastInputBackend = lifecycle.LastInputBackend;
        _wintabDiagnostics = lifecycle.WintabDiagnostics;
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
        InputBackendLifecycleResult lifecycle = _inputBackendCoordinator.Shutdown(_wintabInputBackend);
        _useWintab = lifecycle.UseWintab;
        _expressKeyCount = lifecycle.ExpressKeyCount;
        _lastInputBackend = lifecycle.LastInputBackend;
        _wintabDiagnostics = lifecycle.WintabDiagnostics;
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
        InputBackendResetResult resetState = _inputBackendCoordinator.BuildResetState();
        _isActive = resetState.IsActive;
        CursorDot.Visibility = Visibility.Collapsed;
        _viewModel.ActiveZoneText = resetState.ActiveZoneText;
        _viewModel.ValueText = resetState.ValueText;
        _viewModel.PenButtonsText = resetState.PenButtonsText;
        _viewModel.ModifiersText = resetState.ModifiersText;
        HandleRelease();
        Pad.ReleaseMouseCapture();
        Pad.ReleaseStylusCapture();
    }

    // Express key override setup moved to IWintabInputBackend.

    private void HandleExpressKeyPacket(WintabExpressKeyEventArgs expressKey)
    {
        WintabExpressKeyUpdate update = _wintabEventAdapter.BuildExpressKeyUpdate(expressKey, _config.ExpressKeys);
        _expressKeyCount = Math.Max(_expressKeyCount, update.RequiredExpressKeyCount);
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
                PenButtons: update.EncodedPenButtons,
                Backend: InteractionBackend.ExpressKey)));
        _expressKeyDiagnostics = update.ExpressKeyDiagnostics;
        MarkInputBackend(update.BackendLabel);
        UpdateInputBackendStatus();
    }

    private void HandleWintabPacket(WintabPacketEventArgs packet)
    {
        if (!_wintabEventAdapter.TryBuildPacketUpdate(packet, Pad.ActualWidth, Pad.ActualHeight, out WintabPacketUpdate update))
        {
            return;
        }

        MarkInputBackend(update.BackendLabel);
        UpdatePenButtonsDebug(update.PenButtons);

        _wintabDiagnostics = update.PacketDiagnostics;
        UpdateInputBackendStatus();

        WintabPacketFlowResult flow = _inputEventFlowCoordinator.BuildWintabPacketFlow(_isActive, update);
        if (flow.ReleaseBeforeTickIfWasActive)
        {
            _isActive = false;
            HandleRelease();
        }

        _isActive = flow.IsActiveAfter;
        ProcessInteractionTick(flow.TickInput, flow.PixelPosition, flow.CursorOpacity);
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
        _viewModel.InputBackendText = _inputBackendStatusFormatter.BuildStatusText(
            _lastInputBackend,
            _useWintab,
            _wintabInputBackend.Contexts.Count,
            _wintabDiagnostics,
            _expressKeyDiagnostics);
    }

    private void RefreshWacomServiceStatus()
    {
        RefreshWacomServiceStatus(_wacomServiceController.GetStatus());
    }

    private void RefreshWacomServiceStatus(WacomServiceStatus status)
    {
        WacomServiceMenuState menuState = _workflowCoordinator.BuildWacomServiceMenuState(status);
        MenuWacomServiceStatus.Header = menuState.Header;
        MenuStartWacomService.IsEnabled = menuState.StartEnabled;
    }

    private void UpdatePenButtonsDebug(uint buttons)
    {
        _lastWintabButtons = buttons;
        _viewModel.PenButtonsText = _inputBackendStatusFormatter.BuildPenButtonsText(buttons);
    }

}
