using System;
using System.Windows;
using System.Windows.Input;

namespace TabletMidiPad.Services;

/// <summary>
/// Builds normalized interaction samples for stylus/mouse fallback input.
/// </summary>
internal sealed class InputFallbackService : IInputFallbackService
{
    /// <inheritdoc />
    public bool TryBuildStylusSample(
        StylusEventArgs e,
        IInputElement relativeTo,
        double padWidth,
        double padHeight,
        int mousePressureFallback,
        out InputFallbackSample sample)
    {
        Point point = e.GetPosition(relativeTo);
        if (!TryNormalize(point, padWidth, padHeight, out double xNorm, out double yNorm))
        {
            sample = default;
            return false;
        }

        int pressure = ResolveStylusPressure(e, relativeTo, mousePressureFallback);
        sample = new InputFallbackSample(point, xNorm, yNorm, pressure);
        return true;
    }

    /// <inheritdoc />
    public bool TryBuildMouseSample(
        MouseEventArgs e,
        IInputElement relativeTo,
        double padWidth,
        double padHeight,
        int mousePressureFallback,
        out InputFallbackSample sample)
    {
        Point point = e.GetPosition(relativeTo);
        if (!TryNormalize(point, padWidth, padHeight, out double xNorm, out double yNorm))
        {
            sample = default;
            return false;
        }

        sample = new InputFallbackSample(point, xNorm, yNorm, mousePressureFallback);
        return true;
    }

    private static int ResolveStylusPressure(StylusEventArgs stylusEvent, IInputElement relativeTo, int fallback)
    {
        StylusPointCollection stylusPoints = stylusEvent.GetStylusPoints(relativeTo);
        if (stylusPoints.Count <= 0)
        {
            return fallback;
        }

        StylusPoint point = stylusPoints[0];
        if (!point.HasProperty(StylusPointProperties.NormalPressure))
        {
            return fallback;
        }

        int raw = point.GetPropertyValue(StylusPointProperties.NormalPressure);
        int max = point.Description.GetPropertyInfo(StylusPointProperties.NormalPressure).Maximum;
        return max > 0 ? (int)Math.Round(raw * 127.0 / max) : fallback;
    }

    private static bool TryNormalize(Point point, double padWidth, double padHeight, out double xNorm, out double yNorm)
    {
        xNorm = 0;
        yNorm = 0;

        if (padWidth <= 0 || padHeight <= 0)
        {
            return false;
        }

        xNorm = point.X / padWidth;
        yNorm = point.Y / padHeight;
        return true;
    }
}
