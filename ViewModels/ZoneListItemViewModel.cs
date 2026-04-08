namespace TabletMidiPad.ViewModels;

/// <summary>
/// View-model item representing a single zone entry in the zone list.
/// </summary>
internal sealed class ZoneListItemViewModel
{
    /// <summary>
    /// Gets the zone index in the current config collection.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Gets or sets the display text shown in the list.
    /// </summary>
    public string DisplayText { get; init; } = string.Empty;
}
