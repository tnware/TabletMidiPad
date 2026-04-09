using TabletMidiPad.Core.Interaction;

namespace TabletMidiPad.Services.Input;

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
        _padInteractionEngine = padInteractionEngine ?? throw new ArgumentNullException(nameof(padInteractionEngine));
    }

    /// <inheritdoc />
    public PadInteractionConfig BuildPadInteractionConfig(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new PadInteractionConfig(
            SendPressureGlobally: config.SendPressureGlobally,
            AllowZoneSwitchWhilePressed: config.AllowZoneSwitchWhilePressed,
            MousePressureValue: config.MousePressureValue,
            PositionDeadZone: config.PositionDeadZone,
            PositionSmoothing: config.PositionSmoothing,
            PressureCurve: config.PressureCurve,
            LowerPenButtonAction: config.LowerPenButtonAction,
            LowerPenButtonMidiChannel: config.LowerPenButtonMidiChannel,
            LowerPenButtonCc: config.LowerPenButtonCc,
            LowerPenButtonNote: config.LowerPenButtonNote,
            UpperPenButtonAction: config.UpperPenButtonAction,
            UpperPenButtonMidiChannel: config.UpperPenButtonMidiChannel,
            UpperPenButtonCc: config.UpperPenButtonCc,
            UpperPenButtonNote: config.UpperPenButtonNote,
            ExpressKeys: [.. config.ExpressKeys.Select(k => new ExpressKeyConfigSnapshot(k.Action, k.MidiChannel, k.Cc, k.Note))]);
    }

    /// <inheritdoc />
    public IReadOnlyList<ZoneSnapshot> BuildZoneSnapshots(PageConfig page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return [.. page.Zones
            .Select(z => new ZoneSnapshot(
                Name: z.Name,
                Enabled: z.Enabled,
                MidiChannel: z.MidiChannel,
                CcX: z.CcX,
                CcY: z.CcY,
                CcPressure: z.CcPressure,
                SendX: z.SendX,
                SendY: z.SendY,
                InvertX: z.InvertX,
                InvertY: z.InvertY,
                SendPressure: z.SendPressure,
                ReleaseBehavior: z.ReleaseBehavior,
                Left: z.Left,
                Top: z.Top,
                Width: z.Width,
                Height: z.Height)
            {
                OutputMode = z.OutputMode,
                ScaleBreathRoot = z.ScaleBreathRoot,
                ScaleBreathScale = z.ScaleBreathScale,
                ScaleBreathOctave = z.ScaleBreathOctave,
                ScaleBreathHoleCount = z.ScaleBreathHoleCount,
                ScaleBreathRowSplit = z.ScaleBreathRowSplit,
                ScaleBreathSendCc = z.ScaleBreathSendCc
            })];
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
