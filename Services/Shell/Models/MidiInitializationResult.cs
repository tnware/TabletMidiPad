namespace TabletMidiPad.Services.Shell.Models;

/// <summary>
/// Outcome of async MIDI initialization during startup.
/// </summary>
internal readonly record struct MidiInitializationResult(bool Succeeded, string? ErrorMessage);
