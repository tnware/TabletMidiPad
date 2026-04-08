using System.Collections.Generic;
using System.Windows;

namespace TabletMidiPad.Services;

/// <summary>
/// Encapsulates zone hit-testing and drag-resize orchestration.
/// </summary>
internal interface IZoneResizeService
{
    /// <summary>
    /// Performs edge hit-testing for the currently selected zone.
    /// </summary>
    (int zoneIndex, ZoneResizeEdge edge) HitTestZoneEdge(
        IReadOnlyList<ZoneConfig> zones,
        int selectedZoneIndex,
        double padWidth,
        double padHeight,
        Point position);

    /// <summary>
    /// Performs body hit-testing across all zones.
    /// </summary>
    int HitTestZoneBody(
        IReadOnlyList<ZoneConfig> zones,
        double padWidth,
        double padHeight,
        Point position);

    /// <summary>
    /// Starts a resize drag session for the given zone and edge.
    /// </summary>
    ZoneResizeSession? StartResizeSession(
        IReadOnlyList<ZoneConfig> zones,
        int zoneIndex,
        ZoneResizeEdge edge,
        Point dragStartPx);

    /// <summary>
    /// Computes an updated zone rectangle for an active resize session.
    /// </summary>
    ZoneResizeUpdate? ComputeResizeUpdate(
        IReadOnlyList<ZoneConfig> zones,
        ZoneResizeSession session,
        double padWidth,
        double padHeight,
        Point currentPx);
}

/// <summary>
/// Bit flags for zone edges/corners used by hit-testing and drag-resize.
/// </summary>
[System.Flags]
internal enum ZoneResizeEdge
{
    None = 0,
    Left = 1,
    Right = 2,
    Top = 4,
    Bottom = 8
}

/// <summary>
/// Immutable drag session snapshot for zone resizing.
/// </summary>
internal sealed record ZoneResizeSession(
    int ZoneIndex,
    ZoneResizeEdge Edge,
    Point DragStartPx,
    double OriginalLeft,
    double OriginalTop,
    double OriginalWidth,
    double OriginalHeight);

/// <summary>
/// Resolved resize update for one zone.
/// </summary>
internal sealed record ZoneResizeUpdate(
    int ZoneIndex,
    double Left,
    double Top,
    double Width,
    double Height);

