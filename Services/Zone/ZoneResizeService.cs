using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace TabletMidiPad.Services.Zone;

/// <summary>
/// Encapsulates zone hit-testing and resize calculations for editor interactions.
/// </summary>
internal sealed class ZoneResizeService : IZoneResizeService
{
    private const double ResizeHitMargin = 8.0;
    private const double ZoneSelectionInset = 0.02;
    private const double SnapStep = 0.05;
    private const double MinSize = 0.05;

    /// <inheritdoc />
    public int HitTestZoneBody(IReadOnlyList<ZoneConfig> zones, double padWidth, double padHeight, Point positionPx)
    {
        if (padWidth <= 0 || padHeight <= 0)
        {
            return -1;
        }

        for (int i = 0; i < zones.Count; i++)
        {
            ZoneConfig zone = zones[i];
            double insetX = Math.Min(ZoneSelectionInset, Math.Max(0.0, (zone.Width / 2.0) - 0.001));
            double insetY = Math.Min(ZoneSelectionInset, Math.Max(0.0, (zone.Height / 2.0) - 0.001));
            double zl = (zone.Left + insetX) * padWidth;
            double zt = (zone.Top + insetY) * padHeight;
            double zr = ((zone.Left + zone.Width) - insetX) * padWidth;
            double zb = ((zone.Top + zone.Height) - insetY) * padHeight;

            if (positionPx.X >= zl && positionPx.X <= zr && positionPx.Y >= zt && positionPx.Y <= zb)
            {
                return i;
            }
        }

        return -1;
    }

    /// <inheritdoc />
    public (int zoneIndex, ZoneResizeEdge edge) HitTestZoneEdge(
        IReadOnlyList<ZoneConfig> zones,
        int selectedZoneIndex,
        double padWidth,
        double padHeight,
        Point positionPx)
    {
        if (padWidth <= 0 || padHeight <= 0)
        {
            return (-1, ZoneResizeEdge.None);
        }

        if (selectedZoneIndex < 0 || selectedZoneIndex >= zones.Count)
        {
            return (-1, ZoneResizeEdge.None);
        }

        ZoneConfig zone = zones[selectedZoneIndex];
        double zl = zone.Left * padWidth;
        double zt = zone.Top * padHeight;
        double zr = (zone.Left + zone.Width) * padWidth;
        double zb = (zone.Top + zone.Height) * padHeight;

        if (positionPx.X < zl - ResizeHitMargin || positionPx.X > zr + ResizeHitMargin ||
            positionPx.Y < zt - ResizeHitMargin || positionPx.Y > zb + ResizeHitMargin)
        {
            return (-1, ZoneResizeEdge.None);
        }

        ZoneResizeEdge edge = ZoneResizeEdge.None;
        if (Math.Abs(positionPx.X - zl) <= ResizeHitMargin)
        {
            edge |= ZoneResizeEdge.Left;
        }
        else if (Math.Abs(positionPx.X - zr) <= ResizeHitMargin)
        {
            edge |= ZoneResizeEdge.Right;
        }

        if (Math.Abs(positionPx.Y - zt) <= ResizeHitMargin)
        {
            edge |= ZoneResizeEdge.Top;
        }
        else if (Math.Abs(positionPx.Y - zb) <= ResizeHitMargin)
        {
            edge |= ZoneResizeEdge.Bottom;
        }

        return edge != ZoneResizeEdge.None
            ? (selectedZoneIndex, edge)
            : (-1, ZoneResizeEdge.None);
    }

    /// <inheritdoc />
    public ZoneResizeSession? StartResizeSession(
        IReadOnlyList<ZoneConfig> zones,
        int zoneIndex,
        ZoneResizeEdge edge,
        Point dragStartPx)
    {
        if (zoneIndex < 0 || zoneIndex >= zones.Count || edge == ZoneResizeEdge.None)
        {
            return null;
        }

        ZoneConfig zone = zones[zoneIndex];
        return new ZoneResizeSession(
            ZoneIndex: zoneIndex,
            Edge: edge,
            DragStartPx: dragStartPx,
            OriginalLeft: zone.Left,
            OriginalTop: zone.Top,
            OriginalWidth: zone.Width,
            OriginalHeight: zone.Height);
    }

