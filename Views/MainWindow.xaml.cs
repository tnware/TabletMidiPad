using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TabletMidiPad.Core.Interaction;
using TabletMidiPad.Services;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Views;

public partial class MainWindow : Window
{
    private bool _isLoadingUi;

    internal PadInputSessionState PadStateRef => _padState;
    internal IMainWindowZoneEditorSession ZoneEditorSessionRef => _zoneEditorSession;
    internal ISettingsService SettingsServiceRef => _settingsService;
    internal void SetSettingsResultRef(string message, SettingsResultKind kind) => SetSettingsResult(message, kind);
    internal void RefreshPadZoneOverlayRef() => RefreshPadZoneOverlay();
    internal void SyncToolbarRef() => SyncToolbar();
    internal Canvas PadRef => Pad;
    internal IPadInputChromeSurface PadSurfaceRef => PadSurface;
    internal MainViewModel ViewModelRef => _viewModel;
    internal InteractionState InteractionStateRef => _interactionState;
    internal IPadInteractionOutcomeApplier OutcomeApplierRef => _interactionOutcomeApplier;
    internal bool IsLoadingUiRef => _isLoadingUi;
    internal void ApplyInteractionResultRef(InteractionResult result) => ApplyInteractionResult(result);
    internal void UpdateInputBackendStatusRef() => UpdateInputBackendStatus();
    internal void BeginPopulateTabletDeviceListRef() => TabletDeviceComboBox.SelectionChanged -= TabletDeviceComboBox_SelectionChanged;
    internal void EndPopulateTabletDeviceListRef() => TabletDeviceComboBox.SelectionChanged += TabletDeviceComboBox_SelectionChanged;

    internal void ShutdownWintabBackendOnly() => _padInputSession.ShutdownWintabBackendOnly();
    internal void RefreshTabletDeviceListOnly() => _padInputSession.RefreshTabletDeviceListOnly();
    internal void InitializeTabletInput() => _padInputSession.InitializeTabletInput();
    internal void ApplyTabletLifecycle(InputBackendLifecycleResult result, bool refresh) => _padInputSession.ApplyTabletLifecycle(result, refresh);
    internal InputBackendLifecycleResult BuildTabletStackBlockedLifecycleResult(AppConfig config, WacomServiceStatus wacom) => _startupLifecycle.BuildTabletStackBlockedLifecycleResult(config, wacom);

    private readonly PadInputSessionState _padState = new();
    private readonly PadInputHost _padInputHost;
    private readonly IMainWindowPadInputSession _padInputSession;
    private readonly IMainWindowZonePadSession _zonePadSession;
    private readonly IMainWindowZoneEditorSession _zoneEditorSession;
    private readonly IMainWindowSettingsSession _settingsSession;
    private readonly StartupHost _startupHost;
    private readonly IMainWindowZoneListSession _zoneListSession;

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
    private readonly MainWindowToolbarChrome _toolbarChrome;
    private readonly MainWindowViewModelConfigBinder _vmConfigBinder;
    private readonly IPadInteractionOutcomeApplier _interactionOutcomeApplier;
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settingsService;
    private AppConfig _config => _settingsService.Config;
    private readonly InteractionState _interactionState = new();

    private Canvas Pad => PadSurface.PadInputElement;

    private int SelectedZoneIndex
    {
        get => _viewModel.SelectedZoneIndex;
        set => _viewModel.SelectedZoneIndex = value;
    }

    private void ApplyInteractionResult(InteractionResult result)
        => _interactionOutcomeApplier.Apply(result, _interactionState, _config, _viewModel);

