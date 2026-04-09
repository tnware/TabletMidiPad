namespace TabletMidiPad.Services;

/// <summary>
/// Toolbar quick settings (pressure curve and input backend mode) applied to <see cref="AppConfig"/>.
/// </summary>
internal interface IToolbarConfigService
{
    /// <summary>
    /// Sets <see cref="AppConfig.PressureCurve"/> to the given value.
    /// </summary>
    void ApplyPressureCurve(AppConfig config, string curve);

    /// <summary>
    /// Normalizes and applies input backend mode; returns whether the mode changed (rebind may be required).
    /// </summary>
    ToolbarInputModeApplyResult TryApplyInputBackendMode(AppConfig config, string mode);

    /// <summary>
    /// Returns the next mode label to apply when cycling the toolbar control (not yet normalized into config).
    /// </summary>
    string GetNextCycledInputBackendModeLabel(AppConfig config);
}

/// <summary>
/// Outcome of applying a toolbar input-backend mode selection.
/// </summary>
internal readonly record struct ToolbarInputModeApplyResult(bool ModeChanged, bool RequiresInputRebind);
