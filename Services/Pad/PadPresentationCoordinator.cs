using System.Windows;
using TabletMidiPad.Core.Interaction;

namespace TabletMidiPad.Services.Pad;

/// <summary>
/// Default pad presentation coordinator for <see cref="Views.PadInteractionSurface"/> (invoked by the main window).
/// </summary>
internal sealed class PadPresentationCoordinator : IPadPresentationCoordinator
{
    /// <inheritdoc />
    public CursorTickChrome ComputeCursorForInteractionTick(
        InteractionInput input,
        Point pixelPosition,
        double cursorDotWidth,
        double cursorDotHeight,
        double? opacityOverride)
    {
        if (!input.IsHovering && !input.IsDown)
        {
            return new CursorTickChrome(ShouldApply: false, default, 0, 0, null);
        }

        return new CursorTickChrome(
            ShouldApply: true,
            Visibility: Visibility.Visible,
            Left: pixelPosition.X - (cursorDotWidth / 2),
            Top: pixelPosition.Y - (cursorDotHeight / 2),
            Opacity: opacityOverride);
    }

    /// <inheritdoc />
    public CursorTickChrome ComputeCursorAfterRelease()
    {
        return new CursorTickChrome(
            ShouldApply: true,
            Visibility: Visibility.Collapsed,
            Left: 0,
            Top: 0,
            Opacity: 0.85);
    }

    /// <inheritdoc />
    public ZoneOverlayRenderRequest BuildZoneOverlayRequest(
        IReadOnlyList<ZoneConfig> zones,
        IReadOnlyDictionary<ZoneConfig, Point> lastZonePoints,
        int selectedZoneIndex,
        double padWidth,
        double padHeight,
        PadOverlayTheme theme)
    {
        IReadOnlyList<ZoneOverlayZone> overlayZones = [.. zones
            .Select(zone => new ZoneOverlayZone(
                Name: zone.Name,
                Enabled: zone.Enabled,
                SendX: zone.SendX,
                SendY: zone.SendY,
                SendPressure: zone.SendPressure,
                OutputMode: zone.OutputMode,
                ScaleBreathRoot: zone.ScaleBreathRoot,
                ScaleBreathScale: zone.ScaleBreathScale,
                ScaleBreathOctave: zone.ScaleBreathOctave,
                ScaleBreathHoleCount: zone.ScaleBreathHoleCount,
                ScaleBreathRowSplit: zone.ScaleBreathRowSplit,
                ScaleGridRoot: zone.ScaleGridRoot,
                ScaleGridScale: zone.ScaleGridScale,
                ScaleGridOctave: zone.ScaleGridOctave,
                ScaleGridRows: zone.ScaleGridRows,
                ScaleGridColumns: zone.ScaleGridColumns,
                Left: zone.Left,
                Top: zone.Top,
                Width: zone.Width,
                Height: zone.Height))];

        var lastByIndex = new Dictionary<int, (double X, double Y)>();
        for (int index = 0; index < zones.Count; index++)
        {
            ZoneConfig zone = zones[index];
            if (lastZonePoints.TryGetValue(zone, out Point last))
            {
                lastByIndex[index] = (last.X, last.Y);
            }
        }

        return new ZoneOverlayRenderRequest(
            PadWidth: padWidth,
            PadHeight: padHeight,
            Zones: overlayZones,
            SelectedZoneIndex: selectedZoneIndex,
            LastLocalByZoneIndex: lastByIndex,
            ZonePalette: theme.ZonePalette,
            ColorWindow: theme.ColorWindow,
            TextPrimaryBrush: theme.TextPrimaryBrush,
            TextMutedBrush: theme.TextMutedBrush,
            TextSoftBrush: theme.TextSoftBrush,
            DangerBrush: theme.DangerBrush,
            AccentBrushHover: theme.AccentBrushHover,
            SurfaceBrush: theme.SurfaceBrush,
            OverlayStyle: theme.OverlayStyle);
    }
}
