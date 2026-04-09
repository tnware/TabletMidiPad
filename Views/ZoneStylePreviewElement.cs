using System.Windows;
using System.Windows.Media;
using TabletMidiPad.Services.Zone.Models;

namespace TabletMidiPad.Views;

/// <summary>
/// Lightweight FrameworkElement that renders a miniature zone preview for a given <see cref="ZoneOverlayStyle"/>.
/// </summary>
internal sealed class ZoneStylePreviewElement : FrameworkElement
{
    // Default zone palette green — matches ZonePalette1Color in ThemeResources.
    private static readonly Color PreviewColor = Color.FromRgb(0x67, 0xD6, 0x8A);

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

        Color color = PreviewColor;
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
}
