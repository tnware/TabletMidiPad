using System.Windows;
using System.Windows.Input;

namespace TabletMidiPad.Services;

/// <summary>
/// Builds normalized stylus/mouse fallback input samples for pad interaction processing.
/// </summary>
internal interface IInputFallbackService
{
    /// <summary>
    /// Attempts to build a stylus fallback sample from the event payload.
    /// </summary>
    bool TryBuildStylusSample(
        StylusEventArgs e,
        IInputElement relativeTo,
        double padWidth,
        double padHeight,
        int mousePressureFallback,
        out InputFallbackSample sample);

    /// <summary>
    /// Attempts to build a mouse fallback sample from the event payload.
    /// </summary>
    bool TryBuildMouseSample(
        MouseEventArgs e,
        IInputElement relativeTo,
        double padWidth,
        double padHeight,
        int mousePressureFallback,
        out InputFallbackSample sample);
}

/// <summary>
/// Normalized fallback input payload used by MainWindow interaction flow.
/// </summary>
internal readonly record struct InputFallbackSample(
    Point PixelPosition,
    double XNorm,
    double YNorm,
    int Pressure127);
