namespace TabletMidiPad.Services;

/// <summary>
/// Default formatter for input backend status text shown in the main view-model.
/// </summary>
internal sealed class InputBackendStatusFormatter : IInputBackendStatusFormatter
{
    /// <inheritdoc />
    public string BuildStatusText(
        string lastInputBackend,
        bool useWintab,
        int contextCount,
        string wintabDiagnostics,
        string expressKeyDiagnostics)
    {
        string ekPart = string.IsNullOrEmpty(expressKeyDiagnostics)
            ? string.Empty
            : $" | {expressKeyDiagnostics}";

        string configured = useWintab
            ? $"Wintab enabled, {contextCount} context(s){ekPart}"
            : $"Stylus/mouse fallback | {wintabDiagnostics}";

        return $"Input: {lastInputBackend} | {configured}";
    }
}