    /// <inheritdoc />
    public ZoneResizeUpdate? ComputeResizeUpdate(
        IReadOnlyList<ZoneConfig> zones,
        ZoneResizeSession session,
        double padWidth,
        double padHeight,
        Point currentPx)
    {
        if (session.ZoneIndex < 0 || session.ZoneIndex >= zones.Count)
        {
            return null;
        }

        if (padWidth <= 0 || padHeight <= 0)
        {
            return null;
        }

        double dxNorm = (currentPx.X - session.DragStartPx.X) / padWidth;
        double dyNorm = (currentPx.Y - session.DragStartPx.Y) / padHeight;

        double newLeft = session.OriginalLeft;
        double newTop = session.OriginalTop;
        double newWidth = session.OriginalWidth;
        double newHeight = session.OriginalHeight;

        if ((session.Edge & ZoneResizeEdge.Left) != 0)
        {
            newLeft = Snap(Math.Clamp(session.OriginalLeft + dxNorm, 0.0, session.OriginalLeft + session.OriginalWidth - MinSize), SnapStep);
            newWidth = (session.OriginalLeft + session.OriginalWidth) - newLeft;
        }
        else if ((session.Edge & ZoneResizeEdge.Right) != 0)
        {
            double newRight = Snap(Math.Clamp(session.OriginalLeft + session.OriginalWidth + dxNorm, session.OriginalLeft + MinSize, 1.0), SnapStep);
            newWidth = newRight - session.OriginalLeft;
        }

        if ((session.Edge & ZoneResizeEdge.Top) != 0)
        {
            newTop = Snap(Math.Clamp(session.OriginalTop + dyNorm, 0.0, session.OriginalTop + session.OriginalHeight - MinSize), SnapStep);
            newHeight = (session.OriginalTop + session.OriginalHeight) - newTop;
        }
        else if ((session.Edge & ZoneResizeEdge.Bottom) != 0)
        {
            double newBottom = Snap(Math.Clamp(session.OriginalTop + session.OriginalHeight + dyNorm, session.OriginalTop + MinSize, 1.0), SnapStep);
            newHeight = newBottom - session.OriginalTop;
        }

        newLeft = Math.Clamp(newLeft, 0.0, 1.0);
        newTop = Math.Clamp(newTop, 0.0, 1.0);
        newWidth = Snap(Math.Clamp(newWidth, MinSize, 1.0 - newLeft), SnapStep);
        newHeight = Snap(Math.Clamp(newHeight, MinSize, 1.0 - newTop), SnapStep);

        if (newLeft + newWidth > 1.0)
        {
            newWidth = 1.0 - newLeft;
        }

        if (newTop + newHeight > 1.0)
        {
            newHeight = 1.0 - newTop;
        }

        if (!CanResizeZoneTo(zones, session.ZoneIndex, newLeft, newTop, newWidth, newHeight))
        {
            return null;
        }

        return new ZoneResizeUpdate(
            ZoneIndex: session.ZoneIndex,
            Left: newLeft,
            Top: newTop,
            Width: newWidth,
            Height: newHeight);
    }

    /// <inheritdoc />
    public bool CanResizeZoneTo(IReadOnlyList<ZoneConfig> zones, int zoneIndex, double left, double top, double width, double height)
    {
        if (width < MinSize || height < MinSize)
        {
            return false;
        }

        if (left < 0.0 || top < 0.0 || left + width > 1.0 || top + height > 1.0)
        {
            return false;
        }

        foreach ((ZoneConfig zone, int index) other in zones.Select((zone, index) => (zone, index)))
        {
            if (other.index == zoneIndex)
            {
                continue;
            }

            if (ZonesOverlap(left, top, width, height, other.zone.Left, other.zone.Top, other.zone.Width, other.zone.Height))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ZonesOverlap(
        double leftA,
        double topA,
        double widthA,
        double heightA,
        double leftB,
        double topB,
        double widthB,
        double heightB)
    {
        double rightA = leftA + widthA;
        double bottomA = topA + heightA;
        double rightB = leftB + widthB;
        double bottomB = topB + heightB;

        return leftA < rightB &&
               rightA > leftB &&
               topA < bottomB &&
               bottomA > topB;
    }

    private static double Snap(double value, double increment)
    {
        return Math.Round(value / increment) * increment;
    }
}
