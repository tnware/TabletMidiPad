using TabletMidiPad.Core.Interaction;

namespace TabletMidiPad.Services.Input;

/// <summary>
/// Orchestrates pad interaction engine invocation for view input events.
/// </summary>
internal interface IInputInteractionOrchestrator
{
    /// <summary>
    /// Builds the pad interaction configuration snapshot from persisted settings.
    /// </summary>
    PadInteractionConfig BuildPadInteractionConfig(AppConfig config);

    /// <summary>
    /// Builds zone snapshots for the interaction engine from persisted zone definitions.
    /// </summary>
    IReadOnlyList<ZoneSnapshot> BuildZoneSnapshots(PageConfig page);

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
