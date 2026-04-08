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

    private static readonly IMainWindowLifecycleCoordinator DesignerLifecycleCoordinator = new MainWindowLifecycleCoordinator();

    private static readonly IMainWindowStartupLifecycleService DesignerStartupLifecycle =
        new MainWindowStartupLifecycleService(DesignerLifecycleCoordinator);

    private static readonly MainWindowInputBackendCoordinator DesignerInputBackendCoordinator = new();

    private static readonly MainWindowWintabInputBridge DesignerWintabInputBridge = new(new WintabEventAdapter(), DesignerInputBackendCoordinator);

    private bool _isLoadingUi;

    private readonly PadInputSessionState _padState = new();
    private readonly PadInputHost _padInputHost;
    private readonly IMainWindowPadInputSession _padInputSession;

    private readonly IConfigStore _configStore;
    private readonly IMidiEngine _midiEngine;
    private readonly IWacomServiceController _wacomServiceController;
    private readonly IWintabInputBackend _wintabInputBackend;
    private readonly ITabletDevicePickerSyncService _tabletDevicePickerSyncService;
    private readonly IPreferencesDialogService _preferencesDialogService;
    private readonly IZoneConfigurationService _zoneConfigurationService;
    private readonly IZoneEditorService _zoneEditorService;
    private readonly IZoneResizeService _zoneResizeService;
    private readonly IInputBackendStatusFormatter _inputBackendStatusFormatter;
    private readonly IMainWindowWorkflowCoordinator _workflowCoordinator;
    private readonly IMainWindowInputBackendCoordinator _inputBackendCoordinator;
    private readonly IWacomServiceWorkflowCoordinator _wacomServiceWorkflowCoordinator;
    private readonly IMainWindowLifecycleCoordinator _lifecycleCoordinator;
    private readonly IMainWindowStartupLifecycleService _startupLifecycle;
    private readonly IMainWindowToolbarSession _toolbarSession;
    private readonly IZoneActionWorkflowCoordinator _zoneActionWorkflowCoordinator;
    private readonly IPadInteractionOutcomeApplier _interactionOutcomeApplier;
    private readonly MainViewModel _viewModel;
    private AppConfig _config = AppConfig.Default;
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
            DesignerLifecycleCoordinator,
            DesignerStartupLifecycle,
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
        IMainWindowStartupLifecycleService startupLifecycle,
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
        ArgumentNullException.ThrowIfNull(inputFallbackService);
        _inputBackendStatusFormatter = inputBackendStatusFormatter ?? throw new ArgumentNullException(nameof(inputBackendStatusFormatter));
        ArgumentNullException.ThrowIfNull(inputInteractionOrchestrator);
        _workflowCoordinator = workflowCoordinator ?? throw new ArgumentNullException(nameof(workflowCoordinator));
        _inputBackendCoordinator = inputBackendCoordinator ?? throw new ArgumentNullException(nameof(inputBackendCoordinator));
        _wacomServiceWorkflowCoordinator = wacomServiceWorkflowCoordinator ?? throw new ArgumentNullException(nameof(wacomServiceWorkflowCoordinator));
        _lifecycleCoordinator = lifecycleCoordinator ?? throw new ArgumentNullException(nameof(lifecycleCoordinator));
        _startupLifecycle = startupLifecycle ?? throw new ArgumentNullException(nameof(startupLifecycle));
        ArgumentNullException.ThrowIfNull(wintabInputBridge);
        ArgumentNullException.ThrowIfNull(padPresentationCoordinator);
        _toolbarSession = toolbarSession ?? throw new ArgumentNullException(nameof(toolbarSession));
        _zoneActionWorkflowCoordinator = zoneActionWorkflowCoordinator ?? throw new ArgumentNullException(nameof(zoneActionWorkflowCoordinator));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _interactionOutcomeApplier = interactionOutcomeApplier ?? throw new ArgumentNullException(nameof(interactionOutcomeApplier));
        InitializeComponent();
        _padInputHost = new PadInputHost(this);
        _padInputSession = new MainWindowPadInputSession(
            _padInputHost,
            _tabletDevicePickerSyncService,
            _inputBackendCoordinator,
            _lifecycleCoordinator,
            _wintabInputBackend,
            wintabInputBridge,
            inputFallbackService,
            inputInteractionOrchestrator,
            _inputBackendStatusFormatter);
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
        _wintabInputBackend.PacketReceived += (_, e) => _padInputSession.HandleWintabPacket(e);
        _wintabInputBackend.ExpressKeyReceived += (_, e) => _padInputSession.HandleExpressKeyPacket(e);
        _wintabInputBackend.ProximityChanged += (_, e) => _padInputSession.HandleProximityChanged(e);
        _wintabInputBackend.DiagnosticsChanged += (_, diagnostics) => _padInputSession.OnWintabDiagnosticsChanged(diagnostics);
        WindowChromeTheme.ApplyDarkNonClientChrome(this);
        ConfigureNumericInputs();
        ContentRendered += OnContentRendered;
        Closed += (_, _) =>
        {
            _startupLifecycle.ShutdownForExit(
                _midiEngine,
                _config,
                _inputBackendCoordinator,
                _wintabInputBackend,
                r => _padInputSession.ApplyTabletLifecycle(r, refreshInputBackendStatusText: false));
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
        int effectiveExpressKeyCount = _workflowCoordinator.GetEffectiveExpressKeyCount(_padState.ExpressKeyCount, _config.ExpressKeys.Count);
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
            _padInputSession.RebindInputBackend();
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
                _padInputSession.RebindInputBackend();
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
        _config = _startupLifecycle.LoadPersistedConfig(_configStore);
        LoadConfigIntoUi(_config);
        _startupLifecycle.RunPostConfigLoadShellPhase(new MainWindowPostConfigLoadShellContext(
            GetWacomServiceStatus: () => _wacomServiceController.GetStatus(),
            ApplyWacomServiceMenuState: RefreshWacomServiceStatus,
            Config: _config,
            InitializeTabletWhenReady: () => _padInputSession.InitializeTabletInput(),
            ApplyTabletBlockedByWacomService: ApplyTabletStackUnavailableAtStartup));

        await _startupLifecycle.RunStartupMidiSequenceAsync(
            _midiEngine,
            _config,
            _viewModel,
            a => Dispatcher.Invoke(a),
            UpdateUiForCurrentConfig);
    }

    private void ApplyTabletStackUnavailableAtStartup(WacomServiceStatus wacom)
    {
        _padInputSession.ShutdownWintabBackendOnly();
        _padInputSession.RefreshTabletDeviceListOnly();
        InputBackendLifecycleResult blocked = _startupLifecycle.BuildTabletStackBlockedLifecycleResult(_config, wacom);
        _padInputSession.ApplyTabletLifecycle(blocked, refreshInputBackendStatusText: true);
    }

    private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ApplySettings();
    }

    private void ReconnectPenCore()
    {
        CommitCurrentZoneEdits();
        _padInputSession.RebindInputBackend();
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
            _padInputSession.RebindInputBackend();
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
        => _padInputSession.HandleStylusDown(e);

    private void Pad_StylusMove(object sender, StylusEventArgs e)
        => _padInputSession.HandleStylusMove(e);

    private void Pad_StylusUp(object sender, StylusEventArgs e)
        => _padInputSession.HandleStylusUp(e);

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

        _padInputSession.HandlePadMouseDownForInteraction(e);
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

        _padInputSession.HandlePadMouseMoveForInteraction(e);
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

        _padInputSession.HandlePadMouseUpForInteraction(e);
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

        _padInputSession.RebindInputBackend();
    }

    private static bool IsPromotedStylusMouseEvent(MouseEventArgs e) => e.StylusDevice is not null;

    private void UpdateInputBackendStatus()
    {
        _viewModel.InputBackendText = _inputBackendStatusFormatter.BuildStatusText(
            _padState.LastInputBackend,
            _padState.UseWintab,
            _wintabInputBackend.Contexts.Count,
            _padState.WintabDiagnostics,
            _padState.ExpressKeyDiagnostics);
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

    private sealed class PadInputHost : IMainWindowPadInputSessionHost
    {
        private readonly MainWindow _w;

        public PadInputHost(MainWindow window) => _w = window;

        public Canvas Pad => _w.Pad;

        public IPadInputChromeSurface PadChrome => _w.PadSurface;

        public AppConfig Config => _w._config;

        public MainViewModel ViewModel => _w._viewModel;

        public InteractionState InteractionState => _w._interactionState;

        public IPadInteractionOutcomeApplier OutcomeApplier => _w._interactionOutcomeApplier;

        public PadInputSessionState PadState => _w._padState;

        public int SelectedZoneIndex
        {
            get => _w.SelectedZoneIndex;
            set => _w.SelectedZoneIndex = value;
        }

        public bool IsLoadingUi => _w._isLoadingUi;

        public void ApplyInteractionResult(InteractionResult result) => _w.ApplyInteractionResult(result);

        public void RefreshPadZoneOverlay() => _w.RefreshPadZoneOverlay();

        public void UpdateInputBackendStatus() => _w.UpdateInputBackendStatus();

        public void OnRebindCompletedRefreshRouting() => _w.UpdateUiForCurrentConfig();

        public void BeginPopulateTabletDeviceList()
            => _w.TabletDeviceComboBox.SelectionChanged -= _w.TabletDeviceComboBox_SelectionChanged;

        public void EndPopulateTabletDeviceList()
            => _w.TabletDeviceComboBox.SelectionChanged += _w.TabletDeviceComboBox_SelectionChanged;
    }
}
