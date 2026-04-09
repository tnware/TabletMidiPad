namespace TabletMidiPad.Services;

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
