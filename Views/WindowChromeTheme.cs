using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TabletMidiPad.Views;

/// <summary>
/// Applies Windows non-client (title bar / border) dark styling so shell chrome matches the in-app dark theme.
/// </summary>
internal static class WindowChromeTheme
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;

    /// <summary>
    /// BGR color ref matching <c>ThemeResources</c> <c>ColorWindow</c> (#FF060A07).
    /// </summary>
    private const int WindowFrameColorRef = unchecked(0x00070A06);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    /// <summary>
    /// Registers a one-time handler so dark chrome is applied when the window HWND exists.
    /// </summary>
    /// <param name="window">The WPF window to style.</param>
    public static void ApplyDarkNonClientChrome(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.SourceInitialized += OnWindowSourceInitialized;
    }

    private static void OnWindowSourceInitialized(object? sender, EventArgs e)
    {
        if (sender is not Window w)
        {
            return;
        }

        w.SourceInitialized -= OnWindowSourceInitialized;

        IntPtr hwnd = new WindowInteropHelper(w).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        int dark = 1;
        int cb = sizeof(int);
        if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, cb) != 0)
        {
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeLegacy, ref dark, cb);
        }

        int color = WindowFrameColorRef;
        DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref color, cb);
        DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref color, cb);
    }
}
