using System.Collections.Generic;

namespace TabletMidiPad.Core.Interaction;

/// <summary>
/// Immutable snapshot of application-level configuration relevant to pad interaction processing.
/// </summary>
public sealed record PadInteractionConfig(
    bool SendPressureGlobally,
    bool AllowZoneSwitchWhilePressed,
    int MousePressureValue,
    double PositionDeadZone,
    double PositionSmoothing,
    string PressureCurve,
    string LowerPenButtonAction,
    int LowerPenButtonMidiChannel,
    int LowerPenButtonCc,
    int LowerPenButtonNote,
    string UpperPenButtonAction,
    int UpperPenButtonMidiChannel,
    int UpperPenButtonCc,
    int UpperPenButtonNote,
    IReadOnlyList<ExpressKeyConfigSnapshot> ExpressKeys);

/// <summary>
/// Immutable snapshot of express key configuration.
/// </summary>
public sealed record ExpressKeyConfigSnapshot(
    string Action,
    int MidiChannel,
    int Cc,
    int Note);

/// <summary>
/// Immutable snapshot of a configured interaction zone.
/// </summary>
public sealed record ZoneSnapshot(
    string Name,
    bool Enabled,
    int MidiChannel,
    int CcX,
    int CcY,
    int CcPressure,
    bool SendX,
    bool SendY,
    bool InvertX,
    bool InvertY,
    bool SendPressure,
    string ReleaseBehavior,
    double Left,
    double Top,
    double Width,
    double Height)
{
    public string OutputMode { get; init; } = InteractionConstants.ZoneOutputModes.CcPad;
    public int ScaleBreathRoot { get; init; }
    public string ScaleBreathScale { get; init; } = InteractionConstants.ScaleBreathScales.Major;
    public int ScaleBreathOctave { get; init; } = 4;
    public int ScaleBreathHoleCount { get; init; } = 10;
    public double ScaleBreathRowSplit { get; init; } = 0.5;
    public bool ScaleBreathSendCc { get; init; } = true;
    public int ScaleGridRoot { get; init; }
    public string ScaleGridScale { get; init; } = InteractionConstants.ScaleBreathScales.Major;
    public int ScaleGridOctave { get; init; } = 4;
    public int ScaleGridRows { get; init; } = 4;
    public int ScaleGridColumns { get; init; } = 4;
    public bool ScaleGridSendCc { get; init; } = true;
    public int OneNoteNote { get; init; } = 60;
    public bool OneNoteSendCc { get; init; }
    public bool MappingAssistEnabled { get; init; }
    public string MappingAssistTarget { get; init; } = InteractionConstants.ZoneMappingAssistTargets.CcX;
}

/// <summary>
/// Captures a single input sample for the interaction engine.
/// </summary>
public readonly record struct InteractionInput(
    double XNorm,
    double YNorm,
    int Pressure,
    bool IsDown,
    bool IsHovering,
    uint PenButtons,
    InteractionBackend Backend);

/// <summary>
/// Tracks ongoing interaction state between samples (active zone, smoothing, toggles).
/// </summary>
public sealed class InteractionState
{
    /// <summary>
    /// The index of the currently active zone. -1 means none.
    /// </summary>
    public int ActiveZoneIndex { get; set; } = -1;

    /// <summary>
    /// Whether a position lock modifier is currently holding a point.
    /// </summary>
    public bool HasLockedPosition { get; set; }

    /// <summary>
    /// Locked (local-to-zone) X coordinate when a lock modifier is active.
    /// </summary>
    public double LockedLocalX { get; set; }

    /// <summary>
    /// Locked (local-to-zone) Y coordinate when a lock modifier is active.
    /// </summary>
    public double LockedLocalY { get; set; }

    /// <summary>
    /// Cached per-zone smoothed local X/Y (keyed by zone index).
    /// </summary>
    public Dictionary<int, (double x, double y)> SmoothedLocalByZone { get; } = [];

    /// <summary>
    /// Last local X/Y (keyed by zone index).
    /// </summary>
    public Dictionary<int, (double x, double y)> LastLocalByZone { get; } = [];

    /// <summary>
    /// Pen button held state for the lower side button.
    /// </summary>
    public bool LowerPenButtonHeld { get; set; }

    /// <summary>
    /// Pen button held state for the upper side button.
    /// </summary>
    public bool UpperPenButtonHeld { get; set; }

    /// <summary>
    /// Toggle state for the lower pen button (for toggle actions).
    /// </summary>
    public bool LowerPenButtonToggle { get; set; }

    /// <summary>
    /// Toggle state for the upper pen button (for toggle actions).
    /// </summary>
    public bool UpperPenButtonToggle { get; set; }

    /// <summary>
    /// Express key held state by key index.
    /// </summary>
    public Dictionary<int, bool> ExpressKeyHeld { get; } = [];

    /// <summary>
    /// Express key toggle state by key index.
    /// </summary>
    public Dictionary<int, bool> ExpressKeyToggle { get; } = [];

    /// <summary>
    /// Whether Scale Breath currently has an active note.
    /// </summary>
    public bool HasScaleBreathActiveNote { get; set; }

    /// <summary>
    /// Active Scale Breath MIDI note value.
    /// </summary>
    public int ScaleBreathActiveNote { get; set; } = -1;

    /// <summary>
    /// Active Scale Breath MIDI channel.
    /// </summary>
    public int ScaleBreathActiveChannel { get; set; } = 1;

    /// <summary>
    /// Active Scale Breath zone index.
    /// </summary>
    public int ScaleBreathActiveZoneIndex { get; set; } = -1;
}

/// <summary>
/// Result of processing one input sample, including UI-friendly diagnostics and MIDI messages to emit.
/// </summary>
public sealed record InteractionResult(
    int ActiveZoneIndex,
    string ActiveZoneLabel,
    string ValueLabel,
    string ModifiersLabel,
    IReadOnlyList<MidiMessage> MidiMessages);

/// <summary>
/// Identifies which backend produced the sample (used for diagnostics/UI only).
/// </summary>
public enum InteractionBackend
{
    Unknown = 0,
    Wintab = 1,
    StylusFallback = 2,
    MouseFallback = 3,
    ExpressKey = 4
}

