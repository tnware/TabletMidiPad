namespace TabletMidiPad.Services.UiProjection;

/// <summary>
/// Default projection service for shell, toolbar, and connected-state labels.
/// </summary>
internal sealed class UiStateProjectionService : IUiStateProjectionService
{
    /// <inheritdoc />
    public WacomServiceMenuState BuildWacomServiceMenuState(WacomServiceStatus status)
    {
        return new WacomServiceMenuState(
            Header: status.StatusText,
            StartEnabled: status.CanStart);
    }

    /// <inheritdoc />
    public ToolbarState BuildToolbarState(AppConfig config)
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
        return new ToolbarState(
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
    public ConnectedState BuildConnectedState(AppConfig config, int activePageIndex, int pageCount)
    {
        string switchMode = config.AllowZoneSwitchWhilePressed ? "latch on enter" : "lock zone until release";
        int safePageCount = Math.Max(1, pageCount);
        int safePageIndex = Math.Clamp(activePageIndex, 0, safePageCount - 1);
        return new ConnectedState(
            StatusText: "Connected",
            RoutingText: $"Send to '{config.DawPortName}' via '{config.AppPortName}'",
            ActiveZoneText: "Active zone: none",
            ConfigSummaryText: $"{safePageIndex + 1}/{safePageCount} page(s) active | {switchMode}",
            ValueText: "X: -, Y: -, Pressure: -",
            PenButtonsText: "Pen buttons: -",
            ModifiersText: "Modifiers: none");
    }
}