    /// <summary>
    /// Application entry constructs the window via <see cref="TabletMidiPad.Composition.AppBootstrapper.CreateApplicationShell"/>.
    /// </summary>
    internal MainWindow(MainWindowDependencies d)
    {
        _configStore = d.ConfigStore ?? throw new ArgumentNullException(nameof(d.ConfigStore));
        _midiEngine = d.MidiEngine ?? throw new ArgumentNullException(nameof(d.MidiEngine));
        _wacomServiceController = d.WacomServiceController ?? throw new ArgumentNullException(nameof(d.WacomServiceController));
        _wintabInputBackend = d.WintabInputBackend ?? throw new ArgumentNullException(nameof(d.WintabInputBackend));
        _tabletDevicePickerSyncService = d.TabletDevicePickerSyncService ?? throw new ArgumentNullException(nameof(d.TabletDevicePickerSyncService));
        _preferencesDialogService = d.PreferencesDialogService ?? throw new ArgumentNullException(nameof(d.PreferencesDialogService));
        _zoneConfigurationService = d.ZoneConfigurationService ?? throw new ArgumentNullException(nameof(d.ZoneConfigurationService));
        _zoneEditorService = d.ZoneEditorService ?? throw new ArgumentNullException(nameof(d.ZoneEditorService));
        _zoneResizeService = d.ZoneResizeService ?? throw new ArgumentNullException(nameof(d.ZoneResizeService));
        _inputBackendStatusFormatter = d.InputBackendStatusFormatter ?? throw new ArgumentNullException(nameof(d.InputBackendStatusFormatter));
        _workflowCoordinator = d.WorkflowCoordinator ?? throw new ArgumentNullException(nameof(d.WorkflowCoordinator));
        _inputBackendCoordinator = d.InputBackendCoordinator ?? throw new ArgumentNullException(nameof(d.InputBackendCoordinator));
        _wacomServiceWorkflowCoordinator = d.WacomServiceWorkflowCoordinator ?? throw new ArgumentNullException(nameof(d.WacomServiceWorkflowCoordinator));
        _lifecycleCoordinator = d.LifecycleCoordinator ?? throw new ArgumentNullException(nameof(d.LifecycleCoordinator));
        _startupLifecycle = d.StartupLifecycle ?? throw new ArgumentNullException(nameof(d.StartupLifecycle));
        _toolbarSession = d.ToolbarSession ?? throw new ArgumentNullException(nameof(d.ToolbarSession));
        _viewModel = d.ViewModel ?? throw new ArgumentNullException(nameof(d.ViewModel));
        _interactionOutcomeApplier = d.InteractionOutcomeApplier ?? throw new ArgumentNullException(nameof(d.InteractionOutcomeApplier));
        _settingsService = d.SettingsService ?? throw new ArgumentNullException(nameof(d.SettingsService));
        InitializeComponent();
        _toolbarChrome = new MainWindowToolbarChrome(
            this,
            PressureSplitBorder,
            InputBackendSplitBorder,
            _viewModel,
            () => _config,
            _workflowCoordinator);
        _padInputHost = new PadInputHost(this);
        _padInputSession = new MainWindowPadInputSession(
            _padInputHost,
            _tabletDevicePickerSyncService,
            _inputBackendCoordinator,
            _lifecycleCoordinator,
            _wintabInputBackend,
            d.WintabInputBridge,
            d.InputFallbackService,
            d.InputInteractionOrchestrator,
            _inputBackendStatusFormatter);
        _zonePadSession = new MainWindowZonePadSession(new ZonePadHost(this), _zoneResizeService);
        _zoneEditorSession = new MainWindowZoneEditorSession(
            new ZoneEditorHost(this),
            _zoneEditorService,
            _workflowCoordinator);
        _vmConfigBinder = new MainWindowViewModelConfigBinder(
            () => _isLoadingUi,
            () => _config,
            _viewModel,
            () => _toolbarChrome.SyncPressureSplitBorder(),
            () => _zoneEditorSession.UpdateUiForCurrentConfig());
        _settingsSession = new MainWindowSettingsSession(
            new SettingsSessionHost(this),
            _workflowCoordinator,
            _preferencesDialogService,
            _zoneConfigurationService,
            _configStore,
            _midiEngine,
            _padInputSession,
            _settingsService);
        _startupHost = new StartupHost(this);
        _zoneListSession = new MainWindowZoneListSession(new ZoneListHost(this), d.ZoneActionWorkflowCoordinator);
        PadSurface.AttachPresentationServices(d.PadPresentationCoordinator, d.ZoneOverlayRenderer);
        PadSurface.PadLayoutChanged += (_, _) => RefreshPadZoneOverlay();
        PadSurface.PadStylusDown += Pad_StylusDown;
        PadSurface.PadStylusMove += Pad_StylusMove;
        PadSurface.PadStylusUp += Pad_StylusUp;
        PadSurface.PadMouseDown += Pad_MouseDown;
        PadSurface.PadMouseMove += Pad_MouseMove;
        PadSurface.PadMouseUp += Pad_MouseUp;
        DataContext = _viewModel;
        InitializeCommands();
        _viewModel.PropertyChanged += _vmConfigBinder.OnPropertyChanged;
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
            saveSettings: _settingsSession.ApplySettings,
            resetDefaults: _settingsSession.ResetToDefaultsAndApply,
            openPreferences: _settingsSession.RunPreferencesFlow,
            exit: Close,
            refreshWacomStatus: RefreshWacomServiceStatus,
            reconnectPen: ReconnectPenCore,
            startWacomService: () => _ = StartWacomServiceAsync(),
            showAbout: MenuAbout,
            addZone: _zoneListSession.AddZone,
            removeZone: _zoneListSession.RemoveZone,
            autoLayout: _zoneListSession.AutoLayoutZones);
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
        _viewModel.AboutCommand.Execute(null);
    }

    private async Task InitializeAppAsync()
    {
        await _startupLifecycle.RunColdStartAsync(
            _configStore,
            _startupHost,
            _midiEngine,
            _viewModel,
            a => Dispatcher.Invoke(a),
            _zoneEditorSession.UpdateUiForCurrentConfig);
    }

    private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        _settingsSession.ApplySettings();
    }

    private void ReconnectPenCore()
    {
        _zoneEditorSession.CommitCurrentZoneEdits();
        _viewModel.ReconnectPenCommand.Execute(null);
        SetSettingsResult("Input backend reconnected.", SettingsResultKind.Success);
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
        => _zoneEditorSession.OnZoneListSelectionChanged();

    public void SyncToolbar() => _toolbarChrome.SyncAll();

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

        Point pos = e.GetPosition(Pad);
        if (_zonePadSession.TryHandleMouseDown(pos))
        {
            Pad.CaptureMouse();
            e.Handled = true;
            return;
        }

        _padInputSession.HandlePadMouseDownForInteraction(e);
    }

    private void Pad_MouseMove(object sender, MouseEventArgs e)
    {
        if (IsPromotedStylusMouseEvent(e)) return;

        Point pos = e.GetPosition(Pad);
        if (_zonePadSession.TryConsumeMouseMove(pos, out ZonePadResizeCursorKind cursorKind))
        {
            e.Handled = true;
            return;
        }

        Pad.Cursor = CursorFromResizeKind(cursorKind);

        _padInputSession.HandlePadMouseMoveForInteraction(e);
    }

    private void Pad_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (IsPromotedStylusMouseEvent(e)) return;

        if (_zonePadSession.TryConsumeMouseUp())
        {
            Pad.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        _padInputSession.HandlePadMouseUpForInteraction(e);
    }

    private static Cursor? CursorFromResizeKind(ZonePadResizeCursorKind kind)
    {
        return kind switch
        {
            ZonePadResizeCursorKind.SizeWE => Cursors.SizeWE,
            ZonePadResizeCursorKind.SizeNS => Cursors.SizeNS,
            ZonePadResizeCursorKind.SizeNWSE => Cursors.SizeNWSE,
            ZonePadResizeCursorKind.SizeNESW => Cursors.SizeNESW,
            ZonePadResizeCursorKind.None => null,
            _ => null
        };
    }

    public void RefreshPadZoneOverlay()
        => PadSurface.RefreshZoneOverlay(_config.Zones, _interactionOutcomeApplier.LastZoneMarkers, SelectedZoneIndex);

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

    internal void SetSettingsResult(string message, SettingsResultKind kind)
    {
        _viewModel.SettingsResultMessage = message;
        _viewModel.SettingsResultKind = kind;
    }
}
