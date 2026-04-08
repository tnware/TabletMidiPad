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
    /// Validates the current in-memory config and persists it to disk (Apply Settings flow).
    /// </summary>
    WorkflowSaveResult ApplySettingsAndPersist(AppConfig config, IConfigStore store);

    /// <summary>
    /// Persists the current config to disk after in-memory mutations (e.g. preferences apply).
    /// </summary>
    WorkflowSaveResult PersistConfigToDisk(AppConfig config, IConfigStore store);

    /// <summary>
    /// Applies preferences result values into config and returns follow-up action decisions.
    /// </summary>
    PreferencesWorkflowResult ApplyPreferences(
        AppConfig config,
        PreferencesDialogResult result,
        IntPtr midiOutHandle);

    /// <summary>
    /// Builds Wacom service status menu state for current service status.
    /// </summary>
    WacomServiceMenuState BuildWacomServiceMenuState(WacomServiceStatus status);
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

/// <summary>
/// Wacom service menu state projected for MainWindow controls.
/// </summary>
internal readonly record struct WacomServiceMenuState(
    string Header,
    bool StartEnabled);
