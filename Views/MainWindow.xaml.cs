using System;
using System.ComponentModel;
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
    /// <summary>Shared MIDI engine for the parameterless designer ctor so <see cref="IPadInteractionOutcomeApplier"/> uses the same instance.</summary>
    private static readonly IMidiEngine DesignerMidiEngine = new WinMmMidiEngine();

    /// <summary>Shared Wintab backend for the designer ctor so <see cref="ITabletDevicePickerSyncService"/> and <see cref="IWintabInputBackend"/> refer to the same instance.</summary>
    private static readonly IWintabInputBackend DesignerWintabBackend = new WintabInputBackend();

    /// <summary>Shared zone configuration for designer-time <see cref="IZoneActionWorkflowCoordinator"/> parity.</summary>
    private static readonly IZoneConfigurationService DesignerZoneConfiguration = new ZoneConfigurationService();

    private static readonly MainWindowInputBackendCoordinator DesignerInputBackendCoordinator = new();

    private static readonly MainWindowWintabInputBridge DesignerWintabInputBridge = new(new WintabEventAdapter(), DesignerInputBackendCoordinator);

    private bool _isActive;
    private bool _isLoadingUi;
    private bool _useWintab;
    private int _expressKeyCount;
    private string _expressKeyDiagnostics = "";

    private readonly IConfigStore _configStore;
    private readonly IMidiEngine _midiEngine;
    private readonly IWacomServiceController _wacomServiceController;
    private readonly IWintabInputBackend _wintabInputBackend;
    private readonly ITabletDevicePickerSyncService _tabletDevicePickerSyncService;
    private readonly IPreferencesDialogService _preferencesDialogService;
    private readonly IZoneConfigurationService _zoneConfigurationService;
    private readonly IZoneEditorService _zoneEditorService;
    private readonly IZoneResizeService _zoneResizeService;
    private readonly IInputFallbackService _inputFallbackService;
    private readonly IInputBackendStatusFormatter _inputBackendStatusFormatter;
    private readonly IInputInteractionOrchestrator _inputInteractionOrchestrator;
    private readonly IMainWindowWorkflowCoordinator _workflowCoordinator;
    private readonly IMainWindowInputBackendCoordinator _inputBackendCoordinator;
    private readonly IWacomServiceWorkflowCoordinator _wacomServiceWorkflowCoordinator;
    private readonly IMainWindowLifecycleCoordinator _lifecycleCoordinator;
    private readonly IMainWindowWintabInputBridge _wintabInputBridge;
    private readonly IMainWindowToolbarSession _toolbarSession;
    private readonly IZoneActionWorkflowCoordinator _zoneActionWorkflowCoordinator;
    private readonly IPadInteractionOutcomeApplier _interactionOutcomeApplier;
    private readonly MainViewModel _viewModel;
    private AppConfig _config = AppConfig.Default;
    private string _lastInputBackend = "Idle";
    private string _wintabDiagnostics = "Wintab not initialized yet";
    private uint _lastWintabButtons;
    private readonly InteractionState _interactionState = new();

    private Canvas Pad => PadSurface.PadInputElement;

    private int SelectedZoneIndex
    {
        get => _viewModel.SelectedZoneIndex;
        set => _viewModel.SelectedZoneIndex = value;
    }

    private void ApplyInteractionResult(InteractionResult result)
        => _interactionOutcomeApplier.Apply(result, _interactionState, _config, _viewModel);

    // Zone resize drag state
    private bool _isResizingZone;
    private ZoneResizeSession? _zoneResizeSession;

    public MainWindow()
        : this(
            new JsonConfigStore(AppConfig.GetConfigPath()),
            DesignerMidiEngine,
            new WacomServiceController(),
            DesignerWintabBackend,
            new TabletDevicePickerSyncService(DesignerWintabBackend),
            new PreferencesDialogService(new ConfigValueParser(), () => new PreferencesViewModel()),
            DesignerZoneConfiguration,
            new ZoneEditorService(new ConfigValueParser()),
            new ZoneOverlayRenderer(),
            new ZoneResizeService(),
            new InputFallbackService(),
            new InputInteractionOrchestrator(new PadInteractionEngine()),
            new InputBackendStatusFormatter(),
            new MainWindowWorkflowCoordinator(),
            DesignerInputBackendCoordinator,
            new WacomServiceWorkflowCoordinator(),
            new MainWindowLifecycleCoordinator(),
            DesignerWintabInputBridge,
            new MainWindowPadPresentationCoordinator(),
            new MainWindowToolbarSession(),
            new ZoneActionWorkflowCoordinator(DesignerZoneConfiguration),
            new MainViewModel(),
            new PadInteractionOutcomeApplier(DesignerMidiEngine))
    {
    }

    internal MainWindow(
        IConfigStore configStore,
        IMidiEngine midiEngine,
        IWacomServiceController wacomServiceController,
        IWintabInputBackend wintabInputBackend,
        ITabletDevicePickerSyncService tabletDevicePickerSyncService,
        IPreferencesDialogService preferencesDialogService,
        IZoneConfigurationService zoneConfigurationService,
        IZoneEditorService zoneEditorService,
        IZoneOverlayRenderer zoneOverlayRenderer,
        IZoneResizeService zoneResizeService,
        IInputFallbackService inputFallbackService,
        IInputInteractionOrchestrator inputInteractionOrchestrator,
        IInputBackendStatusFormatter inputBackendStatusFormatter,
        IMainWindowWorkflowCoordinator workflowCoordinator,
        IMainWindowInputBackendCoordinator inputBackendCoordinator,
        IWacomServiceWorkflowCoordinator wacomServiceWorkflowCoordinator,
        IMainWindowLifecycleCoordinator lifecycleCoordinator,
        IMainWindowWintabInputBridge wintabInputBridge,
        IMainWindowPadPresentationCoordinator padPresentationCoordinator,
        IMainWindowToolbarSession toolbarSession,
        IZoneActionWorkflowCoordinator zoneActionWorkflowCoordinator,
        MainViewModel viewModel,
        IPadInteractionOutcomeApplier interactionOutcomeApplier)
    {
        _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        _midiEngine = midiEngine ?? throw new ArgumentNullException(nameof(midiEngine));
        _wacomServiceController = wacomServiceController ?? throw new ArgumentNullException(nameof(wacomServiceController));
        _wintabInputBackend = wintabInputBackend ?? throw new ArgumentNullException(nameof(wintabInputBackend));
        _tabletDevicePickerSyncService = tabletDevicePickerSyncService ?? throw new ArgumentNullException(nameof(tabletDevicePickerSyncService));
        _preferencesDialogService = preferencesDialogService ?? throw new ArgumentNullException(nameof(preferencesDialogService));
        _zoneConfigurationService = zoneConfigurationService ?? throw new ArgumentNullException(nameof(zoneConfigurationService));
        _zoneEditorService = zoneEditorService ?? throw new ArgumentNullException(nameof(zoneEditorService));
        ArgumentNullException.ThrowIfNull(zoneOverlayRenderer);
        _zoneResizeService = zoneResizeService ?? throw new ArgumentNullException(nameof(zoneResizeService));
        _inputFallbackService = inputFallbackService ?? throw new ArgumentNullException(nameof(inputFallbackService));
        _inputBackendStatusFormatter = inputBackendStatusFormatter ?? throw new ArgumentNullException(nameof(inputBackendStatusFormatter));
        _inputInteractionOrchestrator = inputInteractionOrchestrator ?? throw new ArgumentNullException(nameof(inputInteractionOrchestrator));
        _workflowCoordinator = workflowCoordinator ?? throw new ArgumentNullException(nameof(workflowCoordinator));
        _inputBackendCoordinator = inputBackendCoordinator ?? throw new ArgumentNullException(nameof(inputBackendCoordinator));
        _wacomServiceWorkflowCoordinator = wacomServiceWorkflowCoordinator ?? throw new ArgumentNullException(nameof(wacomServiceWorkflowCoordinator));
        _lifecycleCoordinator = lifecycleCoordinator ?? throw new ArgumentNullException(nameof(lifecycleCoordinator));
        _wintabInputBridge = wintabInputBridge ?? throw new ArgumentNullException(nameof(wintabInputBridge));
        ArgumentNullException.ThrowIfNull(padPresentationCoordinator);
        _toolbarSession = toolbarSession ?? throw new ArgumentNullException(nameof(toolbarSession));
        _zoneActionWorkflowCoordinator = zoneActionWorkflowCoordinator ?? throw new ArgumentNullException(nameof(zoneActionWorkflowCoordinator));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _interactionOutcomeApplier = interactionOutcomeApplier ?? throw new ArgumentNullException(nameof(interactionOutcomeApplier));
        InitializeComponent();
        PadSurface.AttachPresentationServices(padPresentationCoordinator, zoneOverlayRenderer);
        PadSurface.PadLayoutChanged += (_, _) => RefreshPadZoneOverlay();
        PadSurface.PadStylusDown += Pad_StylusDown;
        PadSurface.PadStylusMove += Pad_StylusMove;
        PadSurface.PadStylusUp += Pad_StylusUp;
        PadSurface.PadMouseDown += Pad_MouseDown;
        PadSurface.PadMouseMove += Pad_MouseMove;
        PadSurface.PadMouseUp += Pad_MouseUp;
        DataContext = _viewModel;
        InitializeCommands();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _wintabInputBackend.PacketReceived += (_, e) => HandleWintabPacket(e);
        _wintabInputBackend.ExpressKeyReceived += (_, e) => HandleExpressKeyPacket(e);
        _wintabInputBackend.ProximityChanged += (_, e) =>
        {
            _wintabDiagnostics = e.Diagnostics;
            UpdateInputBackendStatus();
            if (!e.Entering)
            {
                if (_inputBackendCoordinator.ShouldReleaseOnProximityLeave(_isActive))
                {
                    _isActive = false;
                    HandleRelease();
                }

                PadSurface.ApplyCursorChrome(PadSurface.ComputeCursorAfterRelease());
                _viewModel.ActiveZoneText = "Active zone: none";
            }
        };
        _wintabInputBackend.DiagnosticsChanged += (_, diagnostics) =>
        {
            _wintabDiagnostics = diagnostics;
            UpdateInputBackendStatus();
        };
        WindowChromeTheme.ApplyDarkNonClientChrome(this);
        ConfigureNumericInputs();
        ContentRendered += OnContentRendered;
        Closed += (_, _) =>
        {
            _lifecycleCoordinator.ShutdownMidiOutput(_midiEngine, _config);
            ApplyInputBackendLifecycleState(
                _lifecycleCoordinator.ShutdownTabletInput(_inputBackendCoordinator, _wintabInputBackend),
                refreshInputBackendStatusText: false);
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
        _viewModel.ConfigureToolbarCommands(
            selectPressureCurveLinear: () => SetPressureCurve("Linear"),
            selectPressureCurveLogarithmic: () => SetPressureCurve("Logarithmic"),
            selectPressureCurveExponential: () => SetPressureCurve("Exponential"),
            selectInputBackendAuto: () => SetInputBackendMode("Auto"),
            selectInputBackendWintab: () => SetInputBackendMode("Wintab Only"),
            selectInputBackendMouse: () => SetInputBackendMode("Mouse"),
            cycleInputBackend: CycleInputBackend);
    }

    private void CycleInputBackend()
    {
        SetInputBackendMode(_toolbarSession.GetNextCycledInputBackendModeLabel(_config));
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
            SetSettingsResult(applyResult.ErrorMessage ?? "Preferences could not be applied.", SettingsResultKind.Error);
            return;
        }

        WorkflowSaveResult persistResult = _workflowCoordinator.PersistConfigToDisk(_config, _configStore);
        if (!persistResult.Success)
        {
            SetSettingsResult(persistResult.ErrorMessage ?? "Preferences could not be saved.", SettingsResultKind.Error);
            return;
        }

        if (applyResult.ShouldRestartMidi)
        {
            RestartMidi();
        }
        else
        {
            UpdateUiForCurrentConfig();
            RefreshPadZoneOverlay();
        }

        if (applyResult.ShouldRebindInputBackend)
        {
            RebindInputBackend();
        }

        SyncToolbar();
        SetSettingsResult("Preferences applied and saved.", SettingsResultKind.Success);
    }

    private async Task StartWacomServiceAsync()
    {
        WacomServiceWorkflowState startingState = _wacomServiceWorkflowCoordinator.BuildStartingState();
        _viewModel.IsWacomServiceStartEnabled = startingState.StartEnabled;
        _viewModel.WacomServiceStatusHeader = startingState.StatusHeader;

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
            _viewModel.WacomServiceStatusHeader = result.FailureStatusHeader;
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
        ZoneActionWorkflowResult result = _zoneActionWorkflowCoordinator.AutoLayout(_config, SelectedZoneIndex);
        RefreshZoneList();
        SelectedZoneIndex = result.SelectedZoneIndex;
        _viewModel.NotifySelectedZoneIndexChanged();
        LoadSelectedZoneIntoEditor();
        RefreshPadZoneOverlay();
        SetSettingsResult(result.StatusMessage, SettingsResultKind.Success);
    }

    private void AddZone()
    {
        CommitCurrentZoneEdits();
        ZoneActionWorkflowResult result = _zoneActionWorkflowCoordinator.AddZone(_config);
        RefreshZoneList();
        SelectedZoneIndex = result.SelectedZoneIndex;
        _viewModel.NotifySelectedZoneIndexChanged();
        LoadSelectedZoneIntoEditor();
        RefreshPadZoneOverlay();
        SetSettingsResult(result.StatusMessage, SettingsResultKind.Success);
    }

    private void RemoveZone()
    {
        ZoneActionWorkflowResult result = _zoneActionWorkflowCoordinator.RemoveZone(_config, SelectedZoneIndex);
        if (!result.Changed)
        {
            return;
        }

        RefreshZoneList();
        SelectedZoneIndex = result.SelectedZoneIndex;
        _viewModel.NotifySelectedZoneIndexChanged();
        LoadSelectedZoneIntoEditor();
        RefreshPadZoneOverlay();
        SetSettingsResult(result.StatusMessage, SettingsResultKind.Success);
    }

    private async Task InitializeAppAsync()
    {
        _config = _lifecycleCoordinator.LoadStartupConfig(_configStore);
        LoadConfigIntoUi(_config);
        RefreshWacomServiceStatus();

        InitializeWintab();

        _viewModel.IsMidiStatusPanelVisible = true;
        _viewModel.IsMidiProgressVisible = true;
        _viewModel.MidiStatusSeverity = MidiStatusSeverity.Normal;
        _viewModel.MidiStatusText = "Connecting MIDI...";
        await Task.Yield();

        MidiInitializationResult midiResult = await _lifecycleCoordinator.TryInitializeMidiAsync(
            _midiEngine,
            _config,
            text => Dispatcher.Invoke(() => _viewModel.MidiStatusText = text));

        if (midiResult.Succeeded)
        {
            UpdateUiForCurrentConfig();
            _viewModel.IsMidiStatusPanelVisible = false;
        }
        else
        {
            _viewModel.IsMidiProgressVisible = false;
            _viewModel.MidiStatusSeverity = MidiStatusSeverity.Error;
            _viewModel.MidiStatusText = "MIDI init failed";
            _viewModel.StatusText = "MIDI init failed";
            _viewModel.ValueText = midiResult.ErrorMessage ?? string.Empty;
        }
    }

    private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ApplySettings();
    }

    private void ReconnectPenCore()
    {
        CommitCurrentZoneEdits();
        RebindInputBackend();
        SetSettingsResult("Input backend reconnected.", SettingsResultKind.Success);
    }

    private Brush ThemeBrush(string key, Brush fallback)
        => TryFindResource(key) as Brush
           ?? Application.Current?.TryFindResource(key) as Brush
           ?? fallback;

    private void SyncPressureSplitBorder()
    {
        bool on = _viewModel.SendPressureGlobally;
        PressureSplitBorder.Background = on ? ThemeBrush("AccentSurfaceBrush", Brushes.Transparent) : Brushes.Transparent;
        PressureSplitBorder.BorderBrush = on ? ThemeBrush("AccentBrush", Brushes.Transparent) : Brushes.Transparent;
    }

    private void SyncInputBackendSplitBorder()
    {
        InputBackendSplitBorder.Background = ThemeBrush("AccentSurfaceBrush", Brushes.Transparent);
        InputBackendSplitBorder.BorderBrush = ThemeBrush("AccentBrush", Brushes.Transparent);
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

    private void ToolbarInputBackendDropdown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.ContextMenu is not null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        }
    }

    private void SetPressureCurve(string curve)
    {
        _toolbarSession.ApplyPressureCurve(_config, curve);
        SyncToolbar();
    }

    private void SetInputBackendMode(string mode)
    {
        ToolbarInputModeApplyResult result = _toolbarSession.TryApplyInputBackendMode(_config, mode);
        if (!result.ModeChanged)
        {
            SyncToolbar();
            return;
        }

        if (result.RequiresInputRebind)
        {
            RebindInputBackend();
        }

        SyncToolbar();
    }

    private void ZoneListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingUi)
        {
            return;
        }

        LoadSelectedZoneIntoEditor();
        RefreshPadZoneOverlay();
    }

    private void ApplySettings()
    {
        try
        {
            CommitCurrentZoneEdits();
            WorkflowSaveResult persistResult = _workflowCoordinator.ApplySettingsAndPersist(_config, _configStore);
            if (!persistResult.Success)
            {
                SetSettingsResult(persistResult.ErrorMessage ?? "Settings could not be applied.", SettingsResultKind.Error);
                return;
            }

            UpdateUiForCurrentConfig();
            RefreshPadZoneOverlay();

            SetSettingsResult("Settings applied and saved.", SettingsResultKind.Success);
        }
        catch (Exception ex)
        {
            SetSettingsResult(ex.Message, SettingsResultKind.Error);
        }
    }

    private void RestartMidi()
    {
        _midiEngine.Restart(_config);
        _midiEngine.ClearCachedValues();
        UpdateUiForCurrentConfig();
        RefreshPadZoneOverlay();
    }

    private void LoadConfigIntoUi(AppConfig config)
    {
        _isLoadingUi = true;

        _viewModel.MaxZoneRowsText = config.MaxZoneRows.ToString();
        _viewModel.MaxZonesPerRowText = config.MaxZonesPerRow.ToString();
        RefreshZoneList();
        SelectedZoneIndex = config.Zones.Count > 0 ? 0 : -1;
        _viewModel.NotifySelectedZoneIndexChanged();
        LoadSelectedZoneIntoEditor();
        SyncToolbar();

        _isLoadingUi = false;
        RefreshPadZoneOverlay();
    }

    private void SyncToolbar()
    {
        _viewModel.SendPressureGlobally = _config.SendPressureGlobally;
        _viewModel.AllowZoneSwitchWhilePressed = _config.AllowZoneSwitchWhilePressed;
        SyncPressureSplitBorder();
        SyncInputBackendSplitBorder();
        MainWindowToolbarState toolbarState = _workflowCoordinator.BuildToolbarState(_config);
        _viewModel.ToolbarPressureLabelText = toolbarState.ToolbarPressureLabelText;
        _viewModel.SelectedPressureCurve = toolbarState.SelectedPressureCurve;
        _viewModel.ToolbarInputBackendLabelText = toolbarState.ToolbarInputBackendLabelText;
        _viewModel.SelectedInputBackendMode = toolbarState.SelectedInputBackendMode;
        _viewModel.IsPressureCurveLinearChecked = toolbarState.IsPressureCurveLinearChecked;
        _viewModel.IsPressureCurveLogarithmicChecked = toolbarState.IsPressureCurveLogarithmicChecked;
        _viewModel.IsPressureCurveExponentialChecked = toolbarState.IsPressureCurveExponentialChecked;
        _viewModel.IsInputBackendAutoChecked = toolbarState.IsInputBackendAutoChecked;
        _viewModel.IsInputBackendWintabChecked = toolbarState.IsInputBackendWintabChecked;
        _viewModel.IsInputBackendMouseChecked = toolbarState.IsInputBackendMouseChecked;
    }

    private void RefreshZoneList()
    {
        _zoneEditorService.RefreshZoneList(_viewModel.Zones, _config);
    }

    private void LoadSelectedZoneIntoEditor()
    {
        _isLoadingUi = true;

        _zoneEditorService.LoadSelectedZoneIntoEditor(_viewModel, _config, SelectedZoneIndex);

        _isLoadingUi = false;
    }

    private void CommitCurrentZoneEdits()
    {
        _zoneEditorService.CommitCurrentZoneEdits(
            _config,
            _viewModel,
            SelectedZoneIndex,
            _viewModel.MaxZoneRowsText,
            _viewModel.MaxZonesPerRowText);

        RefreshZoneList();
        _viewModel.NotifySelectedZoneIndexChanged();
    }

    private void UpdateUiForCurrentConfig()
    {
        MainWindowConnectedState statusState = _workflowCoordinator.BuildConnectedState(_config);
        _viewModel.StatusText = statusState.StatusText;
        _viewModel.RoutingText = statusState.RoutingText;
        UpdateInputBackendStatus();
        _viewModel.ActiveZoneText = statusState.ActiveZoneText;
        _viewModel.ConfigSummaryText = statusState.ConfigSummaryText;
        _viewModel.ValueText = statusState.ValueText;
        _viewModel.PenButtonsText = statusState.PenButtonsText;
        _viewModel.ModifiersText = statusState.ModifiersText;
    }

    private void Pad_StylusDown(object sender, StylusDownEventArgs e)
    {
        if (_inputBackendCoordinator.IsStylusMouseFallbackSuppressed(_useWintab, _config.InputBackendMode)) return;
        e.Handled = true;
        _isActive = true;
        Pad.CaptureStylus();
        HandleStylus(e);
    }

    private void Pad_StylusMove(object sender, StylusEventArgs e)
    {
        if (_inputBackendCoordinator.IsStylusMouseFallbackSuppressed(_useWintab, _config.InputBackendMode)) return;
        if (!_isActive) return;
        e.Handled = true;
        HandleStylus(e);
    }

    private void Pad_StylusUp(object sender, StylusEventArgs e)
    {
        if (_inputBackendCoordinator.IsStylusMouseFallbackSuppressed(_useWintab, _config.InputBackendMode)) return;
        if (!_isActive) return;
        e.Handled = true;
        HandleStylus(e);
        _isActive = false;
        Pad.ReleaseStylusCapture();
        PadSurface.ApplyCursorChrome(PadSurface.ComputeCursorAfterRelease());
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

        if (_inputBackendCoordinator.IsStylusMouseFallbackSuppressed(_useWintab, _config.InputBackendMode)) return;
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

        if (_inputBackendCoordinator.IsStylusMouseFallbackSuppressed(_useWintab, _config.InputBackendMode)) return;
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

        if (_inputBackendCoordinator.IsStylusMouseFallbackSuppressed(_useWintab, _config.InputBackendMode)) return;
        if (!_isActive) return;
        HandleMouse(e);
        _isActive = false;
        Pad.ReleaseMouseCapture();
        PadSurface.ApplyCursorChrome(PadSurface.ComputeCursorAfterRelease());
        HandleRelease();
    }

    private (int zone, ZoneResizeEdge edge) HitTestZoneEdge(Point pos)
        => _zoneResizeService.HitTestZoneEdge(_config.Zones, SelectedZoneIndex, Pad.ActualWidth, Pad.ActualHeight, pos);

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

        if (SelectedZoneIndex != zoneIndex)
        {
            CommitCurrentZoneEdits();
            SelectedZoneIndex = zoneIndex;
            LoadSelectedZoneIntoEditor();
        }
    }

    private void SelectZoneAtPoint(Point pos)
    {
        int zoneIndex = _zoneResizeService.HitTestZoneBody(_config.Zones, Pad.ActualWidth, Pad.ActualHeight, pos);
        if (zoneIndex < 0 || zoneIndex == SelectedZoneIndex)
        {
            return;
        }

        CommitCurrentZoneEdits();
        SelectedZoneIndex = zoneIndex;
        LoadSelectedZoneIntoEditor();
        RefreshPadZoneOverlay();
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
        RefreshPadZoneOverlay();
    }

    private void FinishZoneResize()
    {
        _isResizingZone = false;
        _zoneResizeSession = null;
        Pad.Cursor = null;
        SetSettingsResult("Zone resized. Click Apply Settings to save.", SettingsResultKind.Success);
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
        PadSurface.ApplyCursorChrome(PadSurface.ComputeCursorForInteractionTick(
            input,
            pixelPosition,
            cursorOpacityOverride));

        InteractionResult result = _inputInteractionOrchestrator.ProcessTick(
            _inputInteractionOrchestrator.BuildPadInteractionConfig(_config),
            _inputInteractionOrchestrator.BuildZoneSnapshots(_config),
            _interactionState,
            input);

        ApplyInteractionResult(result);

        RefreshPadZoneOverlay();
    }

    private void HandleRelease()
    {
        // Release tick goes through engine so release behavior stays centralized.
        InteractionResult releaseResult = _inputInteractionOrchestrator.ProcessRelease(
            _inputInteractionOrchestrator.BuildPadInteractionConfig(_config),
            _inputInteractionOrchestrator.BuildZoneSnapshots(_config),
            _interactionState,
            _lastWintabButtons);
        ApplyInteractionResult(releaseResult);
        RefreshPadZoneOverlay();

        PadSurface.ApplyCursorChrome(PadSurface.ComputeCursorAfterRelease());
    }

    private void RefreshPadZoneOverlay()
        => PadSurface.RefreshZoneOverlay(_config.Zones, _interactionOutcomeApplier.LastZoneMarkers, SelectedZoneIndex);

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoadingUi)
        {
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.SendPressureGlobally))
        {
            _config.SendPressureGlobally = _viewModel.SendPressureGlobally;
            SyncPressureSplitBorder();
        }
        else if (e.PropertyName == nameof(MainViewModel.AllowZoneSwitchWhilePressed))
        {
            _config.AllowZoneSwitchWhilePressed = _viewModel.AllowZoneSwitchWhilePressed;
            UpdateUiForCurrentConfig();
        }
    }

    private void InitializeWintab()
    {
        PopulateTabletDeviceList();
        int tabletDeviceIndex = _viewModel.SelectedTabletDevice?.DeviceIndex >= 0
            ? _viewModel.SelectedTabletDevice!.DeviceIndex
            : -1;
        InputBackendLifecycleResult lifecycle = _inputBackendCoordinator.Initialize(
            _config.InputBackendMode,
            _wintabInputBackend,
            tabletDeviceIndex,
            _expressKeyCount);
        ApplyInputBackendLifecycleState(lifecycle, refreshInputBackendStatusText: true);
    }

    private void ApplyInputBackendLifecycleState(InputBackendLifecycleResult lifecycle, bool refreshInputBackendStatusText)
    {
        _useWintab = lifecycle.UseWintab;
        _expressKeyCount = lifecycle.ExpressKeyCount;
        _lastInputBackend = lifecycle.LastInputBackend;
        _wintabDiagnostics = lifecycle.WintabDiagnostics;
        if (refreshInputBackendStatusText)
        {
            UpdateInputBackendStatus();
        }
    }

    private void PopulateTabletDeviceList()
    {
        TabletDeviceComboBox.SelectionChanged -= TabletDeviceComboBox_SelectionChanged;

        int previousDeviceIndex = _viewModel.SelectedTabletDevice?.DeviceIndex ?? -1;
        _tabletDevicePickerSyncService.Refresh(_viewModel, previousDeviceIndex);

        TabletDeviceComboBox.SelectionChanged += TabletDeviceComboBox_SelectionChanged;
    }

    private void TabletDeviceComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isLoadingUi)
        {
            return;
        }

        if (_viewModel.SelectedTabletDevice is not TabletDeviceListItemViewModel item || item.DeviceIndex < 0)
        {
            return;
        }

        RebindInputBackend();
    }

    private void ShutdownWintab()
    {
        ApplyInputBackendLifecycleState(
            _lifecycleCoordinator.ShutdownTabletInput(_inputBackendCoordinator, _wintabInputBackend),
            refreshInputBackendStatusText: false);
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
        PadSurface.ApplyCursorChrome(PadSurface.ComputeCursorAfterRelease());
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
        WintabExpressKeyBridgeOutcome outcome = _wintabInputBridge.BuildExpressKeyBridge(expressKey, _config.ExpressKeys);
        _expressKeyCount = Math.Max(_expressKeyCount, outcome.RequiredExpressKeyCount);
        ApplyInteractionResult(_inputInteractionOrchestrator.ProcessTick(
            _inputInteractionOrchestrator.BuildPadInteractionConfig(_config),
            _inputInteractionOrchestrator.BuildZoneSnapshots(_config),
            _interactionState,
            outcome.EngineInput));
        _expressKeyDiagnostics = outcome.ExpressKeyDiagnostics;
        MarkInputBackend(outcome.BackendLabel);
        UpdateInputBackendStatus();
    }

    private void HandleWintabPacket(WintabPacketEventArgs packet)
    {
        if (!_wintabInputBridge.TryBuildPacketBridge(
            packet,
            Pad.ActualWidth,
            Pad.ActualHeight,
            _isActive,
            out WintabPacketBridgeOutcome bridge))
        {
            return;
        }

        MarkInputBackend(bridge.BackendLabel);
        UpdatePenButtonsDebug(bridge.PenButtons);

        _wintabDiagnostics = bridge.PacketDiagnostics;
        UpdateInputBackendStatus();

        WintabPacketFlowResult flow = bridge.Flow;
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
        _viewModel.WacomServiceStatusHeader = menuState.Header;
        _viewModel.IsWacomServiceStartEnabled = menuState.StartEnabled;
    }

    private void SetSettingsResult(string message, SettingsResultKind kind)
    {
        _viewModel.SettingsResultMessage = message;
        _viewModel.SettingsResultKind = kind;
    }

    private void UpdatePenButtonsDebug(uint buttons)
    {
        _lastWintabButtons = buttons;
        _viewModel.PenButtonsText = _inputBackendStatusFormatter.BuildPenButtonsText(buttons);
    }

}
