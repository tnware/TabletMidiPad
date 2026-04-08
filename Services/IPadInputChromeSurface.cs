using System.Windows;
using TabletMidiPad.Core.Interaction;

namespace TabletMidiPad.Services;

/// <summary>
/// Cursor chrome operations used by <see cref="MainWindowPadInputSession"/> so tests can substitute fakes without a full <see cref="Views.PadInteractionSurface"/> visual tree.
/// </summary>
internal interface IPadInputChromeSurface
{
    CursorTickChrome ComputeCursorForInteractionTick(InteractionInput input, Point pixelPosition, double? opacityOverride);

    CursorTickChrome ComputeCursorAfterRelease();

    void ApplyCursorChrome(CursorTickChrome chrome);
}
