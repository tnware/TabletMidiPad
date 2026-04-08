using System.Windows;
using TabletMidiPad.Core.Interaction;
using TabletMidiPad.Services;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class MainWindowInputEventFlowCoordinatorTests
{
    private readonly MainWindowInputEventFlowCoordinator _sut = new();

    [Theory]
    [InlineData(true, "Auto", true)]
    [InlineData(false, "Wintab Only", true)]
    [InlineData(false, "Auto", false)]
    [InlineData(false, "Wintab", true)]
    public void IsStylusMouseFallbackSuppressed_MatchesLegacyRule(bool useWintab, string mode, bool expected)
    {
        bool actual = _sut.IsStylusMouseFallbackSuppressed(useWintab, mode);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildWintabPacketFlow_HoverWhileActive_ReleasesBeforeTick()
    {
        var update = new WintabPacketUpdate(
            XPixel: 10,
            YPixel: 20,
            XNorm: 0.1,
            YNorm: 0.2,
            Pressure127: 0,
            IsHovering: true,
            PenButtons: 3,
            BackendLabel: "x",
            PacketDiagnostics: "d",
            CursorOpacity: 0.35,
            IsDown: false);

        WintabPacketFlowResult flow = _sut.BuildWintabPacketFlow(wasActive: true, update);

        Assert.True(flow.ReleaseBeforeTickIfWasActive);
        Assert.False(flow.IsActiveAfter);
        Assert.True(flow.TickInput.IsHovering);
        Assert.False(flow.TickInput.IsDown);
        Assert.Equal(0.35, flow.CursorOpacity);
        Assert.Equal(new Point(10, 20), flow.PixelPosition);
        Assert.Equal(InteractionBackend.Wintab, flow.TickInput.Backend);
    }

    [Fact]
    public void BuildWintabPacketFlow_Down_SetsActiveFromUpdate()
    {
        var update = new WintabPacketUpdate(
            XPixel: 0,
            YPixel: 0,
            XNorm: 0.5,
            YNorm: 0.5,
            Pressure127: 80,
            IsHovering: false,
            PenButtons: 0,
            BackendLabel: "x",
            PacketDiagnostics: "d",
            CursorOpacity: 0.85,
            IsDown: true);

        WintabPacketFlowResult flow = _sut.BuildWintabPacketFlow(wasActive: false, update);

        Assert.False(flow.ReleaseBeforeTickIfWasActive);
        Assert.True(flow.IsActiveAfter);
        Assert.True(flow.TickInput.IsDown);
        Assert.False(flow.TickInput.IsHovering);
        Assert.Equal(80, flow.TickInput.Pressure);
    }

    [Fact]
    public void ShouldReleaseOnProximityLeave_OnlyWhenActive()
    {
        Assert.True(_sut.ShouldReleaseOnProximityLeave(true));
        Assert.False(_sut.ShouldReleaseOnProximityLeave(false));
    }

    [Theory]
    [InlineData("Wintab", "Wintab Only")]
    [InlineData("Mouse", "Stylus/Mouse Only")]
    [InlineData("garbage", "Auto")]
    public void InputBackendModeNormalizer_Normalize_AlignsWithLegacy(string raw, string expected)
    {
        Assert.Equal(expected, InputBackendModeNormalizer.Normalize(raw));
    }
}
