using Xunit;

namespace TabletMidiPad.Tests;

public sealed class ConfigValueParserTests
{
    private readonly ConfigValueParser _sut = new();

    [Fact]
    public void RequirePressureCurveAcceptsSupportedNames()
    {
        Assert.Equal("Linear", _sut.RequirePressureCurve("Linear"));
        Assert.Equal("Exponential", _sut.RequirePressureCurve(" Exponential "));
    }

    [Fact]
    public void RequirePressureCurveRejectsUnknown()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => _sut.RequirePressureCurve("Spline"));
        Assert.Contains("Pressure Curve", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RequireMidiUniqueIdentifierAcceptsAlphanumericValuesUpTo32Characters()
    {
        const string identifier = "0123456789abcdef0123456789abcdef";

        Assert.Equal(identifier, _sut.RequireMidiUniqueIdentifier(identifier, "Loopback Unique Identifier"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("has spaces")]
    [InlineData("0123456789abcdef0123456789abcdefg")]
    public void RequireMidiUniqueIdentifierRejectsInvalidValues(string identifier)
    {
        Assert.Throws<InvalidOperationException>(() =>
            _sut.RequireMidiUniqueIdentifier(identifier, "Loopback Unique Identifier"));
    }

    [Fact]
    public void NormalizeZoneOutputModeFallsBackToCcPad()
    {
        Assert.Equal(ZoneOutputModes.OneNote, _sut.NormalizeZoneOutputMode("One Note"));
        Assert.Equal(ZoneOutputModes.ScaleBreath, _sut.NormalizeZoneOutputMode("Scale Breath"));
        Assert.Equal(ZoneOutputModes.ScaleGrid, _sut.NormalizeZoneOutputMode("Scale Grid"));
        Assert.Equal(ZoneOutputModes.CcPad, _sut.NormalizeZoneOutputMode("Unknown"));
    }

    [Fact]
    public void NormalizeScaleBreathScaleFallsBackToMajor()
    {
        Assert.Equal(ScaleBreathScaleNames.NaturalMinor, _sut.NormalizeScaleBreathScale("Natural Minor"));
        Assert.Equal(ScaleBreathScaleNames.Major, _sut.NormalizeScaleBreathScale("X"));
    }

    [Fact]
    public void NormalizeZoneMappingAssistTargetFallsBackToCcX()
    {
        Assert.Equal(ZoneMappingAssistTargets.CcPressure, _sut.NormalizeZoneMappingAssistTarget("CC Pressure"));
        Assert.Equal(ZoneMappingAssistTargets.CcX, _sut.NormalizeZoneMappingAssistTarget("X"));
    }
}
