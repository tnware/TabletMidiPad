namespace TabletMidiPad.ViewModels;

/// <summary>
/// View-model item representing a single zone entry in the zone list.
/// </summary>
internal sealed class ZoneListItemViewModel
{
    /// <summary>
    /// Gets the 1-based display index shown in the list.
    /// </summary>
    public int DisplayIndex { get; init; }

    /// <summary>
    /// Gets the zone index in the current config collection.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Gets the user-facing zone name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the zone is enabled.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Gets the short status label shown in the list.
    /// </summary>
    public string StatusText => IsEnabled ? "On" : "Off";

    /// <summary>
    /// Gets or sets the display text shown in the list.
    /// </summary>
    public string DisplayText { get; init; } = string.Empty;
}
