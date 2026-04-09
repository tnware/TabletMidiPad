using System.Windows;

namespace TabletMidiPad.Services.Zone;

/// <summary>
/// Pad-local zone selection and edge-resize orchestration for the main window (mouse path only).
/// </summary>
internal interface IZonePadController
{
    /// <summary>
    /// Handles left-button down: body hit-test (selection) and edge hit-test (start resize).
    /// </summary>
    /// <returns>True when a resize drag started and the view should capture the mouse.</returns>
    bool TryHandleMouseDown(Point position);

    /// <summary>
    /// Handles mouse move during a resize or updates the resize cursor hint when idle.
    /// </summary>
    /// <param name="position">Position relative to the pad.</param>
    /// <param name="cursorKind">Cursor hint when not consuming the move; <see cref="ZonePadResizeCursorKind.None"/> while a resize drag is active.</param>
    /// <returns>True when the move was fully handled (active resize) and interaction routing should stop.</returns>
    bool TryConsumeMouseMove(Point position, out ZonePadResizeCursorKind cursorKind);

    /// <summary>
    /// Completes an active resize drag if any.
    /// </summary>
    /// <returns>True when a resize ended and the view should release mouse capture.</returns>
    bool TryConsumeMouseUp();
}
