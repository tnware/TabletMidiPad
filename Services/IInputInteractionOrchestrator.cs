using System.Collections.Generic;
using TabletMidiPad.Core.Interaction;

namespace TabletMidiPad.Services;

/// <summary>
/// Orchestrates pad interaction engine invocation for view input events.
/// </summary>
internal interface IInputInteractionOrchestrator
{
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
