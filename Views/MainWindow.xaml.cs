using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TabletMidiPad.Core.Interaction;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Views;

public partial class MainWindow : Window
{
    private readonly PadInputControllerState _padState = new();
    private readonly IPadInputController _padInputSession;
    private readonly ZonePadController _zonePadSession;
    private readonly StartupViewHost _startupHost;

    private readonly IRuntimeConfigService _runtimeConfig;
    private readonly IMidiEngine _midiEngine;
    private readonly IWacomServiceController _wacomServiceController;
    private readonly IWintabInputBackend _wintabInputBackend;
    private readonly IInputBackendStatusFormatter _inputBackendStatusFormatter;
    private readonly IUiStateProjectionService _uiStateProjectionService;
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
    /// Application entry constructs the window via <see cref="Composition.AppBootstrapper.CreateApplicationShell"/>.
    /// </summary>
    internal MainWindow(
        IRuntimeConfigService runtimeConfig,
        IMidiEngine midiEngine,
        IWacomServiceController wacomServiceController,
        IWintabInputBackend wintabInputBackend,
        IZoneOverlayRenderer zoneOverlayRenderer,
        IInputBackendStatusFormatter inputBackendStatusFormatter,
        IUiStateProjectionService uiStateProjectionService,
        IWacomServiceWorkflowCoordinator wacomServiceWorkflowCoordinator,
        IStartupLifecycleService startupLifecycle,
        IPadInputControllerFactory padInputControllerFactory,
        IZoneResizeService zoneResizeService,
        IPreferencesDialogService preferencesDialogService,
        IZoneConfigurationService zoneConfigurationService,
        ISettingsWorkflowCoordinator settingsWorkflowCoordinator,
        IPadPresentationCoordinator padPresentationCoordinator,
        IMainViewModelFactory mainViewModelFactory,
        IPadInteractionOutcomeApplier interactionOutcomeApplier)
    {
        _runtimeConfig = runtimeConfig ?? throw new ArgumentNullException(nameof(runtimeConfig));
        _midiEngine = midiEngine ?? throw new ArgumentNullException(nameof(midiEngine));
        _wacomServiceController = wacomServiceController ?? throw new ArgumentNullException(nameof(wacomServiceController));
        _wintabInputBackend = wintabInputBackend ?? throw new ArgumentNullException(nameof(wintabInputBackend));
        ArgumentNullException.ThrowIfNull(zoneOverlayRenderer);
        _inputBackendStatusFormatter = inputBackendStatusFormatter ?? throw new ArgumentNullException(nameof(inputBackendStatusFormatter));
        _uiStateProjectionService = uiStateProjectionService ?? throw new ArgumentNullException(nameof(uiStateProjectionService));
        ArgumentNullException.ThrowIfNull(wacomServiceWorkflowCoordinator);
        _startupLifecycle = startupLifecycle ?? throw new ArgumentNullException(nameof(startupLifecycle));
        ArgumentNullException.ThrowIfNull(padInputControllerFactory);
        ArgumentNullException.ThrowIfNull(zoneResizeService);
        ArgumentNullException.ThrowIfNull(preferencesDialogService);
        ArgumentNullException.ThrowIfNull(zoneConfigurationService);
        ArgumentNullException.ThrowIfNull(settingsWorkflowCoordinator);
        ArgumentNullException.ThrowIfNull(padPresentationCoordinator);
        ArgumentNullException.ThrowIfNull(mainViewModelFactory);
        _interactionOutcomeApplier = interactionOutcomeApplier ?? throw new ArgumentNullException(nameof(interactionOutcomeApplier));
        InitializeComponent();
        var padInputHost = new PadInputViewHost(this);
        _padInputSession = padInputControllerFactory.Create(padInputHost);
        _zonePadSession = new ZonePadController(new ZonePadViewHost(this), zoneResizeService);
        ISettingsWorkflow settingsWorkflow = new SettingsWorkflow(
            new SettingsWorkflowViewHost(this),
            settingsWorkflowCoordinator,
            preferencesDialogService,
            zoneConfigurationService,
            runtimeConfig,
            midiEngine,
            _padInputSession);
        _startupHost = new StartupViewHost(this);
        var shellCommandController = new MainShellCommandController(
            new MainShellCommandHost(this),
            settingsWorkflow,
            wacomServiceWorkflowCoordinator,
            _wacomServiceController,
            _uiStateProjectionService,
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
        if (IsPromotedStylusMouseEvent(e))
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

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
        if (IsPromotedStylusMouseEvent(e))
        {
            return;
        }

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
        if (IsPromotedStylusMouseEvent(e))
        {
            return;
        }

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

    private void TabletDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
        WacomServiceMenuState menuState = _uiStateProjectionService.BuildWacomServiceMenuState(status);
        _viewModel.ApplyWacomServiceMenuState(menuState);
    }

    private void LoadConfigIntoUi()
    {
        _viewModel.ZoneEditor.LoadConfigIntoUi();
        SyncToolbar();
        RefreshPadZoneOverlay();
    }

    private void UpdateUiForCurrentConfig()
    {
        ConnectedState statusState = _uiStateProjectionService.BuildConnectedState(Config);
        _viewModel.ApplyConnectedState(statusState);
        UpdateInputBackendStatus();
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
