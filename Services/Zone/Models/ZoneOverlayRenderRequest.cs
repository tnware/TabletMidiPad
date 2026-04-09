using System.Windows.Media;

namespace TabletMidiPad.Services.Zone.Models;

/// <summary>
/// Input data needed to render the zone overlay.
/// </summary>
public sealed record ZoneOverlayRenderRequest(
    double PadWidth,
    double PadHeight,
    IReadOnlyList<ZoneOverlayZone> Zones,
    int SelectedZoneIndex,
    IReadOnlyDictionary<int, (double X, double Y)> LastLocalByZoneIndex,
    IReadOnlyList<Color> ZonePalette,
    Color ColorWindow,
    Brush TextPrimaryBrush,
    Brush TextMutedBrush,
    Brush TextSoftBrush,
    Brush DangerBrush,
    Brush AccentBrushHover,
    Brush SurfaceBrush,
    ZoneOverlayStyle OverlayStyle);

/// <summary>
/// View-only zone snapshot used by the overlay renderer.
/// </summary>
public sealed record ZoneOverlayZone(
    string Name,
    bool Enabled,
    bool SendX,
    bool SendY,
    bool SendPressure,
    string OutputMode,
    int ScaleBreathRoot,
    string ScaleBreathScale,
    int ScaleBreathOctave,
    int ScaleBreathHoleCount,
    double ScaleBreathRowSplit,
    double Left,
    double Top,
    double Width,
    double Height);
