using System;
using TabletMidiPad.Services;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class ConfigValueParserTests
{
    private readonly ConfigValueParser _sut = new();

    [Fact]
    public void RequirePressureCurve_AcceptsSupportedNames()
    {
        Assert.Equal("Linear", _sut.RequirePressureCurve("Linear"));
        Assert.Equal("Exponential", _sut.RequirePressureCurve(" Exponential "));
    }

    [Fact]
    public void RequirePressureCurve_RejectsUnknown()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => _sut.RequirePressureCurve("Spline"));
        Assert.Contains("Pressure Curve", ex.Message, StringComparison.Ordinal);
    }
}
