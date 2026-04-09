using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TabletMidiPad.Core.Interaction;

namespace TabletMidiPad.Views;

/// <summary>
/// Hosts the interactive pad surface, zone overlay, and cursor chrome; resolves overlay theme from application resources.
/// </summary>
public partial class PadInteractionSurface : UserControl, IPadInputChromeSurface
{
    private IPadPresentationCoordinator? _presentationCoordinator;
    private IZoneOverlayRenderer? _zoneOverlayRenderer;

    /// <summary>
    /// Initializes a new instance of the <see cref="PadInteractionSurface"/> class.
    /// </summary>
    public PadInteractionSurface()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Wires presentation services from the composition root. Call once after the visual tree is loaded.
    /// </summary>
    internal void AttachPresentationServices(
        IPadPresentationCoordinator presentationCoordinator,
        IZoneOverlayRenderer zoneOverlayRenderer)
    {
        _presentationCoordinator = presentationCoordinator ?? throw new ArgumentNullException(nameof(presentationCoordinator));
        _zoneOverlayRenderer = zoneOverlayRenderer ?? throw new ArgumentNullException(nameof(zoneOverlayRenderer));
    }

    /// <summary>
    /// Raised when the pad surface layout (size) changes.
    /// </summary>
    internal event EventHandler? PadLayoutChanged;

    /// <summary>
    /// Hit-test surface for stylus, mouse, and coordinate queries.
    /// </summary>
    internal Canvas PadInputElement => Pad;

    internal event StylusDownEventHandler? PadStylusDown;

    internal event StylusEventHandler? PadStylusMove;

    internal event StylusEventHandler? PadStylusUp;

    internal event MouseButtonEventHandler? PadMouseDown;

    internal event MouseEventHandler? PadMouseMove;

    internal event MouseButtonEventHandler? PadMouseUp;

    internal CursorTickChrome ComputeCursorForInteractionTick(
        InteractionInput input,
        Point pixelPosition,
        double? opacityOverride)
    {
        EnsureServices();
        return _presentationCoordinator!.ComputeCursorForInteractionTick(
            input,
            pixelPosition,
            CursorDot.Width,
            CursorDot.Height,
            opacityOverride);
    }

    internal CursorTickChrome ComputeCursorAfterRelease()
    {
        EnsureServices();
        return _presentationCoordinator!.ComputeCursorAfterRelease();
    }

    internal void ApplyCursorChrome(CursorTickChrome chrome)
    {
        if (!chrome.ShouldApply)
        {
            return;
        }

        CursorDot.Visibility = chrome.Visibility;
        Canvas.SetLeft(CursorDot, chrome.Left);
        Canvas.SetTop(CursorDot, chrome.Top);
        if (chrome.Opacity.HasValue)
        {
            CursorDot.Opacity = chrome.Opacity.Value;
        }
    }

    internal void RefreshZoneOverlay(
        IReadOnlyList<ZoneConfig> zones,
        IReadOnlyDictionary<ZoneConfig, Point> lastZonePoints,
        int selectedZoneIndex,
        ZoneOverlayStyle overlayStyle = ZoneOverlayStyle.Plain)
    {
        EnsureServices();

        var theme = new PadOverlayTheme(
            ZonePalette: [.. Enumerable.Range(0, zones.Count).Select(GetZonePaletteColor)],
            ColorWindow: ThemeColor("LayerFillColorDefaultBrush", Color.FromRgb(0x08, 0x12, 0x20)),
            TextPrimaryBrush: ThemeBrush("TextFillColorPrimaryBrush", Brushes.White),
            TextMutedBrush: ThemeBrush("TextFillColorSecondaryBrush", Brushes.Gainsboro),
            TextSoftBrush: ThemeBrush("TextFillColorDisabledBrush", Brushes.Gray),
            DangerBrush: ThemeBrush("SystemFillColorCriticalBrush", Brushes.IndianRed),
            AccentBrushHover: ThemeBrush("AccentFillColorSecondaryBrush", Brushes.White),
            SurfaceBrush: ThemeBrush("LayerFillColorDefaultBrush", Brushes.Black),
            OverlayStyle: overlayStyle);

        ZoneOverlayRenderRequest request = _presentationCoordinator!.BuildZoneOverlayRequest(
            zones,
            lastZonePoints,
            selectedZoneIndex,
            Pad.ActualWidth,
            Pad.ActualHeight,
            theme);

        _zoneOverlayRenderer!.Render(ZoneOverlayCanvas, request);
    }

    private void EnsureServices()
    {
        if (_presentationCoordinator is null || _zoneOverlayRenderer is null)
        {
            throw new InvalidOperationException("Call AttachPresentationServices before using pad presentation APIs.");
        }
    }

    private Brush ThemeBrush(string key, Brush fallback)
        => TryFindResource(key) as Brush
           ?? Application.Current?.TryFindResource(key) as Brush
           ?? fallback;

    private Color ThemeColor(string key, Color fallback)
    {
        object? raw = TryFindResource(key) ?? Application.Current?.TryFindResource(key);
        if (raw is Color color)
        {
            return color;
        }

        if (raw is SolidColorBrush brush)
        {
            return brush.Color;
        }

        return fallback;
    }

    private Color GetZonePaletteColor(int index)
    {
        Color accent = ThemeColor("AccentFillColorDefaultBrush", Color.FromRgb(0x4C, 0xA0, 0xFF));
        Color[] palette =
        [
            accent,
            Mix(accent, Colors.White, 0.18),
            Mix(accent, Colors.White, 0.34),
            Mix(accent, Colors.Black, 0.12),
            Mix(accent, Colors.Black, 0.24),
            Mix(Mix(accent, Colors.White, 0.10), Colors.Black, 0.08),
        ];
        return palette[index % palette.Length];
    }

    private static Color Mix(Color from, Color to, double amount)
    {
        double t = Math.Clamp(amount, 0.0, 1.0);
        byte r = (byte)Math.Round(from.R + ((to.R - from.R) * t));
        byte g = (byte)Math.Round(from.G + ((to.G - from.G) * t));
        byte b = (byte)Math.Round(from.B + ((to.B - from.B) * t));
        return Color.FromRgb(r, g, b);
    }

    private void Pad_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        PadLayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Pad_StylusDown(object sender, StylusDownEventArgs e) => PadStylusDown?.Invoke(sender, e);

    private void Pad_StylusMove(object sender, StylusEventArgs e) => PadStylusMove?.Invoke(sender, e);

    private void Pad_StylusUp(object sender, StylusEventArgs e) => PadStylusUp?.Invoke(sender, e);

    private void Pad_MouseDown(object sender, MouseButtonEventArgs e) => PadMouseDown?.Invoke(sender, e);

    private void Pad_MouseMove(object sender, MouseEventArgs e) => PadMouseMove?.Invoke(sender, e);

    private void Pad_MouseUp(object sender, MouseButtonEventArgs e) => PadMouseUp?.Invoke(sender, e);

    CursorTickChrome IPadInputChromeSurface.ComputeCursorForInteractionTick(
        InteractionInput input,
        Point pixelPosition,
        double? opacityOverride)
        => ComputeCursorForInteractionTick(input, pixelPosition, opacityOverride);

    CursorTickChrome IPadInputChromeSurface.ComputeCursorAfterRelease()
        => ComputeCursorAfterRelease();

    void IPadInputChromeSurface.ApplyCursorChrome(CursorTickChrome chrome)
        => ApplyCursorChrome(chrome);
}
