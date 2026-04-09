using System.Windows;
using System.Windows.Media;
using TabletMidiPad.Services.Zone.Models;

namespace TabletMidiPad.Views;

/// <summary>
/// Lightweight FrameworkElement that renders a miniature zone preview for a given <see cref="ZoneOverlayStyle"/>.
/// </summary>
internal sealed class ZoneStylePreviewElement : FrameworkElement
{
    public static readonly DependencyProperty OverlayStyleProperty =
        DependencyProperty.Register(
            nameof(OverlayStyle),
            typeof(ZoneOverlayStyle),
            typeof(ZoneStylePreviewElement),
            new FrameworkPropertyMetadata(ZoneOverlayStyle.Plain, FrameworkPropertyMetadataOptions.AffectsRender));

    public ZoneOverlayStyle OverlayStyle
    {
        get => (ZoneOverlayStyle)GetValue(OverlayStyleProperty);
        set => SetValue(OverlayStyleProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        Color color = ResolveAccentColor();
        var fillBrush = new SolidColorBrush(Color.FromArgb(56, color.R, color.G, color.B));
        var strokePen = new Pen(new SolidColorBrush(Color.FromArgb(200, color.R, color.G, color.B)), 1.5);
        double r = 6;
        var bounds = new Rect(1, 1, w - 2, h - 2);

        dc.PushClip(new RectangleGeometry(bounds, r, r));

        dc.DrawRoundedRectangle(fillBrush, null, bounds, r, r);

        switch (OverlayStyle)
        {
            case ZoneOverlayStyle.VignetteGrid:
                DrawVignetteGrid(dc, bounds, color);
                break;
            case ZoneOverlayStyle.DotArray:
                DrawDotArray(dc, bounds, color);
                break;
            case ZoneOverlayStyle.XYGraph:
                DrawXYGraph(dc, bounds, color);
                break;
            case ZoneOverlayStyle.XYGraphBottomLeft:
                DrawXYGraphBottomLeft(dc, bounds, color);
                break;
        }

        dc.Pop(); // pop clip

        dc.DrawRoundedRectangle(null, strokePen, bounds, r, r);
    }

    private static void DrawVignetteGrid(DrawingContext dc, Rect bounds, Color color)
    {
        const int divisions = 7;
        const byte maxAlpha = 120;

        for (int i = 1; i < divisions; i++)
        {
            double t = (double)i / divisions;
            double distFromCenter = Math.Abs(t - 0.5) * 2;
            byte alpha = (byte)(maxAlpha * (1 - distFromCenter * distFromCenter));
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B)), 1.0);

            double xPos = bounds.Left + t * bounds.Width;
            dc.DrawLine(pen, new Point(xPos, bounds.Top), new Point(xPos, bounds.Bottom));

            double yPos = bounds.Top + t * bounds.Height;
            dc.DrawLine(pen, new Point(bounds.Left, yPos), new Point(bounds.Right, yPos));
        }
    }

    private static void DrawDotArray(DrawingContext dc, Rect bounds, Color color)
    {
        var dotBrush = new SolidColorBrush(Color.FromArgb(155, color.R, color.G, color.B));
        const double spacing = 11.0;
        const double dotR = 1.8;

        double startX = bounds.Left + spacing / 2;
        double startY = bounds.Top + spacing / 2;

        for (double x = startX; x < bounds.Right; x += spacing)
        {
            for (double y = startY; y < bounds.Bottom; y += spacing)
            {
                dc.DrawEllipse(dotBrush, null, new Point(x, y), dotR, dotR);
            }
        }
    }

    private static void DrawXYGraph(DrawingContext dc, Rect bounds, Color color)
    {
        const byte axisAlpha = 195;
        const double margin = 8;
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(axisAlpha, color.R, color.G, color.B)), 1.5);

        double cx = bounds.Left + bounds.Width / 2;
        double cy = bounds.Top + bounds.Height / 2;

        dc.DrawLine(pen, new Point(bounds.Left + margin, cy), new Point(bounds.Right - margin, cy));
        dc.DrawLine(pen, new Point(cx, bounds.Top + margin), new Point(cx, bounds.Bottom - margin));
        dc.DrawEllipse(pen.Brush, null, new Point(cx, cy), 2.5, 2.5);
    }

    private void DrawXYGraphBottomLeft(DrawingContext dc, Rect bounds, Color color)
    {
        const byte axisAlpha = 200;
        const double margin = 8;
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(axisAlpha, color.R, color.G, color.B)), 1.5);
        var textBrush = new SolidColorBrush(Color.FromArgb(215, color.R, color.G, color.B));

        double originX = bounds.Left + margin;
        double originY = bounds.Bottom - margin;
        double axisWidth = Math.Max(0, bounds.Width - (2 * margin));
        double axisHeight = Math.Max(0, bounds.Height - (2 * margin));

        dc.DrawLine(pen, new Point(originX, originY), new Point(bounds.Right - margin, originY));
        dc.DrawLine(pen, new Point(originX, originY), new Point(originX, bounds.Top + margin));
        dc.DrawEllipse(pen.Brush, null, new Point(originX, originY), 2.5, 2.5);

        foreach ((double t, string label, bool major) in new[]
        {
            (0.00, "0", true),
            (0.50, "64", true),
            (1.00, "127", true),
        })
        {
            double x = originX + (t * axisWidth);
            double y = originY - (t * axisHeight);
            double tickHalf = major ? 3.0 : 0.0;

            dc.DrawEllipse(pen.Brush, null, new Point(x, originY), 1.8, 1.8);
            dc.DrawEllipse(pen.Brush, null, new Point(originX, y), 1.8, 1.8);

            if (major)
            {
                dc.DrawLine(pen, new Point(x, originY - tickHalf), new Point(x, originY + tickHalf));
                dc.DrawLine(pen, new Point(originX - tickHalf, y), new Point(originX + tickHalf, y));
            }

            DrawLabel(dc, label, textBrush, new Point(t == 0 ? x + 4 : x, originY - 4), t == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Center, VerticalAlignment.Bottom);
            if (t > 0)
            {
                DrawLabel(dc, label, textBrush, new Point(originX + 4, y), HorizontalAlignment.Left, VerticalAlignment.Center);
            }
        }
    }

    private void DrawLabel(
        DrawingContext dc,
        string text,
        Brush brush,
        Point anchor,
        HorizontalAlignment horizontalAlignment,
        VerticalAlignment verticalAlignment)
    {
        var formattedText = new FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            8,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        double x = anchor.X;
        double y = anchor.Y;

        if (horizontalAlignment == HorizontalAlignment.Center)
        {
            x -= formattedText.Width / 2;
        }
        else if (horizontalAlignment == HorizontalAlignment.Right)
        {
            x -= formattedText.Width;
        }

        if (verticalAlignment == VerticalAlignment.Center)
        {
            y -= formattedText.Height / 2;
        }
        else if (verticalAlignment == VerticalAlignment.Bottom)
        {
            y -= formattedText.Height;
        }

        dc.DrawText(formattedText, new Point(x, y));
    }

    private Color ResolveAccentColor()
    {
        object? raw = TryFindResource("AccentFillColorDefaultBrush") ?? Application.Current?.TryFindResource("AccentFillColorDefaultBrush");
        if (raw is SolidColorBrush brush)
        {
            return brush.Color;
        }

        return Color.FromRgb(0x4C, 0xA0, 0xFF);
    }
}
