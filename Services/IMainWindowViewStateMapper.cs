namespace TabletMidiPad.Services;

/// <summary>
/// Maps application configuration and runtime state to main-window view-model text state.
/// </summary>
internal interface IMainWindowViewStateMapper
{
    /// <summary>
    /// Builds toolbar labels and selected mode values from configuration.
    /// </summary>
    MainWindowToolbarState BuildToolbarState(AppConfig config);

    /// <summary>
    /// Builds base connected-state text payload for the main view-model.
    /// </summary>
    MainWindowConnectedState BuildConnectedState(AppConfig config);
}

/// <summary>
/// Toolbar label + selection state mapped from configuration.
/// </summary>
internal readonly record struct MainWindowToolbarState(
    string ToolbarPressureLabelText,
    string SelectedPressureCurve,
    string ToolbarInputBackendLabelText,
    string SelectedInputBackendMode);

/// <summary>
/// Main connected-state status labels mapped from configuration.
/// </summary>
internal readonly record struct MainWindowConnectedState(
    string StatusText,
    string RoutingText,
    string ActiveZoneText,
    string ConfigSummaryText,
    string ValueText,
    string PenButtonsText,
    string ModifiersText);
