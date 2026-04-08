namespace TabletMidiPad.Services;

/// <summary>
/// Builds input backend status text for the main window view-model.
/// </summary>
internal interface IInputBackendStatusFormatter
{
    /// <summary>
    /// Builds the composed input backend status string.
    /// </summary>
    string BuildStatusText(
        string lastInputBackend,
        bool useWintab,
        int contextCount,
        string wintabDiagnostics,
        string expressKeyDiagnostics);

    /// <summary>
    /// Builds pen button diagnostics text from Wintab button flags.
    /// </summary>
    string BuildPenButtonsText(uint buttons);
}
