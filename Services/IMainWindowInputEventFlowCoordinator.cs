using System.Windows;
using TabletMidiPad.Core.Interaction;

namespace TabletMidiPad.Services;

/// <summary>
/// Pure decisions for main-window input routing (Wintab vs fallback, hover vs down).
/// </summary>
internal interface IMainWindowInputEventFlowCoordinator
{
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
/// Result of interpreting one Wintab packet for main-window orchestration.
/// </summary>
internal readonly record struct WintabPacketFlowResult(
    bool ReleaseBeforeTickIfWasActive,
    bool IsActiveAfter,
    InteractionInput TickInput,
    Point PixelPosition,
    double CursorOpacity);
