using System.Windows.Input;

namespace TabletMidiPad.Services;

/// <summary>
/// Pad/tablet input session: rebind, reset, Wintab bridge, and stylus/mouse fallback routing for the main window.
/// </summary>
internal interface IMainWindowPadInputSession
{
    void RebindInputBackend();

    void ResetActiveInputState();

    /// <summary>
    /// Shuts down the tablet backend using <see cref="IMainWindowLifecycleCoordinator.ShutdownTabletInput"/> and syncs view state.
    /// </summary>
    void ShutdownTabletInput();

    void InitializeTabletInput();

    /// <summary>
    /// Refreshes the tablet device picker list (combo subscription bracketing) without initializing Wintab.
    /// </summary>
    void RefreshTabletDeviceListOnly();

    /// <summary>
    /// Calls <see cref="IMainWindowInputBackendCoordinator.Shutdown"/> on the backend only (no lifecycle DTO).
    /// </summary>
    void ShutdownWintabBackendOnly();

    void ApplyTabletLifecycle(InputBackendLifecycleResult lifecycle, bool refreshInputBackendStatusText);

    void HandleWintabPacket(WintabPacketEventArgs packet);

    void HandleExpressKeyPacket(WintabExpressKeyEventArgs expressKey);

    void HandleProximityChanged(WintabProximityEventArgs e);

    void OnWintabDiagnosticsChanged(string diagnostics);

    void HandleStylusDown(StylusDownEventArgs e);

    void HandleStylusMove(StylusEventArgs e);

    void HandleStylusUp(StylusEventArgs e);

    /// <summary>
    /// Mouse-down on the pad after zone selection / resize handling (interaction-only path).
    /// </summary>
    void HandlePadMouseDownForInteraction(MouseButtonEventArgs e);

    void HandlePadMouseMoveForInteraction(MouseEventArgs e);

    void HandlePadMouseUpForInteraction(MouseButtonEventArgs e);

    void HandleRelease();
}
