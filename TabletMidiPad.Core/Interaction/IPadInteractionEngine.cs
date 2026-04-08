using System.Collections.Generic;

namespace TabletMidiPad.Core.Interaction;

/// <summary>
/// Processes pad input samples into MIDI messages and UI-friendly labels.
/// </summary>
public interface IPadInteractionEngine
{
    /// <summary>
    /// Processes one input sample and updates the provided <paramref name="state"/> in place.
    /// </summary>
    /// <param name="config">The configuration snapshot.</param>
    /// <param name="zones">The zone snapshot list.</param>
    /// <param name="state">Mutable interaction state (active zone, smoothing caches, toggles).</param>
    /// <param name="input">The input sample for this tick.</param>
    /// <returns>The computed result for UI and MIDI output.</returns>
    InteractionResult Process(
        PadInteractionConfig config,
        IReadOnlyList<ZoneSnapshot> zones,
        InteractionState state,
        InteractionInput input);
}

