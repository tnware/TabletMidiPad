namespace TabletMidiPad.Services.Diagnostics;

/// <summary>
/// Lightweight diagnostic sink for developer-facing traces (DebugView, log collectors). Not a user-visible UI channel.
/// </summary>
internal interface IAppTrace
{
    /// <summary>
    /// Writes an informational trace line.
    /// </summary>
    void Info(string source, string message);

    /// <summary>
    /// Writes a warning trace line (failures, missing optional dependencies).
    /// </summary>
    void Warn(string source, string message);
}
