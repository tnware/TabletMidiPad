namespace TabletMidiPad.Services.Settings.Models;

/// <summary>
/// Captures validated preferences values returned from the preferences dialog.
/// </summary>
internal sealed class PreferencesDialogResult
{
    public required string DawPortName { get; init; }
    public required string AppPortName { get; init; }
    public required string AssociationId { get; init; }
    public required int MousePressureValue { get; init; }
    public required double PositionDeadZone { get; init; }
    public required double PositionSmoothing { get; init; }
    public required string PressureCurve { get; init; }
    public required string InputBackendMode { get; init; }
    public required string LowerPenButtonAction { get; init; }
    public required int LowerPenButtonMidiChannel { get; init; }
    public required int LowerPenButtonCc { get; init; }
    public required int LowerPenButtonNote { get; init; }
    public required string UpperPenButtonAction { get; init; }
    public required int UpperPenButtonMidiChannel { get; init; }
    public required int UpperPenButtonCc { get; init; }
    public required int UpperPenButtonNote { get; init; }
    public required bool SendPressureGlobally { get; init; }
    public required bool AllowZoneSwitchWhilePressed { get; init; }
    public required List<ExpressKeyConfig> ExpressKeys { get; init; }
}
