using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using TabletMidiPad.Core.Interaction;
using TabletMidiPad.Services;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class MainWindowPadPresentationCoordinatorTests
{
    private readonly MainWindowPadPresentationCoordinator _sut = new();

    [Fact]
    public void ComputeCursorForInteractionTick_WhenNotHoveringOrDown_DoesNotApply()
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
    public void ComputeCursorForInteractionTick_WhenDown_AppliesPositionAndOpacity()
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
    public void ComputeCursorAfterRelease_CollapsesAndSetsOpacity()
    {
        CursorTickChrome chrome = _sut.ComputeCursorAfterRelease();

        Assert.True(chrome.ShouldApply);
        Assert.Equal(Visibility.Collapsed, chrome.Visibility);
        Assert.Equal(0.85, chrome.Opacity);
    }

    [Fact]
    public void BuildZoneOverlayRequest_MapsZonesAndLastPoints()
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
        var theme = new MainWindowPadOverlayTheme(
            ZonePalette: new[] { Colors.Red, Colors.Blue },
            ColorWindow: Colors.Black,
            TextPrimaryBrush: Brushes.White,
            TextMutedBrush: Brushes.Gray,
            TextSoftBrush: Brushes.DarkGray,
            DangerBrush: Brushes.Red,
            AccentBrushHover: Brushes.White,
            SurfaceBrush: Brushes.Black);

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
