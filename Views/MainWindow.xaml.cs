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
    private readonly PadInputControllerState _padState = new();
    private readonly PadInputHost _padInputHost;
    private readonly IPadInputController _padInputSession;
    private readonly IZonePadController _zonePadSession;
    private readonly StartupHost _startupHost;

    private readonly IRuntimeConfigService _runtimeConfig;
    private readonly IMidiEngine _midiEngine;
    private readonly IWacomServiceController _wacomServiceController;
    private readonly IWintabInputBackend _wintabInputBackend;
    private readonly IInputBackendStatusFormatter _inputBackendStatusFormatter;
    private readonly IWorkflowCoordinator _workflowCoordinator;
    private readonly IInputBackendCoordinator _inputBackendCoordinator;
    private readonly ILifecycleCoordinator _lifecycleCoordinator;
    private readonly IStartupLifecycleService _startupLifecycle;
    private readonly IPadInteractionOutcomeApplier _interactionOutcomeApplier;
    private readonly MainViewModel _viewModel;
    private readonly InteractionState _interactionState = new();

    private Canvas Pad => PadSurface.PadInputElement;

    private AppConfig Config => _runtimeConfig.Current;

    private int SelectedZoneIndex
    {
        get => _viewModel.ZoneEditor.SelectedZoneIndex;
        set => _viewModel.ZoneEditor.SelectedZoneIndex = value;
    }

    private void ApplyInteractionResult(InteractionResult result)
        => _interactionOutcomeApplier.Apply(result, _interactionState, Config, _viewModel);

    /// <summary>
    /// Application entry constructs the window via <see cref="TabletMidiPad.Composition.AppBootstrapper.CreateApplicationShell"/>.
    /// </summary>
    internal MainWindow(
        IRuntimeConfigService runtimeConfig,
        IMidiEngine midiEngine,
        IWacomServiceController wacomServiceController,
        IWintabInputBackend wintabInputBackend,
        ITabletDevicePickerSyncService tabletDevicePickerSyncService,
        IPreferencesDialogService preferencesDialogService,
        IZoneConfigurationService zoneConfigurationService,
        IZoneOverlayRenderer zoneOverlayRenderer,
        IZoneResizeService zoneResizeService,
        IInputFallbackService inputFallbackService,
        IInputInteractionOrchestrator inputInteractionOrchestrator,
        IInputBackendStatusFormatter inputBackendStatusFormatter,
        IWorkflowCoordinator workflowCoordinator,
        IInputBackendCoordinator inputBackendCoordinator,
        IWacomServiceWorkflowCoordinator wacomServiceWorkflowCoordinator,
        ILifecycleCoordinator lifecycleCoordinator,
        IStartupLifecycleService startupLifecycle,
        IWintabInputBridge wintabInputBridge,
        IPadPresentationCoordinator padPresentationCoordinator,
        IMainViewModelFactory mainViewModelFactory,
        IPadInteractionOutcomeApplier interactionOutcomeApplier)
    {
        _runtimeConfig = runtimeConfig ?? throw new ArgumentNullException(nameof(runtimeConfig));
        _midiEngine = midiEngine ?? throw new ArgumentNullException(nameof(midiEngine));
        _wacomServiceController = wacomServiceController ?? throw new ArgumentNullException(nameof(wacomServiceController));
        _wintabInputBackend = wintabInputBackend ?? throw new ArgumentNullException(nameof(wintabInputBackend));
        ArgumentNullException.ThrowIfNull(zoneOverlayRenderer);
        ArgumentNullException.ThrowIfNull(inputFallbackService);
        _inputBackendStatusFormatter = inputBackendStatusFormatter ?? throw new ArgumentNullException(nameof(inputBackendStatusFormatter));
        ArgumentNullException.ThrowIfNull(inputInteractionOrchestrator);
        _workflowCoordinator = workflowCoordinator ?? throw new ArgumentNullException(nameof(workflowCoordinator));
        _inputBackendCoordinator = inputBackendCoordinator ?? throw new ArgumentNullException(nameof(inputBackendCoordinator));
        ArgumentNullException.ThrowIfNull(wacomServiceWorkflowCoordinator);
        _lifecycleCoordinator = lifecycleCoordinator ?? throw new ArgumentNullException(nameof(lifecycleCoordinator));
        _startupLifecycle = startupLifecycle ?? throw new ArgumentNullException(nameof(startupLifecycle));
        ArgumentNullException.ThrowIfNull(wintabInputBridge);
        ArgumentNullException.ThrowIfNull(padPresentationCoordinator);
        ArgumentNullException.ThrowIfNull(mainViewModelFactory);
        _interactionOutcomeApplier = interactionOutcomeApplier ?? throw new ArgumentNullException(nameof(interactionOutcomeApplier));
        InitializeComponent();
        _padInputHost = new PadInputHost(this);
        _padInputSession = new PadInputController(
            _padInputHost,
            tabletDevicePickerSyncService ?? throw new ArgumentNullException(nameof(tabletDevicePickerSyncService)),
            _inputBackendCoordinator,
            _lifecycleCoordinator,
            _wintabInputBackend,
            wintabInputBridge,
            inputFallbackService,
            inputInteractionOrchestrator,
            _inputBackendStatusFormatter);
        _zonePadSession = new ZonePadController(
            new ZonePadHost(this),
            zoneResizeService ?? throw new ArgumentNullException(nameof(zoneResizeService)));
        ISettingsWorkflow settingsWorkflow = new SettingsWorkflow(
            new SettingsSessionHost(this),
            _workflowCoordinator,
            preferencesDialogService ?? throw new ArgumentNullException(nameof(preferencesDialogService)),
            zoneConfigurationService ?? throw new ArgumentNullException(nameof(zoneConfigurationService)),
            _runtimeConfig,
            _midiEngine,
            _padInputSession);
        _startupHost = new StartupHost(this);
        var shellCommandController = new MainShellCommandController(
            new MainShellCommandHost(this),
            settingsWorkflow,
            wacomServiceWorkflowCoordinator,
            _wacomServiceController,
            _workflowCoordinator,
            _padInputSession);
        _viewModel = mainViewModelFactory.Create(new MainShellCommandsViewModel(shellCommandController));
        _viewModel.ZoneList.SettingsResultRequested += SetSettingsResult;
        _viewModel.ZoneList.PadZoneOverlayRefreshRequested += RefreshPadZoneOverlay;
        _viewModel.Toolbar.InputBackendRebindRequested += () => _padInputSession.RebindInputBackend();
        _viewModel.Toolbar.PressureChromeRefreshRequested += SyncPressureSplitBorder;
        _viewModel.Toolbar.RoutingStatusRefreshRequested += UpdateUiForCurrentConfig;
        PadSurface.AttachPresentationServices(padPresentationCoordinator, zoneOverlayRenderer);
        PadSurface.PadLayoutChanged += (_, _) => RefreshPadZoneOverlay();
        PadSurface.PadStylusDown += Pad_StylusDown;
        PadSurface.PadStylusMove += Pad_StylusMove;
        PadSurface.PadStylusUp += Pad_StylusUp;
        PadSurface.PadMouseDown += Pad_MouseDown;
        PadSurface.PadMouseMove += Pad_MouseMove;
        PadSurface.PadMouseUp += Pad_MouseUp;
        DataContext = _viewModel;
        ApplyInputBackendSplitBorderChrome();
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
                Config,
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

    private async void OnContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= OnContentRendered;
        await InitializeAppAsync();
    }

    private async Task InitializeAppAsync()
    {
        await _startupLifecycle.RunColdStartAsync(
            _startupHost,
            _midiEngine,
            _viewModel,
            a => Dispatcher.Invoke(a),
            UpdateUiForCurrentConfig);
    }

    private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        _viewModel.ShellCommands.SaveSettingsCommand.Execute(null);
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

    private void ZoneListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel.ZoneEditor.OnZoneListSelectionChanged())
        {
            RefreshPadZoneOverlay();
        }
    }

    private void SyncToolbar() => _viewModel.Toolbar.SyncFromConfig();

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
        => PadSurface.RefreshZoneOverlay(Config.Zones, _interactionOutcomeApplier.LastZoneMarkers, SelectedZoneIndex);

    private void TabletDeviceComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_viewModel.ZoneEditor.IsLoadingUi)
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

    private void RefreshWacomServiceStatus(WacomServiceStatus status)
    {
        WacomServiceMenuState menuState = _workflowCoordinator.BuildWacomServiceMenuState(status);
        _viewModel.WacomServiceStatusHeader = menuState.Header;
        _viewModel.IsWacomServiceStartEnabled = menuState.StartEnabled;
    }

    private void LoadConfigIntoUi()
    {
        _viewModel.ZoneEditor.LoadConfigIntoUi();
        SyncToolbar();
        RefreshPadZoneOverlay();
    }

    private void UpdateUiForCurrentConfig()
    {
        ConnectedState statusState = _workflowCoordinator.BuildConnectedState(Config);
        _viewModel.StatusText = statusState.StatusText;
        _viewModel.RoutingText = statusState.RoutingText;
        UpdateInputBackendStatus();
        _viewModel.ActiveZoneText = statusState.ActiveZoneText;
        _viewModel.ConfigSummaryText = statusState.ConfigSummaryText;
        _viewModel.ValueText = statusState.ValueText;
        _viewModel.PenButtonsText = statusState.PenButtonsText;
        _viewModel.ModifiersText = statusState.ModifiersText;
    }

    private void ApplyInputBackendSplitBorderChrome()
    {
        InputBackendSplitBorder.Background = TryFindResource("AccentSurfaceBrush") as System.Windows.Media.Brush
            ?? System.Windows.Media.Brushes.Transparent;
        InputBackendSplitBorder.BorderBrush = TryFindResource("AccentBrush") as System.Windows.Media.Brush
            ?? System.Windows.Media.Brushes.Transparent;
    }

    private void SyncPressureSplitBorder()
    {
        bool on = _viewModel.Toolbar.SendPressureGlobally;
        PressureSplitBorder.Background = on
            ? TryFindResource("AccentSurfaceBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Transparent
            : System.Windows.Media.Brushes.Transparent;
        PressureSplitBorder.BorderBrush = on
            ? TryFindResource("AccentBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Transparent
            : System.Windows.Media.Brushes.Transparent;
    }

    private void SetSettingsResult(string message, SettingsResultKind kind)
    {
        _viewModel.SettingsResultMessage = message;
        _viewModel.SettingsResultKind = kind;
    }
}
