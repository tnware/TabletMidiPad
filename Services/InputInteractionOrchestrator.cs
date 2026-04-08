using System.Collections.Generic;
using TabletMidiPad.Core.Interaction;

namespace TabletMidiPad.Services;

/// <summary>
/// Orchestrates interaction-engine invocation for pad input samples.
/// </summary>
internal sealed class InputInteractionOrchestrator : IInputInteractionOrchestrator
{
    private readonly IPadInteractionEngine _padInteractionEngine;

    /// <summary>
    /// Initializes a new instance of the <see cref="InputInteractionOrchestrator"/> class.
    /// </summary>
    public InputInteractionOrchestrator(IPadInteractionEngine padInteractionEngine)
    {
        _padInteractionEngine = padInteractionEngine ?? throw new System.ArgumentNullException(nameof(padInteractionEngine));
    }

    /// <inheritdoc />
    public InteractionResult ProcessTick(
        PadInteractionConfig config,
        IReadOnlyList<ZoneSnapshot> zones,
        InteractionState state,
        InteractionInput input)
    {
        return _padInteractionEngine.Process(config, zones, state, input);
    }

    /// <inheritdoc />
    public InteractionResult ProcessRelease(
        PadInteractionConfig config,
        IReadOnlyList<ZoneSnapshot> zones,
        InteractionState state,
        uint lastPenButtons)
    {
        return _padInteractionEngine.Process(
            config,
            zones,
            state,
            new InteractionInput(
                XNorm: 0,
                YNorm: 0,
                Pressure: 0,
                IsDown: false,
                IsHovering: false,
                PenButtons: lastPenButtons,
                Backend: InteractionBackend.Unknown));
    }
}
