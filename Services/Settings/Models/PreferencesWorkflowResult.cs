namespace TabletMidiPad.Services.Settings.Models;

/// <summary>
/// Result of applying preferences and deciding follow-up workflow actions.
/// </summary>
internal readonly record struct PreferencesWorkflowResult(
    bool SaveSucceeded,
    string? ErrorMessage,
    bool ShouldRestartMidi,
    bool ShouldRebindInputBackend);
