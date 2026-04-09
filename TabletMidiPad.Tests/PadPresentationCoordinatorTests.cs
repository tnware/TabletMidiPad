using System.Windows;
using System.Windows.Media;
using TabletMidiPad.Core.Interaction;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class PadPresentationCoordinatorTests
{
    private readonly PadPresentationCoordinator _sut = new();

    [Fact]
    public void ComputeCursorForInteractionTickWhenNotHoveringOrDownDoesNotApply()
    {
        var input = new InteractionInput(0.5, 0.5, 0, false, false, 0, InteractionBackend.Wintab);

        CursorTickChrome chrome = _sut.ComputeCursorForInteractionTick(
            input,
            new Point(100, 50),
            cursorDotWidth: 10,
            cursorDotHeight: 10,
            opacityOverride: 0.5);

        Assert.False(chrome.ShouldApply);
    }

    [Fact]
    public void ComputeCursorForInteractionTickWhenDownAppliesPositionAndOpacity()
    {
        var input = new InteractionInput(0.5, 0.5, 64, true, false, 0, InteractionBackend.MouseFallback);

        CursorTickChrome chrome = _sut.ComputeCursorForInteractionTick(
            input,
            new Point(100, 50),
            cursorDotWidth: 10,
            cursorDotHeight: 10,
            opacityOverride: 0.42);

        Assert.True(chrome.ShouldApply);
        Assert.Equal(Visibility.Visible, chrome.Visibility);
        Assert.Equal(95, chrome.Left);
        Assert.Equal(45, chrome.Top);
        Assert.Equal(0.42, chrome.Opacity);
    }

    [Fact]
    public void ComputeCursorAfterReleaseCollapsesAndSetsOpacity()
    {
        CursorTickChrome chrome = _sut.ComputeCursorAfterRelease();

        Assert.True(chrome.ShouldApply);
        Assert.Equal(Visibility.Collapsed, chrome.Visibility);
        Assert.Equal(0.85, chrome.Opacity);
    }

    [Fact]
    public void BuildZoneOverlayRequestMapsZonesAndLastPoints()
    {
        var zones = new List<ZoneConfig>
        {
            new() { Name = "A", Left = 0, Top = 0, Width = 0.5, Height = 1 },
            new() { Name = "B", Left = 0.5, Top = 0, Width = 0.5, Height = 1 },
        };
        var lastPoints = new Dictionary<ZoneConfig, Point>
        {
            [zones[0]] = new Point(0.1, 0.2),
        };
        var theme = new PadOverlayTheme(
            ZonePalette: [Colors.Red, Colors.Blue],
            ColorWindow: Colors.Black,
            TextPrimaryBrush: Brushes.White,
            TextMutedBrush: Brushes.Gray,
            TextSoftBrush: Brushes.DarkGray,
            DangerBrush: Brushes.Red,
            AccentBrushHover: Brushes.White,
            SurfaceBrush: Brushes.Black,
            OverlayStyle: ZoneOverlayStyle.Plain);

        ZoneOverlayRenderRequest request = _sut.BuildZoneOverlayRequest(
            zones,
            lastPoints,
            selectedZoneIndex: 1,
            padWidth: 800,
            padHeight: 600,
            theme);

        Assert.Equal(800, request.PadWidth);
        Assert.Equal(600, request.PadHeight);
        Assert.Equal(2, request.Zones.Count);
        Assert.Equal(1, request.SelectedZoneIndex);
        Assert.Single(request.LastLocalByZoneIndex);
        Assert.True(request.LastLocalByZoneIndex.ContainsKey(0));
        Assert.Equal(2, request.ZonePalette.Count);
    }
}
