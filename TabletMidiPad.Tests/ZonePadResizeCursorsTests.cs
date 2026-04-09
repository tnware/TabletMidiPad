using Xunit;

namespace TabletMidiPad.Tests;

/// <summary>
/// Public entry points use primitive edge bits so xUnit can discover tests while <see cref="ZoneResizeEdge"/> stays internal.
/// </summary>
public sealed class ZonePadResizeCursorsTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(4, 2)]
    [InlineData(8, 2)]
    [InlineData(1 | 4, 3)]
    [InlineData(2 | 8, 3)]
    [InlineData(1 | 8, 4)]
    [InlineData(2 | 4, 4)]
    public void FromResizeEdgeMatchesMainWindowLegacyMapping(int edgeBits, int expectedCursorKind)
    {
        var edge = (ZoneResizeEdge)edgeBits;
        var expected = (ZonePadResizeCursorKind)expectedCursorKind;
        Assert.Equal(expected, ZonePadResizeCursors.FromResizeEdge(edge));
    }
}
