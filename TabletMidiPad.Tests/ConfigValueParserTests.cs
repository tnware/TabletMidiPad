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
}
