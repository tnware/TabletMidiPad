namespace TabletMidiPad.Services;

/// <summary>
/// Maps <see cref="ZoneResizeEdge"/> flags to a pad resize cursor hint (matches legacy MainWindow edge ordering).
/// </summary>
internal static class ZonePadResizeCursors
{
    /// <summary>
    /// Returns a cursor kind for the given edge flags, or <see cref="ZonePadResizeCursorKind.None"/> when there is no edge.
    /// </summary>
    public static ZonePadResizeCursorKind FromResizeEdge(ZoneResizeEdge edge)
    {
        return edge switch
        {
            ZoneResizeEdge.None => ZonePadResizeCursorKind.None,
            ZoneResizeEdge.Left or ZoneResizeEdge.Right => ZonePadResizeCursorKind.SizeWE,
            ZoneResizeEdge.Top or ZoneResizeEdge.Bottom => ZonePadResizeCursorKind.SizeNS,
            ZoneResizeEdge.Left | ZoneResizeEdge.Top => ZonePadResizeCursorKind.SizeNWSE,
            ZoneResizeEdge.Right | ZoneResizeEdge.Bottom => ZonePadResizeCursorKind.SizeNWSE,
            ZoneResizeEdge.Left | ZoneResizeEdge.Bottom => ZonePadResizeCursorKind.SizeNESW,
            ZoneResizeEdge.Right | ZoneResizeEdge.Top => ZonePadResizeCursorKind.SizeNESW,
            _ => ZonePadResizeCursorKind.None,
        };
    }
}
