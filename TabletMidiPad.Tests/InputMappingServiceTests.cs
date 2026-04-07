using TabletMidiPad.Services;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class InputMappingServiceTests
{
    private readonly InputMappingService _service = new();

    [Theory]
    [InlineData(0.0, false, 0)]
    [InlineData(1.0, false, 127)]
    [InlineData(0.5, false, 64)]
    [InlineData(-1.0, false, 0)]
    [InlineData(2.0, false, 127)]
    [InlineData(0.0, true, 127)]
    [InlineData(1.0, true, 0)]
    public void ToCc_HandlesEdgeCases(double value, bool invert, int expected)
    {
        Assert.Equal(expected, _service.ToCc(value, invert));
    }

    [Theory]
    [InlineData(64, "Linear", 64)]
    [InlineData(64, "Logarithmic", 90)]
    [InlineData(64, "Exponential", 32)]
    [InlineData(64, null, 64)]
    public void ApplyPressureCurve_UsesExpectedCurve(int pressure, string? curve, int expected)
    {
        Assert.Equal(expected, _service.ApplyPressureCurve(pressure, curve));
    }

    [Theory]
    [InlineData(0.24, 0.1, 0.2)]
    [InlineData(0.25, 0.1, 0.2)]
    [InlineData(0.26, 0.1, 0.3)]
    public void Snap_ReturnsSnappedValue(double input, double increment, double expected)
    {
        Assert.Equal(expected, _service.Snap(input, increment), 3);
    }

    [Theory]
    [InlineData("Wintab", "Wintab Only")]
    [InlineData("Mouse", "Stylus/Mouse Only")]
    [InlineData("Wintab Only", "Wintab Only")]
    [InlineData("Stylus/Mouse Only", "Stylus/Mouse Only")]
    [InlineData("", "Auto")]
    [InlineData(null, "Auto")]
    public void NormalizeInputBackendMode_HandlesAllBranches(string? input, string expected)
    {
        Assert.Equal(expected, _service.NormalizeInputBackendMode(input));
    }

    [Theory]
    [InlineData("Hold Last Values", ZoneReleaseBehaviors.Hold)]
    [InlineData(ZoneReleaseBehaviors.Hold, ZoneReleaseBehaviors.Hold)]
    [InlineData(ZoneReleaseBehaviors.ResetAll, ZoneReleaseBehaviors.ResetAll)]
    [InlineData(ZoneReleaseBehaviors.NoSendOnRelease, ZoneReleaseBehaviors.NoSendOnRelease)]
    [InlineData(null, ZoneReleaseBehaviors.ResetPressureOnly)]
    public void NormalizeReleaseBehavior_HandlesAllBranches(string? input, string expected)
    {
        Assert.Equal(expected, _service.NormalizeReleaseBehavior(input));
    }

    [Theory]
    [InlineData(PenButtonActions.None)]
    [InlineData(PenButtonActions.LockXY)]
    [InlineData(PenButtonActions.LockX)]
    [InlineData(PenButtonActions.LockY)]
    [InlineData(PenButtonActions.DisablePressure)]
    [InlineData(PenButtonActions.EnablePressure)]
    [InlineData(PenButtonActions.CcMomentary)]
    [InlineData(PenButtonActions.CcToggle)]
    [InlineData(PenButtonActions.NoteMomentary)]
    [InlineData(PenButtonActions.NoteToggle)]
    public void NormalizePenButtonAction_KnownValues_RoundTrips(string input)
    {
        Assert.Equal(input, _service.NormalizePenButtonAction(input));
    }

    [Fact]
    public void NormalizePenButtonAction_Unknown_DefaultsToNone()
    {
        Assert.Equal(PenButtonActions.None, _service.NormalizePenButtonAction("unknown"));
    }
}
