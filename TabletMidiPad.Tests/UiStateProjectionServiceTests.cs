using Xunit;

namespace TabletMidiPad.Tests;

public sealed class UiStateProjectionServiceTests
{
    [Fact]
    public void BuildWacomServiceMenuStateMapsFieldsDirectly()
    {
        var projection = new UiStateProjectionService();

        WacomServiceMenuState menu = projection.BuildWacomServiceMenuState(
            new WacomServiceStatus("Service: running", CanStart: false, IsServiceRunning: true));

        Assert.Equal("Service: running", menu.Header);
        Assert.False(menu.StartEnabled);
    }

    [Fact]
    public void BuildToolbarStateMapsAutoInputAndCurveLabels()
    {
        var projection = new UiStateProjectionService();
        var config = new AppConfig
        {
            InputBackendMode = "Auto",
            PressureCurve = "Logarithmic"
        };

        ToolbarState state = projection.BuildToolbarState(config);

        Assert.Equal("Pressure (Log)", state.ToolbarPressureLabelText);
        Assert.Equal("Logarithmic", state.SelectedPressureCurve);
        Assert.Equal("Input (Auto)", state.ToolbarInputBackendLabelText);
        Assert.Equal("Auto", state.SelectedInputBackendMode);
        Assert.False(state.IsPressureCurveLinearChecked);
        Assert.True(state.IsPressureCurveLogarithmicChecked);
        Assert.False(state.IsPressureCurveExponentialChecked);
        Assert.True(state.IsInputBackendAutoChecked);
        Assert.False(state.IsInputBackendWintabChecked);
        Assert.False(state.IsInputBackendMouseChecked);
    }

    [Fact]
    public void BuildToolbarStateNormalizesInputBackendAliasesAndMapsCheckmarks()
    {
        var projection = new UiStateProjectionService();
        var config = new AppConfig
        {
            InputBackendMode = "Wintab",
            PressureCurve = "Exponential",
        };

        ToolbarState state = projection.BuildToolbarState(config);

        Assert.Equal("Input (Wintab)", state.ToolbarInputBackendLabelText);
        Assert.Equal("Wintab", state.SelectedInputBackendMode);
        Assert.True(state.IsInputBackendWintabChecked);
        Assert.False(state.IsInputBackendAutoChecked);
        Assert.True(state.IsPressureCurveExponentialChecked);
    }

    [Fact]
    public void BuildConnectedStatePreservesSummaryAndBaselineTexts()
    {
        var projection = new UiStateProjectionService();
        var config = new AppConfig
        {
            DawPortName = "DAW X",
            AppPortName = "APP X",
            MaxZoneRows = 3,
            MaxZonesPerRow = 4,
            AllowZoneSwitchWhilePressed = false
        };
        config.Zones.Add(ZoneConfig.CreateDefault());

        ConnectedState state = projection.BuildConnectedState(config);

        Assert.Equal("Connected", state.StatusText);
        Assert.Equal("Send to 'DAW X' via 'APP X'", state.RoutingText);
        Assert.Contains("lock zone until release", state.ConfigSummaryText);
        Assert.Equal("Active zone: none", state.ActiveZoneText);
        Assert.Equal("X: -, Y: -, Pressure: -", state.ValueText);
        Assert.Equal("Pen buttons: -", state.PenButtonsText);
        Assert.Equal("Modifiers: none", state.ModifiersText);
    }
}
