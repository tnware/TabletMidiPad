using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TabletMidiPad.Core.Interaction;
using TabletMidiPad.ViewModels;

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
        IInputBackendStatusFormatter inputBackendStatusFormatter)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _tabletDevicePickerSyncService = tabletDevicePickerSyncService ?? throw new ArgumentNullException(nameof(tabletDevicePickerSyncService));
        _inputBackendCoordinator = inputBackendCoordinator ?? throw new ArgumentNullException(nameof(inputBackendCoordinator));
        _wintabInputBackend = wintabInputBackend ?? throw new ArgumentNullException(nameof(wintabInputBackend));
        _wintabInputBridge = wintabInputBridge ?? throw new ArgumentNullException(nameof(wintabInputBridge));
        _inputFallbackService = inputFallbackService ?? throw new ArgumentNullException(nameof(inputFallbackService));
        _inputInteractionOrchestrator = inputInteractionOrchestrator ?? throw new ArgumentNullException(nameof(inputInteractionOrchestrator));
        _inputBackendStatusFormatter = inputBackendStatusFormatter ?? throw new ArgumentNullException(nameof(inputBackendStatusFormatter));
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
            ? _host.ViewModel.SelectedTabletDevice!.DeviceIndex
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
        _host.ApplyInteractionResult(_inputInteractionOrchestrator.ProcessTick(
            _inputInteractionOrchestrator.BuildPadInteractionConfig(_host.Config),
            _inputInteractionOrchestrator.BuildZoneSnapshots(_host.Config),
            _host.InteractionState,
            outcome.EngineInput));
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
            _inputInteractionOrchestrator.BuildZoneSnapshots(_host.Config),
            _host.InteractionState,
            S.LastWintabButtons);
        _host.ApplyInteractionResult(releaseResult);
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
            _inputInteractionOrchestrator.BuildZoneSnapshots(_host.Config),
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
}
