using System.Windows;
using System.Windows.Media;
using TabletMidiPad.Core.Interaction;

namespace TabletMidiPad.Services.Pad;

/// <summary>
/// Builds pad overlay render requests and cursor chrome for interaction ticks.
/// </summary>
internal interface IPadPresentationCoordinator
{
    /// <summary>
    /// Computes cursor layout for a tick; returns <see cref="CursorTickChrome.ShouldApply"/> false when the cursor should be left unchanged.
    /// </summary>
    CursorTickChrome ComputeCursorForInteractionTick(
        InteractionInput input,
        Point pixelPosition,
        double cursorDotWidth,
        double cursorDotHeight,
        double? opacityOverride);

    /// <summary>
    /// Cursor state after a full release (matches legacy MainWindow release cleanup).
    /// </summary>
    CursorTickChrome ComputeCursorAfterRelease();

    /// <summary>
    /// Builds the zone overlay request from config zones and last-known local points.
    /// </summary>
    ZoneOverlayRenderRequest BuildZoneOverlayRequest(
        IReadOnlyList<ZoneConfig> zones,
        IReadOnlyDictionary<ZoneConfig, Point> lastZonePoints,
        int selectedZoneIndex,
        double padWidth,
        double padHeight,
        PadOverlayTheme theme);
}

/// <summary>
/// Theme inputs for overlay rendering (resolved in the view from application resources).
/// </summary>
public sealed record PadOverlayTheme(
    IReadOnlyList<Color> ZonePalette,
    Color ColorWindow,
    Brush TextPrimaryBrush,
    Brush TextMutedBrush,
    Brush TextSoftBrush,
    Brush DangerBrush,
    Brush AccentBrushHover,
    Brush SurfaceBrush);

/// <summary>
/// Cursor dot layout for one application step.
/// </summary>
public readonly record struct CursorTickChrome(
    bool ShouldApply,
    Visibility Visibility,
    double Left,
    double Top,
    double? Opacity);
