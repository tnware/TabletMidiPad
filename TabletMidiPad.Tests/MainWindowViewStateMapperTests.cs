using TabletMidiPad.Services;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class MainWindowViewStateMapperTests
{
    [Fact]
    public void BuildToolbarState_MapsAutoInputAndCurveLabels()
    {
        var mapper = new MainWindowViewStateMapper();
        var config = new AppConfig
        {
            InputBackendMode = "Auto",
            PressureCurve = "Logarithmic"
        };

        MainWindowToolbarState state = mapper.BuildToolbarState(config);

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
    public void BuildToolbarState_NormalizesInputBackendAliases_AndMapsCheckmarks()
    {
        var mapper = new MainWindowViewStateMapper();
        var config = new AppConfig
        {
            InputBackendMode = "Wintab",
            PressureCurve = "Exponential",
        };

        MainWindowToolbarState state = mapper.BuildToolbarState(config);

        Assert.Equal("Input (Wintab)", state.ToolbarInputBackendLabelText);
        Assert.Equal("Wintab", state.SelectedInputBackendMode);
        Assert.True(state.IsInputBackendWintabChecked);
        Assert.False(state.IsInputBackendAutoChecked);
        Assert.True(state.IsPressureCurveExponentialChecked);
    }

    [Fact]
    public void BuildConnectedState_PreservesSummaryAndBaselineTexts()
    {
        var mapper = new MainWindowViewStateMapper();
        var config = new AppConfig
        {
            DawPortName = "DAW X",
            AppPortName = "APP X",
            MaxZoneRows = 3,
            MaxZonesPerRow = 4,
            AllowZoneSwitchWhilePressed = false
        };
        config.Zones.Add(ZoneConfig.CreateDefault());

        MainWindowConnectedState state = mapper.BuildConnectedState(config);

        Assert.Equal("Connected", state.StatusText);
        Assert.Equal("Send to 'DAW X' via 'APP X'", state.RoutingText);
        Assert.Contains("lock zone until release", state.ConfigSummaryText);
        Assert.Equal("Active zone: none", state.ActiveZoneText);
        Assert.Equal("X: -, Y: -, Pressure: -", state.ValueText);
        Assert.Equal("Pen buttons: -", state.PenButtonsText);
        Assert.Equal("Modifiers: none", state.ModifiersText);
    }
}
