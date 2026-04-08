using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using TabletMidiPad.Core.Interaction;

namespace TabletMidiPad.Services;

/// <summary>
/// Orchestrates interaction sample construction and engine processing for view input events.
/// </summary>
internal interface IInputInteractionOrchestrator
{
    /// <summary>
    /// Attempts to build a stylus-fallback interaction sample.
    /// </summary>
    bool TryBuildStylusInteraction(
        StylusEventArgs e,
        IInputElement relativeTo,
        double padWidth,
        double padHeight,
        int mousePressureFallback,
        out InteractionTickSample sample);

    /// <summary>
    /// Attempts to build a mouse-fallback interaction sample.
    /// </summary>
    bool TryBuildMouseInteraction(
        MouseEventArgs e,
        IInputElement relativeTo,
        double padWidth,
        double padHeight,
        int mousePressureFallback,
        out InteractionTickSample sample);

    /// <summary>
    /// Processes one interaction sample through the pad interaction engine.
    /// </summary>
    InteractionResult ProcessTick(
        PadInteractionConfig config,
        IReadOnlyList<ZoneSnapshot> zones,
        InteractionState state,
        InteractionInput input);

    /// <summary>
    /// Processes a release tick using the last known button state.
    /// </summary>
    InteractionResult ProcessRelease(
        PadInteractionConfig config,
        IReadOnlyList<ZoneSnapshot> zones,
        InteractionState state,
        uint lastPenButtons);
}

/// <summary>
/// Interaction input sample plus pixel position for cursor rendering.
/// </summary>
internal readonly record struct InteractionTickSample(
    Point PixelPosition,
    InteractionInput Input);
