using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using TabletMidiPad.Core.Interaction;

namespace TabletMidiPad.Services;

/// <summary>
/// Orchestrates interaction-engine invocation for stylus/mouse/wintab inputs.
/// </summary>
internal sealed class InputInteractionOrchestrator : IInputInteractionOrchestrator
{
    private readonly IPadInteractionEngine _padInteractionEngine;
    private readonly IInputFallbackService _inputFallbackService;

    /// <summary>
    /// Initializes a new instance of the <see cref="InputInteractionOrchestrator"/> class.
    /// </summary>
    public InputInteractionOrchestrator(
        IPadInteractionEngine padInteractionEngine,
        IInputFallbackService inputFallbackService)
    {
        _padInteractionEngine = padInteractionEngine ?? throw new System.ArgumentNullException(nameof(padInteractionEngine));
        _inputFallbackService = inputFallbackService ?? throw new System.ArgumentNullException(nameof(inputFallbackService));
    }

    /// <inheritdoc />
    public bool TryBuildStylusInteraction(
        StylusEventArgs e,
        IInputElement relativeTo,
        double padWidth,
        double padHeight,
        int mousePressureFallback,
        out InteractionTickSample sample)
    {
        if (!_inputFallbackService.TryBuildStylusSample(
            e,
            relativeTo,
            padWidth,
            padHeight,
            mousePressureFallback,
            out InputFallbackSample fallbackSample))
        {
            sample = default;
            return false;
        }

        sample = new InteractionTickSample(
            PixelPosition: fallbackSample.PixelPosition,
            Input: new InteractionInput(
                XNorm: fallbackSample.XNorm,
                YNorm: fallbackSample.YNorm,
                Pressure: fallbackSample.Pressure127,
                IsDown: true,
                IsHovering: false,
                PenButtons: 0,
                Backend: InteractionBackend.StylusFallback));
        return true;
    }

    /// <inheritdoc />
    public bool TryBuildMouseInteraction(
        MouseEventArgs e,
        IInputElement relativeTo,
        double padWidth,
        double padHeight,
        int mousePressureFallback,
        out InteractionTickSample sample)
    {
        if (!_inputFallbackService.TryBuildMouseSample(
            e,
            relativeTo,
            padWidth,
            padHeight,
            mousePressureFallback,
            out InputFallbackSample fallbackSample))
        {
            sample = default;
            return false;
        }

        sample = new InteractionTickSample(
            PixelPosition: fallbackSample.PixelPosition,
            Input: new InteractionInput(
                XNorm: fallbackSample.XNorm,
                YNorm: fallbackSample.YNorm,
                Pressure: fallbackSample.Pressure127,
                IsDown: true,
                IsHovering: false,
                PenButtons: 0,
                Backend: InteractionBackend.MouseFallback));
        return true;
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
