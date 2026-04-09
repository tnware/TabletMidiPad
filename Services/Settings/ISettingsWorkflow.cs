namespace TabletMidiPad.Services;

/// <summary>
/// Sequences settings/preferences/reset flows using workflow coordinators and coordinates follow-up UI refresh and tablet rebind.
/// </summary>
internal interface ISettingsWorkflow
{
    /// <summary>
    /// Validates, persists current settings, and refreshes status UI (Apply Settings).
    /// </summary>
    void ApplySettings();

    /// <summary>
    /// Preferences dialog, apply, persist, MIDI restart or UI refresh, optional input rebind, toolbar sync.
    /// </summary>
    void RunPreferencesFlow();

    /// <summary>
    /// Reset config to defaults, auto-tile zones, reload UI, then <see cref="ApplySettings"/>.
    /// </summary>
    void ResetToDefaultsAndApply();
}
