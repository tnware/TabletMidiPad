using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TabletMidiPad.Services.Zone;

/// <summary>
/// Default WPF implementation for rendering the zone overlay into a <see cref="Canvas"/>.
/// </summary>
internal sealed class ZoneOverlayRenderer : IZoneOverlayRenderer
{
    private const double HandleSize = 10;

    public void Render(Canvas overlayCanvas, ZoneOverlayRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(overlayCanvas);
        ArgumentNullException.ThrowIfNull(request);

        overlayCanvas.Children.Clear();

        if (request.PadWidth <= 0 || request.PadHeight <= 0)
        {
            return;
        }

        overlayCanvas.Width = request.PadWidth;
        overlayCanvas.Height = request.PadHeight;

        for (int i = 0; i < request.Zones.Count; i++)
        {
            ZoneOverlayZone zone = request.Zones[i];
            double rectLeft = zone.Left * request.PadWidth;
            double rectTop = zone.Top * request.PadHeight;
            double rectWidth = zone.Width * request.PadWidth;
            double rectHeight = zone.Height * request.PadHeight;

            Color baseColor = request.ZonePalette[i % request.ZonePalette.Count];
            bool selected = i == request.SelectedZoneIndex;
            byte fillAlpha = selected ? (byte)(zone.Enabled ? 100 : 56) : (byte)(zone.Enabled ? 56 : 28);
            byte outlineAlpha = selected ? (byte)240 : (byte)(zone.Enabled ? 170 : 85);
            Color fillColor = WithAlpha(baseColor, fillAlpha);
            Color strokeColor = WithAlpha(baseColor, outlineAlpha);

            var rect = new Rectangle
            {
                Width = rectWidth,
                Height = rectHeight,
                Fill = new SolidColorBrush(fillColor),
                Stroke = new SolidColorBrush(strokeColor),
                StrokeThickness = 2,
                RadiusX = 8,
                RadiusY = 8
            };

            string bounds = $"({zone.Left:0.##},{zone.Top:0.##}) {zone.Width:0.##}\u00D7{zone.Height:0.##}";
            string axes = $"  [{(zone.SendX ? "X" : "-")}{(zone.SendY ? "Y" : "-")}{(zone.SendPressure ? "P" : "-")}]";
            string pressureFlag = zone.SendPressure ? "" : "  [no pressure]";
            string enabledFlag = zone.Enabled ? "" : "  [off]";

            var label = new TextBlock
            {
                FontSize = 11,
                Padding = new Thickness(6, 3, 6, 3),
                Background = new SolidColorBrush(WithAlpha(request.ColorWindow, 180)),
            };
            label.Inlines.Add(new System.Windows.Documents.Run(zone.Name)
            {
                Foreground = request.TextPrimaryBrush,
                FontWeight = FontWeights.SemiBold
            });
            label.Inlines.Add(new System.Windows.Documents.Run($"  {bounds}")
            {
                Foreground = request.TextMutedBrush
            });
            label.Inlines.Add(new System.Windows.Documents.Run(axes)
            {
                Foreground = new SolidColorBrush(WithAlpha(baseColor, 240))
            });
            if (pressureFlag.Length > 0)
            {
                label.Inlines.Add(new System.Windows.Documents.Run(pressureFlag)
                {
                    Foreground = request.DangerBrush
                });
            }

            if (enabledFlag.Length > 0)
            {
                label.Inlines.Add(new System.Windows.Documents.Run(enabledFlag)
                {
                    Foreground = request.TextSoftBrush
                });
            }

            Canvas.SetLeft(rect, rectLeft);
            Canvas.SetTop(rect, rectTop);
            Canvas.SetLeft(label, rectLeft + 6);
            Canvas.SetTop(label, rectTop + 6);

            overlayCanvas.Children.Add(rect);
            overlayCanvas.Children.Add(label);

            if (selected)
            {
                AddResizeHandles(overlayCanvas, rectLeft, rectTop, rectWidth, rectHeight, strokeColor, request.SurfaceBrush);
            }

            if (request.LastLocalByZoneIndex.TryGetValue(i, out (double x, double y) local))
            {
                double markerX = zone.SendX ? local.x : 0.5;
                double markerY = zone.SendY ? local.y : 0.5;
                double markerCenterX = rectLeft + (markerX * rectWidth);
                double markerCenterY = rectTop + (markerY * rectHeight);

                var markerRing = new Ellipse
                {
                    Width = 18,
                    Height = 18,
                    Stroke = new SolidColorBrush(WithAlpha(baseColor, 235)),
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent
                };

                var markerDot = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = request.AccentBrushHover,
                    Stroke = request.SurfaceBrush,
                    StrokeThickness = 1
                };

                Canvas.SetLeft(markerRing, markerCenterX - (markerRing.Width / 2));
                Canvas.SetTop(markerRing, markerCenterY - (markerRing.Height / 2));
                Canvas.SetLeft(markerDot, markerCenterX - (markerDot.Width / 2));
                Canvas.SetTop(markerDot, markerCenterY - (markerDot.Height / 2));

                overlayCanvas.Children.Add(markerRing);
                overlayCanvas.Children.Add(markerDot);
            }
        }
    }

    private static void AddResizeHandles(
        Canvas canvas,
        double rectLeft,
        double rectTop,
        double rectWidth,
        double rectHeight,
        Color strokeColor,
        Brush fill)
    {
        double half = HandleSize / 2;
        var brush = new SolidColorBrush(Color.FromArgb(235, strokeColor.R, strokeColor.G, strokeColor.B));

        foreach (Point handleCenter in new[]
        {
            new Point(rectLeft, rectTop),
            new Point(rectLeft + rectWidth / 2, rectTop),
            new Point(rectLeft + rectWidth, rectTop),
            new Point(rectLeft, rectTop + rectHeight / 2),
            new Point(rectLeft + rectWidth, rectTop + rectHeight / 2),
            new Point(rectLeft, rectTop + rectHeight),
            new Point(rectLeft + rectWidth / 2, rectTop + rectHeight),
            new Point(rectLeft + rectWidth, rectTop + rectHeight)
        })
        {
            var handle = new Rectangle
            {
                Width = HandleSize,
                Height = HandleSize,
                RadiusX = 3,
                RadiusY = 3,
                Fill = fill,
                Stroke = brush,
                StrokeThickness = 1.5
            };

            Canvas.SetLeft(handle, handleCenter.X - half);
            Canvas.SetTop(handle, handleCenter.Y - half);
            canvas.Children.Add(handle);
        }
    }

    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);
}
