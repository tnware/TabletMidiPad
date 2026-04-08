using System;

namespace TabletMidiPad.Services;

/// <summary>
/// Default toolbar quick-settings mutations for the main window.
/// </summary>
internal sealed class MainWindowToolbarSession : IMainWindowToolbarSession
{
    /// <inheritdoc />
    public void ApplyPressureCurve(AppConfig config, string curve)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.PressureCurve = curve;
    }

    /// <inheritdoc />
    public ToolbarInputModeApplyResult TryApplyInputBackendMode(AppConfig config, string mode)
    {
        ArgumentNullException.ThrowIfNull(config);
        string normalized = InputBackendModeNormalizer.Normalize(mode);
        if (string.Equals(config.InputBackendMode, normalized, StringComparison.Ordinal))
        {
            return new ToolbarInputModeApplyResult(ModeChanged: false, RequiresInputRebind: false);
        }

        config.InputBackendMode = normalized;
        return new ToolbarInputModeApplyResult(ModeChanged: true, RequiresInputRebind: true);
    }

    /// <inheritdoc />
    public string GetNextCycledInputBackendModeLabel(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return InputBackendModeNormalizer.Normalize(config.InputBackendMode) switch
        {
            "Wintab Only" => "Mouse",
            "Stylus/Mouse Only" => "Auto",
            _ => "Wintab Only"
        };
    }
}
