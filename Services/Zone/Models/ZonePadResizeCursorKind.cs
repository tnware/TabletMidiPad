namespace TabletMidiPad.Services.Zone.Models;

/// <summary>
/// Platform-neutral resize cursor hint for pad zone edges (mapped to WPF cursors in the view).
/// </summary>
internal enum ZonePadResizeCursorKind
{
    None = 0,
    SizeWE,
    SizeNS,
    SizeNWSE,
    SizeNESW,
}
