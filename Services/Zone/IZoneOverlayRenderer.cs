using System.Windows.Controls;

namespace TabletMidiPad.Services;

/// <summary>
/// Renders the zone overlay visuals onto the pad overlay canvas.
/// </summary>
internal interface IZoneOverlayRenderer
{
    /// <summary>
    /// Renders zone rectangles, labels, markers, and resize handles.
    /// </summary>
    void Render(Canvas overlayCanvas, ZoneOverlayRenderRequest request);
}

