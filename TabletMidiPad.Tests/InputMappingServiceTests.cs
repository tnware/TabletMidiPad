using TabletMidiPad.Services;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class InputMappingServiceTests
{
    private readonly InputMappingService _service = new();

    [Fact]
    public void ToCc_InvertFalse_MapsZeroToZero()
    {
        Assert.Equal(0, _service.ToCc(0, invert: false));
    }

    [Fact]
    public void ToCc_InvertTrue_MapsZeroToMax()
    {
        Assert.Equal(127, _service.ToCc(0, invert: true));
    }
}
