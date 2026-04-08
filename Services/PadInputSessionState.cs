namespace TabletMidiPad.Services;

/// <summary>
/// Mutable pad/tablet interaction flags and diagnostics shared between the main window and <see cref="IMainWindowPadInputSession"/>.
/// </summary>
internal sealed class PadInputSessionState
{
    public bool IsActive;

    public bool UseWintab;

    public int ExpressKeyCount;

    public string LastInputBackend { get; set; } = "Idle";

    public string WintabDiagnostics { get; set; } = "Wintab not initialized yet";

    public string ExpressKeyDiagnostics { get; set; } = string.Empty;

    public uint LastWintabButtons;
}
