using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using TabletMidiPad.Services.Zone.Models;

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

            overlayCanvas.Children.Add(rect);
            AddStyleDecoration(overlayCanvas, request.OverlayStyle, rectLeft, rectTop, rectWidth, rectHeight, baseColor);
            AddModeDecoration(overlayCanvas, zone, rectLeft, rectTop, rectWidth, rectHeight, baseColor, selected);

            Canvas.SetLeft(label, rectLeft + 6);
            Canvas.SetTop(label, rectTop + 6);
            overlayCanvas.Children.Add(label);

            if (selected)
            {
                AddResizeHandles(overlayCanvas, rectLeft, rectTop, rectWidth, rectHeight, strokeColor, request.SurfaceBrush);
            }

            if (request.LastLocalByZoneIndex.TryGetValue(i, out (double X, double Y) local))
            {
                double markerX = zone.SendX ? local.X : 0.5;
                double markerY = zone.SendY ? local.Y : 0.5;
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

    private static void AddModeDecoration(
        Canvas canvas,
        ZoneOverlayZone zone,
        double left,
        double top,
        double width,
        double height,
        Color baseColor,
        bool selected)
    {
        string mode = NormalizeZoneOutputMode(zone.OutputMode);
        if (!string.Equals(mode, ZoneOutputModes.ScaleBreath, StringComparison.Ordinal))
        {
            if (!string.Equals(mode, ZoneOutputModes.ScaleGrid, StringComparison.Ordinal))
            {
                return;
            }

            AddScaleGridOverlay(canvas, zone, left, top, width, height, baseColor, selected);
            return;
        }

        AddScaleBreathOverlay(canvas, zone, left, top, width, height, baseColor, selected);
    }

    private static void AddScaleBreathOverlay(
        Canvas canvas,
        ZoneOverlayZone zone,
        double left,
        double top,
        double width,
        double height,
        Color baseColor,
        bool selected)
    {
        int holes = Math.Clamp(zone.ScaleBreathHoleCount, 4, 16);
        double split = Math.Clamp(zone.ScaleBreathRowSplit, 0.1, 0.9);
        byte lineAlpha = selected ? (byte)150 : (byte)110;
        var lineBrush = new SolidColorBrush(WithAlpha(baseColor, lineAlpha));

        for (int i = 1; i < holes; i++)
        {
            double x = left + ((width * i) / holes);
            var divider = new Line
            {
                X1 = x,
                Y1 = top,
                X2 = x,
                Y2 = top + height,
                Stroke = lineBrush,
                StrokeThickness = 1
            };
            canvas.Children.Add(divider);
        }

        double splitY = top + (height * split);
        var splitLine = new Line
        {
            X1 = left,
            Y1 = splitY,
            X2 = left + width,
            Y2 = splitY,
            Stroke = lineBrush,
            StrokeThickness = 1.2
        };
        canvas.Children.Add(splitLine);

        bool drawLabels = (width / holes) >= 26 && height >= 64;
        if (!drawLabels)
        {
            return;
        }

        var labelBrush = new SolidColorBrush(WithAlpha(baseColor, selected ? (byte)245 : (byte)215));
        for (int hole = 0; hole < holes; hole++)
        {
            for (int row = 0; row < 2; row++)
            {
                int streamIndex = (hole * 2) + row;
                int note = BuildScaleBreathNote(zone.ScaleBreathRoot, zone.ScaleBreathScale, zone.ScaleBreathOctave, streamIndex);
                string labelText = FormatNoteName(note);
                var label = new TextBlock
                {
                    Text = labelText,
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = labelBrush
                };

                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double cellLeft = left + ((hole * width) / holes);
                double cellRight = left + (((hole + 1) * width) / holes);
                double centerX = (cellLeft + cellRight) / 2;

                double rowTop = row == 0 ? top : splitY;
                double rowBottom = row == 0 ? splitY : top + height;
                double centerY = (rowTop + rowBottom) / 2;

                Canvas.SetLeft(label, centerX - (label.DesiredSize.Width / 2));
                Canvas.SetTop(label, centerY - (label.DesiredSize.Height / 2));
                canvas.Children.Add(label);
            }
        }
    }

    private static void AddScaleGridOverlay(
        Canvas canvas,
        ZoneOverlayZone zone,
        double left,
        double top,
        double width,
        double height,
        Color baseColor,
        bool selected)
    {
        int rows = Math.Clamp(zone.ScaleGridRows, 1, 16);
        int cols = Math.Clamp(zone.ScaleGridColumns, 1, 16);
        byte lineAlpha = selected ? (byte)150 : (byte)110;
        var lineBrush = new SolidColorBrush(WithAlpha(baseColor, lineAlpha));

        for (int c = 1; c < cols; c++)
        {
            double x = left + ((width * c) / cols);
            var divider = new Line
            {
                X1 = x,
                Y1 = top,
                X2 = x,
                Y2 = top + height,
                Stroke = lineBrush,
                StrokeThickness = 1
            };
            canvas.Children.Add(divider);
        }

        for (int r = 1; r < rows; r++)
        {
            double y = top + ((height * r) / rows);
            var divider = new Line
            {
                X1 = left,
                Y1 = y,
                X2 = left + width,
                Y2 = y,
                Stroke = lineBrush,
                StrokeThickness = 1
            };
            canvas.Children.Add(divider);
        }

        bool drawLabels = (width / cols) >= 24 && (height / rows) >= 16;
        if (!drawLabels)
        {
            return;
        }

        var labelBrush = new SolidColorBrush(WithAlpha(baseColor, selected ? (byte)245 : (byte)215));
        for (int rowFromTop = 0; rowFromTop < rows; rowFromTop++)
        {
            int rowFromBottom = (rows - 1) - rowFromTop;
            for (int col = 0; col < cols; col++)
            {
                int streamIndex = (rowFromBottom * cols) + col;
                int note = BuildScaleBreathNote(zone.ScaleGridRoot, zone.ScaleGridScale, zone.ScaleGridOctave, streamIndex);
                string labelText = FormatNoteName(note);
                var label = new TextBlock
                {
                    Text = labelText,
                    FontSize = 8.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = labelBrush
                };

                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double cellLeft = left + ((col * width) / cols);
                double cellRight = left + (((col + 1) * width) / cols);
                double centerX = (cellLeft + cellRight) / 2;

                double cellTop = top + ((rowFromTop * height) / rows);
                double cellBottom = top + (((rowFromTop + 1) * height) / rows);
                double centerY = (cellTop + cellBottom) / 2;

                Canvas.SetLeft(label, centerX - (label.DesiredSize.Width / 2));
                Canvas.SetTop(label, centerY - (label.DesiredSize.Height / 2));
                canvas.Children.Add(label);
            }
        }
    }

    private static void AddStyleDecoration(
        Canvas canvas,
        ZoneOverlayStyle style,
        double left,
        double top,
        double width,
        double height,
        Color baseColor)
    {
        switch (style)
        {
            case ZoneOverlayStyle.VignetteGrid:
                AddVignetteGrid(canvas, left, top, width, height, baseColor);
                break;
            case ZoneOverlayStyle.DotArray:
                AddDotArray(canvas, left, top, width, height, baseColor);
                break;
            case ZoneOverlayStyle.XYGraph:
                AddXYGraph(canvas, left, top, width, height, baseColor);
                break;
            case ZoneOverlayStyle.XYGraphBottomLeft:
                AddXYGraphBottomLeft(canvas, left, top, width, height, baseColor);
                break;
        }
    }

    private static void AddVignetteGrid(Canvas canvas, double left, double top, double width, double height, Color baseColor)
    {
        const int divisions = 8;
        const byte maxAlpha = 115;

        for (int i = 1; i < divisions; i++)
        {
            double t = (double)i / divisions;
            double distFromCenter = Math.Abs(t - 0.5) * 2; // 0 at center, 1 at edge
            byte alpha = (byte)(maxAlpha * (1 - distFromCenter * distFromCenter));

            var pen = new SolidColorBrush(WithAlpha(baseColor, alpha));

            // Vertical line
            double xPos = left + t * width;
            var vLine = new Line
            {
                X1 = xPos, Y1 = top,
                X2 = xPos, Y2 = top + height,
                Stroke = pen,
                StrokeThickness = 1
            };
            canvas.Children.Add(vLine);

            // Horizontal line
            double yPos = top + t * height;
            var hLine = new Line
            {
                X1 = left, Y1 = yPos,
                X2 = left + width, Y2 = yPos,
                Stroke = pen,
                StrokeThickness = 1
            };
            canvas.Children.Add(hLine);
        }
    }

    private static void AddDotArray(Canvas canvas, double left, double top, double width, double height, Color baseColor)
    {
        const double dotRadius = 2.0;
        const double spacing = 18.0;

        var dotGeometry = new EllipseGeometry(new Point(spacing / 2, spacing / 2), dotRadius, dotRadius);
        var dotBrush = new DrawingBrush(new GeometryDrawing(
            new SolidColorBrush(WithAlpha(baseColor, 130)),
            null,
            dotGeometry))
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(left % spacing, top % spacing, spacing, spacing),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None
        };

        var dotRect = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = dotBrush
        };
        Canvas.SetLeft(dotRect, left);
        Canvas.SetTop(dotRect, top);
        canvas.Children.Add(dotRect);
    }

    private static void AddXYGraph(Canvas canvas, double left, double top, double width, double height, Color baseColor)
    {
        const double margin = 10;
        const byte axisAlpha = 180;
        var pen = new SolidColorBrush(WithAlpha(baseColor, axisAlpha));

        double cx = left + width / 2;
        double cy = top + height / 2;

        var xAxis = new Line
        {
            X1 = left + margin,
            Y1 = cy,
            X2 = left + width - margin,
            Y2 = cy,
            Stroke = pen,
            StrokeThickness = 1.5
        };

        var yAxis = new Line
        {
            X1 = cx,
            Y1 = top + margin,
            X2 = cx,
            Y2 = top + height - margin,
            Stroke = pen,
            StrokeThickness = 1.5
        };

        var origin = new Ellipse
        {
            Width = 5,
            Height = 5,
            Fill = pen
        };
        Canvas.SetLeft(origin, cx - 2.5);
        Canvas.SetTop(origin, cy - 2.5);

        canvas.Children.Add(xAxis);
        canvas.Children.Add(yAxis);
        canvas.Children.Add(origin);
    }

    private static void AddXYGraphBottomLeft(Canvas canvas, double left, double top, double width, double height, Color baseColor)
    {
        const double margin = 10;
        const byte axisAlpha = 190;
        var axisBrush = new SolidColorBrush(WithAlpha(baseColor, axisAlpha));
        var labelBrush = new SolidColorBrush(WithAlpha(baseColor, 210));

        double originX = left + margin;
        double originY = top + height - margin;
        double axisWidth = Math.Max(0, width - (2 * margin));
        double axisHeight = Math.Max(0, height - (2 * margin));

        var xAxis = new Line
        {
            X1 = originX,
            Y1 = originY,
            X2 = left + width - margin,
            Y2 = originY,
            Stroke = axisBrush,
            StrokeThickness = 1.5
        };

        var yAxis = new Line
        {
            X1 = originX,
            Y1 = originY,
            X2 = originX,
            Y2 = top + margin,
            Stroke = axisBrush,
            StrokeThickness = 1.5
        };

        var origin = new Ellipse
        {
            Width = 5,
            Height = 5,
            Fill = axisBrush
        };
        Canvas.SetLeft(origin, originX - 2.5);
        Canvas.SetTop(origin, originY - 2.5);

        canvas.Children.Add(xAxis);
        canvas.Children.Add(yAxis);
        canvas.Children.Add(origin);

        // MIDI scale markers from 0..127 at strategic intervals.
        foreach ((double t, string label, bool major) in new[]
        {
            (0.00, "0", true),
            (0.25, "32", false),
            (0.50, "64", true),
            (0.75, "96", false),
            (1.00, "127", true),
        })
        {
            double x = originX + (t * axisWidth);
            double y = originY - (t * axisHeight);
            double markerRadius = major ? 2.2 : 1.5;
            double tickHalf = major ? 4.0 : 0.0;

            // X-axis marker.
            var xMarker = new Ellipse
            {
                Width = markerRadius * 2,
                Height = markerRadius * 2,
                Fill = axisBrush
            };
            Canvas.SetLeft(xMarker, x - markerRadius);
            Canvas.SetTop(xMarker, originY - markerRadius);
            canvas.Children.Add(xMarker);

            // Y-axis marker.
            var yMarker = new Ellipse
            {
                Width = markerRadius * 2,
                Height = markerRadius * 2,
                Fill = axisBrush
            };
            Canvas.SetLeft(yMarker, originX - markerRadius);
            Canvas.SetTop(yMarker, y - markerRadius);
            canvas.Children.Add(yMarker);

            if (major)
            {
                var xTick = new Line
                {
                    X1 = x,
                    Y1 = originY - tickHalf,
                    X2 = x,
                    Y2 = originY + tickHalf,
                    Stroke = axisBrush,
                    StrokeThickness = 1
                };
                canvas.Children.Add(xTick);

                var yTick = new Line
                {
                    X1 = originX - tickHalf,
                    Y1 = y,
                    X2 = originX + tickHalf,
                    Y2 = y,
                    Stroke = axisBrush,
                    StrokeThickness = 1
                };
                canvas.Children.Add(yTick);
            }

            var xLabel = new TextBlock
            {
                Text = label,
                FontSize = 9,
                Foreground = labelBrush
            };
            xLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double xLabelLeft = t == 0
                ? x + 4
                : x - (xLabel.DesiredSize.Width / 2);
            Canvas.SetLeft(xLabel, xLabelLeft);
            Canvas.SetTop(xLabel, originY - xLabel.DesiredSize.Height - 4);
            canvas.Children.Add(xLabel);

            if (t > 0)
            {
                var yLabel = new TextBlock
                {
                    Text = label,
                    FontSize = 9,
                    Foreground = labelBrush
                };
                yLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(yLabel, originX + 6);
                Canvas.SetTop(yLabel, y - (yLabel.DesiredSize.Height / 2));
                canvas.Children.Add(yLabel);
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
                new Point(rectLeft + (rectWidth / 2), rectTop),
                new Point(rectLeft + rectWidth, rectTop),
                new Point(rectLeft, rectTop + (rectHeight / 2)),
                new Point(rectLeft + rectWidth, rectTop + (rectHeight / 2)),
                new Point(rectLeft, rectTop + rectHeight),
                new Point(rectLeft + (rectWidth / 2), rectTop + rectHeight),
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

    private static string NormalizeZoneOutputMode(string? text)
    {
        string value = (text ?? string.Empty).Trim();
        return value switch
        {
            ZoneOutputModes.CcPad => value,
            ZoneOutputModes.ScaleBreath => value,
            ZoneOutputModes.ScaleGrid => value,
            _ => ZoneOutputModes.CcPad
        };
    }

    private static string NormalizeScaleBreathScale(string? text)
    {
        string value = (text ?? string.Empty).Trim();
        return value switch
        {
            ScaleBreathScaleNames.Major => value,
            ScaleBreathScaleNames.NaturalMinor => value,
            ScaleBreathScaleNames.MajorPentatonic => value,
            ScaleBreathScaleNames.MinorPentatonic => value,
            _ => ScaleBreathScaleNames.Major
        };
    }

    private static int BuildScaleBreathNote(int root, string scale, int octave, int streamIndex)
    {
        int[] steps = BuildScaleSteps(NormalizeScaleBreathScale(scale));
        int rootMidi = Math.Clamp((Math.Clamp(octave, 0, 8) * 12) + Math.Clamp(root, 0, 11), 0, 127);
        int octaveStride = streamIndex / steps.Length;
        int stepIndex = streamIndex % steps.Length;
        return Math.Clamp(rootMidi + (octaveStride * 12) + steps[stepIndex], 0, 127);
    }

    private static int[] BuildScaleSteps(string scale)
    {
        return scale switch
        {
            ScaleBreathScaleNames.NaturalMinor => [0, 2, 3, 5, 7, 8, 10],
            ScaleBreathScaleNames.MajorPentatonic => [0, 2, 4, 7, 9],
            ScaleBreathScaleNames.MinorPentatonic => [0, 3, 5, 7, 10],
            _ => [0, 2, 4, 5, 7, 9, 11]
        };
    }

    private static string FormatNoteName(int note)
    {
        string[] names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
        int clamped = Math.Clamp(note, 0, 127);
        int octave = (clamped / 12) - 1;
        return $"{names[clamped % 12]}{octave}";
    }
}
