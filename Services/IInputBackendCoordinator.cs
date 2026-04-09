using System.Windows;
using TabletMidiPad.Core.Interaction;

namespace TabletMidiPad.Services;

/// <summary>
/// Coordinates main-window input backend lifecycle, rebind/reset orchestration, and Wintab vs fallback routing decisions.
/// </summary>
internal interface IInputBackendCoordinator
{
    /// <summary>
    /// Initializes backend state for the selected input mode/device.
    /// </summary>
    InputBackendLifecycleResult Initialize(
        string inputBackendMode,
        IWintabInputBackend backend,
        int selectedDeviceIndex,
        int currentExpressKeyCount);

    /// <summary>
    /// Shuts down the backend and returns resulting lifecycle state.
    /// </summary>
    InputBackendLifecycleResult Shutdown(IWintabInputBackend backend);

    /// <summary>
    /// Produces reset-state labels/flags before a backend rebind.
    /// </summary>
    InputBackendResetResult BuildResetState();

    /// <summary>
    /// Returns true when stylus/mouse fallback events should be ignored (Wintab active or Wintab-only mode).
    /// </summary>
    bool IsStylusMouseFallbackSuppressed(bool useWintab, string inputBackendMode);

    /// <summary>
    /// Builds Wintab packet follow-up: optional release-on-hover transition, next active flag, and tick payload.
    /// </summary>
    WintabPacketFlowResult BuildWintabPacketFlow(bool wasActive, WintabPacketUpdate update);

    /// <summary>
    /// When the pen leaves proximity while down, a release should be processed first.
    /// </summary>
    bool ShouldReleaseOnProximityLeave(bool isActive);
}

/// <summary>
/// Backend lifecycle state payload for main-window orchestration.
/// </summary>
internal readonly record struct InputBackendLifecycleResult(
    bool UseWintab,
    int ExpressKeyCount,
    string LastInputBackend,
    string WintabDiagnostics);

/// <summary>
/// Active input state reset payload for main-window orchestration.
/// </summary>
internal readonly record struct InputBackendResetResult(
    bool IsActive,
    string ActiveZoneText,
    string ValueText,
    string PenButtonsText,
    string ModifiersText);

/// <summary>
/// Result of interpreting one Wintab packet for main-window orchestration.
/// </summary>
internal readonly record struct WintabPacketFlowResult(
    bool ReleaseBeforeTickIfWasActive,
    bool IsActiveAfter,
    InteractionInput TickInput,
    Point PixelPosition,
    double CursorOpacity);
