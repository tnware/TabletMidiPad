namespace TabletMidiPad.ViewModels;

/// <summary>
/// Drives settings feedback strip foreground via XAML data triggers (no brushes on the view model).
/// </summary>
internal enum SettingsResultKind
{
    None,
    Success,
    Error,
}

/// <summary>
/// Drives MIDI status line foreground via XAML data triggers.
/// </summary>
internal enum MidiStatusSeverity
{
    Normal,
    Error,
}
