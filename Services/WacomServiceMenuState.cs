namespace TabletMidiPad.Services;

/// <summary>
/// Wacom service menu state projected for MainWindow controls.
/// </summary>
internal readonly record struct WacomServiceMenuState(
    string Header,
    bool StartEnabled);
