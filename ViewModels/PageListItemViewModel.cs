namespace TabletMidiPad.ViewModels;

internal sealed class PageListItemViewModel
{
    public int Index { get; init; }

    public int DisplayIndex { get; init; }

    public string Name { get; init; } = string.Empty;

    public string DisplayText { get; init; } = string.Empty;
}
