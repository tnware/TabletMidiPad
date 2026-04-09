namespace TabletMidiPad.Services.Settings.Models;

/// <summary>
/// Result of a validate/save workflow step.
/// </summary>
internal readonly record struct WorkflowSaveResult(bool Success, string? ErrorMessage);
