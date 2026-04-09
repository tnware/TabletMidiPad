using System;

namespace TabletMidiPad.Services;

/// <summary>
/// Coordinates main-window workflow decisions for settings and preferences flows.
/// </summary>
internal interface IWorkflowCoordinator
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

    /// <summary>
    /// Builds Wacom service status menu state for current service status.
    /// </summary>
    WacomServiceMenuState BuildWacomServiceMenuState(WacomServiceStatus status);

    /// <summary>
    /// Builds toolbar labels and selected mode values from configuration.
    /// </summary>
    ToolbarState BuildToolbarState(AppConfig config);

    /// <summary>
    /// Builds base connected-state text payload for the main view-model.
    /// </summary>
    ConnectedState BuildConnectedState(AppConfig config);
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

/// <summary>
/// Toolbar label + selection state mapped from configuration.
/// </summary>
internal readonly record struct ToolbarState(
    string ToolbarPressureLabelText,
    string SelectedPressureCurve,
    string ToolbarInputBackendLabelText,
    string SelectedInputBackendMode,
    bool IsPressureCurveLinearChecked,
    bool IsPressureCurveLogarithmicChecked,
    bool IsPressureCurveExponentialChecked,
    bool IsInputBackendAutoChecked,
    bool IsInputBackendWintabChecked,
    bool IsInputBackendMouseChecked);

/// <summary>
/// Main connected-state status labels mapped from configuration.
/// </summary>
internal readonly record struct ConnectedState(
    string StatusText,
    string RoutingText,
    string ActiveZoneText,
    string ConfigSummaryText,
    string ValueText,
    string PenButtonsText,
    string ModifiersText);
