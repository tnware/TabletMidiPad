using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TabletMidiPad.Core.Interaction;
using TabletMidiPad.Services;

namespace TabletMidiPad.Views;

/// <summary>
/// Hosts the interactive pad surface, zone overlay, and cursor chrome; resolves overlay theme from application resources.
/// </summary>
public partial class PadInteractionSurface : UserControl, IPadInputChromeSurface
{
    private static readonly string[] ZonePaletteColorKeys =
    {
        "ZonePalette1Color",
        "ZonePalette2Color",
        "ZonePalette3Color",
        "ZonePalette4Color",
        "ZonePalette5Color",
        "ZonePalette6Color",
    };

    private IMainWindowPadPresentationCoordinator? _presentationCoordinator;
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
        IMainWindowPadPresentationCoordinator presentationCoordinator,
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
        int selectedZoneIndex)
    {
        EnsureServices();

        var theme = new MainWindowPadOverlayTheme(
            ZonePalette: Enumerable.Range(0, zones.Count).Select(GetZonePaletteColor).ToList(),
            ColorWindow: ThemeColor("ColorWindow", Colors.Black),
            TextPrimaryBrush: ThemeBrush("TextPrimaryBrush", Brushes.White),
            TextMutedBrush: ThemeBrush("TextMutedBrush", Brushes.Gainsboro),
            TextSoftBrush: ThemeBrush("TextSoftBrush", Brushes.Gray),
            DangerBrush: ThemeBrush("DangerBrush", Brushes.IndianRed),
            AccentBrushHover: ThemeBrush("AccentBrushHover", Brushes.White),
            SurfaceBrush: ThemeBrush("SurfaceBrush", Brushes.Black));

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
        => ThemeColor(ZonePaletteColorKeys[index % ZonePaletteColorKeys.Length], Color.FromRgb(0x67, 0xD6, 0x8A));

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
