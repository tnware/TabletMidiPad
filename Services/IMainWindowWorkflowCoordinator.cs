using System;

namespace TabletMidiPad.Services;

/// <summary>
/// Coordinates main-window workflow decisions for settings and preferences flows.
/// </summary>
internal interface IMainWindowWorkflowCoordinator
{
    /// <summary>
    /// Returns the effective express key count to use for preferences editing.
    /// </summary>
    int GetEffectiveExpressKeyCount(int detectedExpressKeyCount, int configuredExpressKeyCount);

    /// <summary>
    /// Validates and saves configuration settings.
    /// </summary>
    WorkflowSaveResult ValidateAndSave(AppConfig config);

    /// <summary>
    /// Applies preferences result values into config and returns follow-up action decisions.
    /// </summary>
    PreferencesWorkflowResult ApplyPreferences(
        AppConfig config,
        PreferencesDialogResult result,
        IntPtr midiOutHandle);
}

/// <summary>
/// Result of a validate/save workflow step.
/// </summary>
internal readonly record struct WorkflowSaveResult(bool Success, string? ErrorMessage);

/// <summary>
/// Result of applying preferences and deciding follow-up workflow actions.
/// </summary>
internal readonly record struct PreferencesWorkflowResult(
    bool SaveSucceeded,
    string? ErrorMessage,
    bool ShouldRestartMidi,
    bool ShouldRebindInputBackend);
