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
    private bool _isLoadingUi;

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

    /// <summary>
    /// Application entry constructs the window via <see cref="TabletMidiPad.Composition.AppBootstrapper.CreateApplicationShell"/>.
    /// </summary>
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
        ArgumentNullException.ThrowIfNull(zoneActionWorkflowCoordinator);
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
        _zonePadSession = new MainWindowZonePadSession(new ZonePadHost(this), _zoneResizeService);
        _zoneEditorSession = new MainWindowZoneEditorSession(
            new ZoneEditorHost(this),
            _zoneEditorService,
            _workflowCoordinator);
        _settingsSession = new MainWindowSettingsSession(
            new SettingsSessionHost(this),
            _workflowCoordinator,
            _preferencesDialogService,
            _zoneConfigurationService,
            _configStore,
            _midiEngine,
            _padInputSession);
        _startupHost = new StartupHost(this);
        _zoneListSession = new MainWindowZoneListSession(new ZoneListHost(this), zoneActionWorkflowCoordinator);
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
        MessageBox.Show(
            "Tablet MIDI Pad\n\nTurns a Wacom tablet into a MIDI XY controller with configurable zones.",
            "About Tablet MIDI Pad",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
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
        => _zoneEditorSession.OnZoneListSelectionChanged();

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
            _zoneEditorSession.UpdateUiForCurrentConfig();
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
}
