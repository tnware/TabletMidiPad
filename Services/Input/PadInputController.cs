using System.Windows;
using System.Windows.Input;
using TabletMidiPad.Core.Interaction;

namespace TabletMidiPad.Services.Input;

/// <summary>
/// Default pad/tablet session for <see cref="Views.MainWindow"/>.
/// </summary>
internal sealed class PadInputController : IPadInputController
{
    private readonly IPadInputControllerHost _host;
    private readonly ITabletDevicePickerSyncService _tabletDevicePickerSyncService;
    private readonly IInputBackendCoordinator _inputBackendCoordinator;
    private readonly IWintabInputBackend _wintabInputBackend;
    private readonly IWintabInputBridge _wintabInputBridge;
    private readonly IInputFallbackService _inputFallbackService;
    private readonly IInputInteractionOrchestrator _inputInteractionOrchestrator;
    private readonly IInputBackendStatusFormatter _inputBackendStatusFormatter;
    private readonly IPageNavigationService _pageNavigationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PadInputController"/> class.
    /// </summary>
    public PadInputController(
        IPadInputControllerHost host,
        ITabletDevicePickerSyncService tabletDevicePickerSyncService,
        IInputBackendCoordinator inputBackendCoordinator,
        IWintabInputBackend wintabInputBackend,
        IWintabInputBridge wintabInputBridge,
        IInputFallbackService inputFallbackService,
        IInputInteractionOrchestrator inputInteractionOrchestrator,
        IInputBackendStatusFormatter inputBackendStatusFormatter,
        IPageNavigationService pageNavigationService)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _tabletDevicePickerSyncService = tabletDevicePickerSyncService ?? throw new ArgumentNullException(nameof(tabletDevicePickerSyncService));
        _inputBackendCoordinator = inputBackendCoordinator ?? throw new ArgumentNullException(nameof(inputBackendCoordinator));
        _wintabInputBackend = wintabInputBackend ?? throw new ArgumentNullException(nameof(wintabInputBackend));
        _wintabInputBridge = wintabInputBridge ?? throw new ArgumentNullException(nameof(wintabInputBridge));
        _inputFallbackService = inputFallbackService ?? throw new ArgumentNullException(nameof(inputFallbackService));
        _inputInteractionOrchestrator = inputInteractionOrchestrator ?? throw new ArgumentNullException(nameof(inputInteractionOrchestrator));
        _inputBackendStatusFormatter = inputBackendStatusFormatter ?? throw new ArgumentNullException(nameof(inputBackendStatusFormatter));
        _pageNavigationService = pageNavigationService ?? throw new ArgumentNullException(nameof(pageNavigationService));
    }

    private PadInputControllerState S => _host.PadState;

    /// <inheritdoc />
    public void ApplyTabletLifecycle(InputBackendLifecycleResult lifecycle, bool refreshInputBackendStatusText)
    {
        S.UseWintab = lifecycle.UseWintab;
        S.ExpressKeyCount = lifecycle.ExpressKeyCount;
        S.LastInputBackend = lifecycle.LastInputBackend;
        S.WintabDiagnostics = lifecycle.WintabDiagnostics;
        if (refreshInputBackendStatusText)
        {
            _host.UpdateInputBackendStatus();
        }
    }

    /// <inheritdoc />
    public void RefreshTabletDeviceListOnly()
    {
        _host.BeginPopulateTabletDeviceList();
        try
        {
            int previousDeviceIndex = _host.ViewModel.SelectedTabletDevice?.DeviceIndex ?? -1;
            _tabletDevicePickerSyncService.Refresh(_host.ViewModel, previousDeviceIndex);
        }
        finally
        {
            _host.EndPopulateTabletDeviceList();
        }
    }

    /// <inheritdoc />
    public void ShutdownWintabBackendOnly()
    {
        _inputBackendCoordinator.Shutdown(_wintabInputBackend);
    }

    /// <inheritdoc />
    public void InitializeTabletInput()
    {
        RefreshTabletDeviceListOnly();

        int tabletDeviceIndex = _host.ViewModel.SelectedTabletDevice?.DeviceIndex >= 0
            ? _host.ViewModel.SelectedTabletDevice.DeviceIndex
            : -1;
        InputBackendLifecycleResult lifecycle = _inputBackendCoordinator.Initialize(
            _host.Config.InputBackendMode,
            _wintabInputBackend,
            tabletDeviceIndex,
            S.ExpressKeyCount);
        ApplyTabletLifecycle(lifecycle, refreshInputBackendStatusText: true);
    }

    /// <inheritdoc />
    public void ShutdownTabletInput()
    {
        InputBackendLifecycleResult result = _inputBackendCoordinator.Shutdown(_wintabInputBackend);
        ApplyTabletLifecycle(result, refreshInputBackendStatusText: false);
    }

    /// <inheritdoc />
    public void RebindInputBackend()
    {
        ResetActiveInputState();
        ShutdownTabletInput();
        InitializeTabletInput();
        _host.OnRebindCompletedRefreshRouting();
    }

    /// <inheritdoc />
    public void ResetActiveInputState()
    {
        InputBackendResetResult resetState = _inputBackendCoordinator.BuildResetState();
        S.IsActive = resetState.IsActive;
        _host.PadChrome.ApplyCursorChrome(_host.PadChrome.ComputeCursorAfterRelease());
        _host.ViewModel.ActiveZoneText = resetState.ActiveZoneText;
        _host.ViewModel.ValueText = resetState.ValueText;
        _host.ViewModel.PenButtonsText = resetState.PenButtonsText;
        _host.ViewModel.ModifiersText = resetState.ModifiersText;
        HandleRelease();
        _host.Pad.ReleaseMouseCapture();
        _host.Pad.ReleaseStylusCapture();
    }

    /// <inheritdoc />
    public void HandleWintabPacket(WintabPacketEventArgs packet)
    {
        if (!_wintabInputBridge.TryBuildPacketBridge(
            packet,
            _host.Pad.ActualWidth,
            _host.Pad.ActualHeight,
            S.IsActive,
            out WintabPacketBridgeOutcome bridge))
        {
            return;
        }

        MarkInputBackend(bridge.BackendLabel);
        UpdatePenButtonsDebug(bridge.PenButtons);

        S.WintabDiagnostics = bridge.PacketDiagnostics;
        _host.UpdateInputBackendStatus();

        WintabPacketFlowResult flow = bridge.Flow;
        if (flow.ReleaseBeforeTickIfWasActive)
        {
            S.IsActive = false;
            HandleRelease();
        }

        S.IsActive = flow.IsActiveAfter;
        ProcessInteractionTick(flow.TickInput, flow.PixelPosition, flow.CursorOpacity);
    }

    /// <inheritdoc />
    public void HandleExpressKeyPacket(WintabExpressKeyEventArgs expressKey)
    {
        WintabExpressKeyBridgeOutcome outcome = _wintabInputBridge.BuildExpressKeyBridge(expressKey, _host.Config.ExpressKeys);
        S.ExpressKeyCount = Math.Max(S.ExpressKeyCount, outcome.RequiredExpressKeyCount);

        string action = ResolveExpressKeyAction(expressKey.Control);
        if (expressKey.IsPressed && TryResolvePageSwitchTarget(action, out int targetPageIndex))
        {
            ApplyPageSwitch(targetPageIndex);
        }
        else
        {
            _host.ApplyInteractionResult(_inputInteractionOrchestrator.ProcessTick(
                _inputInteractionOrchestrator.BuildPadInteractionConfig(_host.Config),
                BuildActiveZoneSnapshots(),
                _host.InteractionState,
                outcome.EngineInput));
        }

        S.ExpressKeyDiagnostics = outcome.ExpressKeyDiagnostics;
        MarkInputBackend(outcome.BackendLabel);
        _host.UpdateInputBackendStatus();
    }

    /// <inheritdoc />
    public void HandleProximityChanged(WintabProximityEventArgs e)
    {
        S.WintabDiagnostics = e.Diagnostics;
        _host.UpdateInputBackendStatus();
        if (!e.Entering)
        {
            if (_inputBackendCoordinator.ShouldReleaseOnProximityLeave(S.IsActive))
            {
                S.IsActive = false;
                HandleRelease();
            }

            _host.PadChrome.ApplyCursorChrome(_host.PadChrome.ComputeCursorAfterRelease());
            _host.ViewModel.ActiveZoneText = "Active zone: none";
        }
    }

    /// <inheritdoc />
    public void OnWintabDiagnosticsChanged(string diagnostics)
    {
        S.WintabDiagnostics = diagnostics;
        _host.UpdateInputBackendStatus();
    }

    /// <inheritdoc />
    public void HandleStylusDown(StylusDownEventArgs e)
    {
        if (_inputBackendCoordinator.IsStylusMouseFallbackSuppressed(S.UseWintab, _host.Config.InputBackendMode))
        {
            return;
        }

        e.Handled = true;
        S.IsActive = true;
        _host.Pad.CaptureStylus();
        HandleStylus(e);
    }

    /// <inheritdoc />
    public void HandleStylusMove(StylusEventArgs e)
    {
        if (_inputBackendCoordinator.IsStylusMouseFallbackSuppressed(S.UseWintab, _host.Config.InputBackendMode))
        {
            return;
        }

        if (!S.IsActive)
        {
            return;
        }

        e.Handled = true;
        HandleStylus(e);
    }

    /// <inheritdoc />
    public void HandleStylusUp(StylusEventArgs e)
    {
        if (_inputBackendCoordinator.IsStylusMouseFallbackSuppressed(S.UseWintab, _host.Config.InputBackendMode))
        {
            return;
        }

        if (!S.IsActive)
        {
            return;
        }

        e.Handled = true;
        HandleStylus(e);
        S.IsActive = false;
        _host.Pad.ReleaseStylusCapture();
        _host.PadChrome.ApplyCursorChrome(_host.PadChrome.ComputeCursorAfterRelease());
        HandleRelease();
    }

    /// <inheritdoc />
    public void HandlePadMouseDownForInteraction(MouseButtonEventArgs e)
    {
        if (IsPromotedStylusMouseEvent(e))
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (_inputBackendCoordinator.IsStylusMouseFallbackSuppressed(S.UseWintab, _host.Config.InputBackendMode))
        {
            return;
        }

        S.IsActive = true;
        _host.Pad.CaptureMouse();
        HandleMouse(e);
    }

    /// <inheritdoc />
    public void HandlePadMouseMoveForInteraction(MouseEventArgs e)
    {
        if (IsPromotedStylusMouseEvent(e))
        {
            return;
        }

        if (_inputBackendCoordinator.IsStylusMouseFallbackSuppressed(S.UseWintab, _host.Config.InputBackendMode))
        {
            return;
        }

        if (!S.IsActive || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        HandleMouse(e);
    }

    /// <inheritdoc />
    public void HandlePadMouseUpForInteraction(MouseButtonEventArgs e)
    {
        if (IsPromotedStylusMouseEvent(e))
        {
            return;
        }

        if (_inputBackendCoordinator.IsStylusMouseFallbackSuppressed(S.UseWintab, _host.Config.InputBackendMode))
        {
            return;
        }

        if (!S.IsActive)
        {
            return;
        }

        HandleMouse(e);
        S.IsActive = false;
        _host.Pad.ReleaseMouseCapture();
        _host.PadChrome.ApplyCursorChrome(_host.PadChrome.ComputeCursorAfterRelease());
        HandleRelease();
    }

    /// <inheritdoc />
    public void HandleRelease()
    {
        InteractionResult releaseResult = _inputInteractionOrchestrator.ProcessRelease(
            _inputInteractionOrchestrator.BuildPadInteractionConfig(_host.Config),
            BuildActiveZoneSnapshots(),
            _host.InteractionState,
            S.LastWintabButtons);
        _host.ApplyInteractionResult(releaseResult);

        PageSelectionResult? deferred = _pageNavigationService.ConsumeDeferred(_host.Config);
        if (deferred is PageSelectionResult deferredResult && deferredResult.Changed)
        {
            SyncPageSelectionToViewModel(deferredResult.ActivePageIndex);
            _host.OnRebindCompletedRefreshRouting();
        }

        _host.RefreshPadZoneOverlay();

        _host.PadChrome.ApplyCursorChrome(_host.PadChrome.ComputeCursorAfterRelease());
    }

    private void ProcessInteractionTick(InteractionInput input, Point pixelPosition, double? cursorOpacityOverride = null)
    {
        _host.PadChrome.ApplyCursorChrome(_host.PadChrome.ComputeCursorForInteractionTick(
            input,
            pixelPosition,
            cursorOpacityOverride));

        InteractionResult result = _inputInteractionOrchestrator.ProcessTick(
            _inputInteractionOrchestrator.BuildPadInteractionConfig(_host.Config),
            BuildActiveZoneSnapshots(),
            _host.InteractionState,
            input);

        _host.ApplyInteractionResult(result);

        _host.RefreshPadZoneOverlay();
    }

    private void HandleStylus(StylusEventArgs e)
    {
        MarkInputBackend("Stylus");
        _host.ViewModel.PenButtonsText = "Pen buttons: stylus fallback";
        if (!_inputFallbackService.TryBuildStylusSample(
            e,
            _host.Pad,
            _host.Pad.ActualWidth,
            _host.Pad.ActualHeight,
            _host.Config.MousePressureValue,
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
        _host.ViewModel.PenButtonsText = "Pen buttons: mouse fallback";
        if (!_inputFallbackService.TryBuildMouseSample(
            e,
            _host.Pad,
            _host.Pad.ActualWidth,
            _host.Pad.ActualHeight,
            _host.Config.MousePressureValue,
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

    private void MarkInputBackend(string backend)
    {
        S.LastInputBackend = backend;
        _host.UpdateInputBackendStatus();
    }

    private void UpdatePenButtonsDebug(uint buttons)
    {
        S.LastWintabButtons = buttons;
        _host.ViewModel.PenButtonsText = _inputBackendStatusFormatter.BuildPenButtonsText(buttons);
    }

    private static bool IsPromotedStylusMouseEvent(MouseEventArgs e) => e.StylusDevice is not null;

    private IReadOnlyList<ZoneSnapshot> BuildActiveZoneSnapshots()
        => _inputInteractionOrchestrator.BuildZoneSnapshots(_pageNavigationService.GetActivePage(_host.Config));

    private string ResolveExpressKeyAction(int control)
    {
        if (control < 0 || control >= _host.Config.ExpressKeys.Count)
        {
            return ExpressKeyActions.None;
        }

        string value = (_host.Config.ExpressKeys[control].Action ?? string.Empty).Trim();
        return value switch
        {
            ExpressKeyActions.None => value,
            ExpressKeyActions.CcMomentary => value,
            ExpressKeyActions.CcToggle => value,
            ExpressKeyActions.NoteMomentary => value,
            ExpressKeyActions.NoteToggle => value,
            ExpressKeyActions.PrevPage => value,
            ExpressKeyActions.NextPage => value,
            ExpressKeyActions.GoToPage1 => value,
            ExpressKeyActions.GoToPage2 => value,
            ExpressKeyActions.GoToPage3 => value,
            ExpressKeyActions.GoToPage4 => value,
            ExpressKeyActions.GoToPage5 => value,
            ExpressKeyActions.GoToPage6 => value,
            ExpressKeyActions.GoToPage7 => value,
            ExpressKeyActions.GoToPage8 => value,
            _ => ExpressKeyActions.None
        };
    }

    private bool TryResolvePageSwitchTarget(string action, out int targetPageIndex)
    {
        targetPageIndex = _pageNavigationService.ActivePageIndex;
        return action switch
        {
            ExpressKeyActions.PrevPage => ResolveRelativePage(-1, out targetPageIndex),
            ExpressKeyActions.NextPage => ResolveRelativePage(1, out targetPageIndex),
            ExpressKeyActions.GoToPage1 => ResolveDirectPage(0, out targetPageIndex),
            ExpressKeyActions.GoToPage2 => ResolveDirectPage(1, out targetPageIndex),
            ExpressKeyActions.GoToPage3 => ResolveDirectPage(2, out targetPageIndex),
            ExpressKeyActions.GoToPage4 => ResolveDirectPage(3, out targetPageIndex),
            ExpressKeyActions.GoToPage5 => ResolveDirectPage(4, out targetPageIndex),
            ExpressKeyActions.GoToPage6 => ResolveDirectPage(5, out targetPageIndex),
            ExpressKeyActions.GoToPage7 => ResolveDirectPage(6, out targetPageIndex),
            ExpressKeyActions.GoToPage8 => ResolveDirectPage(7, out targetPageIndex),
            _ => false
        };
    }

    private bool ResolveRelativePage(int offset, out int targetPageIndex)
    {
        int count = _host.Config.Pages.Count;
        if (count <= 0)
        {
            targetPageIndex = 0;
            return false;
        }

        int current = _pageNavigationService.ActivePageIndex;
        int target = current + offset;
        if (target < 0)
        {
            target = _host.Config.WrapPageNavigation ? count - 1 : current;
        }
        else if (target >= count)
        {
            target = _host.Config.WrapPageNavigation ? 0 : current;
        }

        targetPageIndex = target;
        return target != current;
    }

    private bool ResolveDirectPage(int directIndex, out int targetPageIndex)
    {
        targetPageIndex = directIndex;
        if (targetPageIndex < 0 || targetPageIndex >= _host.Config.Pages.Count)
        {
            return false;
        }

        return targetPageIndex != _pageNavigationService.ActivePageIndex;
    }

    private void ApplyPageSwitch(int targetPageIndex)
    {
        if (!_host.Config.AllowPageSwitchWhilePressed && S.IsActive)
        {
            _pageNavigationService.TryQueueDeferred(_host.Config, targetPageIndex);
            return;
        }

        if (S.IsActive && _host.Config.AllowPageSwitchWhilePressed)
        {
            HandleRelease();
        }

        PageSelectionResult result = _pageNavigationService.Select(_host.Config, targetPageIndex);
        if (!result.Changed)
        {
            return;
        }

        SyncPageSelectionToViewModel(result.ActivePageIndex);
        _host.OnRebindCompletedRefreshRouting();
        _host.RefreshPadZoneOverlay();
    }

    private void SyncPageSelectionToViewModel(int activePageIndex)
    {
        var zoneEditor = _host.ViewModel.ZoneEditor;
        zoneEditor.ApplyActivePageFromWorkflow(activePageIndex, resetZoneSelection: true);
    }
}
