using System;

namespace TabletMidiPad.Services;

/// <summary>
/// Default mapper for deriving main-window view-model text and toolbar state from config.
/// </summary>
internal sealed class MainWindowViewStateMapper : IMainWindowViewStateMapper
{
    /// <inheritdoc />
    public MainWindowToolbarState BuildToolbarState(AppConfig config)
    {
        string normalizedInputMode = InputBackendModeNormalizer.Normalize(config.InputBackendMode);
        string curveShort = config.PressureCurve switch
        {
            "Logarithmic" => "Log",
            "Exponential" => "Exp",
            _ => "Lin"
        };

        string toolbarInputLabel = normalizedInputMode switch
        {
            "Wintab Only" => "Input (Wintab)",
            "Stylus/Mouse Only" => "Input (Mouse)",
            _ => "Input (Auto)"
        };

        string selectedInputMode = normalizedInputMode switch
        {
            "Wintab Only" => "Wintab",
            "Stylus/Mouse Only" => "Mouse",
            _ => "Auto"
        };

        string curve = config.PressureCurve;
        return new MainWindowToolbarState(
            ToolbarPressureLabelText: $"Pressure ({curveShort})",
            SelectedPressureCurve: curve,
            ToolbarInputBackendLabelText: toolbarInputLabel,
            SelectedInputBackendMode: selectedInputMode,
            IsPressureCurveLinearChecked: string.Equals(curve, "Linear", StringComparison.Ordinal),
            IsPressureCurveLogarithmicChecked: string.Equals(curve, "Logarithmic", StringComparison.Ordinal),
            IsPressureCurveExponentialChecked: string.Equals(curve, "Exponential", StringComparison.Ordinal),
            IsInputBackendAutoChecked: string.Equals(selectedInputMode, "Auto", StringComparison.Ordinal),
            IsInputBackendWintabChecked: string.Equals(selectedInputMode, "Wintab", StringComparison.Ordinal),
            IsInputBackendMouseChecked: string.Equals(selectedInputMode, "Mouse", StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public MainWindowConnectedState BuildConnectedState(AppConfig config)
    {
        string switchMode = config.AllowZoneSwitchWhilePressed ? "latch on enter" : "lock zone until release";
        return new MainWindowConnectedState(
            StatusText: "Connected",
            RoutingText: $"Send to '{config.DawPortName}' via '{config.AppPortName}'",
            ActiveZoneText: "Active zone: none",
            ConfigSummaryText: $"{config.Zones.Count} zone(s) configured | layout {config.MaxZoneRows} row(s) max / {config.MaxZonesPerRow} per row | {switchMode}",
            ValueText: "X: -, Y: -, Pressure: -",
            PenButtonsText: "Pen buttons: -",
            ModifiersText: "Modifiers: none");
    }

}
