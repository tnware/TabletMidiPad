namespace TabletMidiPad.Services.Page;

/// <summary>
/// Result payload for page selection and mutation operations.
/// </summary>
internal readonly record struct PageSelectionResult(
    bool Changed,
    int ActivePageIndex,
    string StatusMessage);
