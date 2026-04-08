using System.Collections.Generic;
using System.Windows.Media;

namespace TabletMidiPad.Services;

/// <summary>
/// Input data needed to render the zone overlay.
/// </summary>
public sealed record ZoneOverlayRenderRequest(
    double PadWidth,
    double PadHeight,
    IReadOnlyList<ZoneOverlayZone> Zones,
    int SelectedZoneIndex,
    IReadOnlyDictionary<int, (double x, double y)> LastLocalByZoneIndex,
    IReadOnlyList<Color> ZonePalette,
    Color ColorWindow,
    Brush TextPrimaryBrush,
    Brush TextMutedBrush,
    Brush TextSoftBrush,
    Brush DangerBrush,
    Brush AccentBrushHover,
    Brush SurfaceBrush);

/// <summary>
/// View-only zone snapshot used by the overlay renderer.
/// </summary>
public sealed record ZoneOverlayZone(
    string Name,
    bool Enabled,
    bool SendX,
    bool SendY,
    bool SendPressure,
    double Left,
    double Top,
    double Width,
    double Height);

