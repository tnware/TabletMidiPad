using System.Collections.Generic;

namespace TabletMidiPad.Core.Overlay;

/// <summary>
/// Calculates pixel-space overlay layout for zones and markers.
/// </summary>
public static class ZoneOverlayLayoutCalculator
{
    /// <summary>
    /// Builds layout entries for each zone based on normalized zone bounds and pad size.
    /// </summary>
    /// <param name="zones">Normalized zone definitions.</param>
    /// <param name="padWidth">Pad width in pixels.</param>
    /// <param name="padHeight">Pad height in pixels.</param>
    /// <param name="lastLocalByZoneIndex">
    /// Optional local marker points keyed by zone index (values normalized in [0,1]).
    /// </param>
    /// <returns>Computed per-zone overlay layout entries.</returns>
    public static IReadOnlyList<ZoneOverlayLayoutEntry> BuildLayout(
        IReadOnlyList<ZoneOverlayZoneSnapshot> zones,
        double padWidth,
        double padHeight,
        IReadOnlyDictionary<int, (double x, double y)> lastLocalByZoneIndex)
    {
        var entries = new List<ZoneOverlayLayoutEntry>(zones.Count);
        if (padWidth <= 0 || padHeight <= 0)
        {
            return entries;
        }

        for (int i = 0; i < zones.Count; i++)
        {
            ZoneOverlayZoneSnapshot zone = zones[i];
            double leftPx = zone.Left * padWidth;
            double topPx = zone.Top * padHeight;
            double widthPx = zone.Width * padWidth;
            double heightPx = zone.Height * padHeight;

            (double x, double y)? markerCenter = null;
            if (lastLocalByZoneIndex.TryGetValue(i, out (double x, double y) local))
            {
                double markerX = zone.SendX ? local.x : 0.5;
                double markerY = zone.SendY ? local.y : 0.5;
                markerCenter = (
                    leftPx + (markerX * widthPx),
                    topPx + (markerY * heightPx));
            }

            entries.Add(new ZoneOverlayLayoutEntry(
                ZoneIndex: i,
                LeftPx: leftPx,
                TopPx: topPx,
                WidthPx: widthPx,
                HeightPx: heightPx,
                MarkerCenterPx: markerCenter));
        }

        return entries;
    }
}

/// <summary>
/// Zone data needed to compute overlay geometry.
/// </summary>
/// <param name="SendX">Whether X is active for marker position.</param>
/// <param name="SendY">Whether Y is active for marker position.</param>
/// <param name="Left">Normalized left bound in [0,1].</param>
/// <param name="Top">Normalized top bound in [0,1].</param>
/// <param name="Width">Normalized width in [0,1].</param>
/// <param name="Height">Normalized height in [0,1].</param>
public sealed record ZoneOverlayZoneSnapshot(
    bool SendX,
    bool SendY,
    double Left,
    double Top,
    double Width,
    double Height);

/// <summary>
/// Pixel-space layout for one zone in the overlay.
/// </summary>
/// <param name="ZoneIndex">Original zone index in the input list.</param>
/// <param name="LeftPx">Left coordinate in pixels.</param>
/// <param name="TopPx">Top coordinate in pixels.</param>
/// <param name="WidthPx">Width in pixels.</param>
/// <param name="HeightPx">Height in pixels.</param>
/// <param name="MarkerCenterPx">Optional marker center position in pixels.</param>
public sealed record ZoneOverlayLayoutEntry(
    int ZoneIndex,
    double LeftPx,
    double TopPx,
    double WidthPx,
    double HeightPx,
    (double x, double y)? MarkerCenterPx);
