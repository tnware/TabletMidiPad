namespace TabletMidiPad.Core.Interaction;

/// <summary>
/// Represents a single MIDI message the interaction engine wants to emit.
/// </summary>
public sealed record MidiMessage(
    MidiMessageKind Kind,
    int Channel,
    int Number,
    int Value,
    bool Force)
{
    /// <summary>
    /// Creates a MIDI CC (control change) message.
    /// </summary>
    public static MidiMessage Cc(int channel, int cc, int value, bool force = false)
        => new(MidiMessageKind.ControlChange, channel, cc, value, force);

    /// <summary>
    /// Creates a MIDI note on/off message.
    /// </summary>
    public static MidiMessage Note(int channel, int note, bool on, int velocity, bool force = false)
        => new(MidiMessageKind.Note, channel, note, on ? velocity : 0, force);
}

/// <summary>
/// The kind of MIDI message.
/// </summary>
public enum MidiMessageKind
{
    /// <summary>
    /// Control change (CC) message.
    /// </summary>
    ControlChange = 0,

    /// <summary>
    /// Note on/off message.
    /// </summary>
    Note = 1
}

