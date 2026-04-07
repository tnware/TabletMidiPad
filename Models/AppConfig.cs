using System;
using System.Collections.Generic;

namespace TabletMidiPad.Models;

public sealed class AppConfig
{
    public string AppPortName { get; set; } = "Tablet XY (App)";
    public string DawPortName { get; set; } = "Tablet XY (DAW)";
    public string LoopbackAssociationId { get; set; } = "{7d6e1f6b-6b0d-4d6c-a1f3-44ddefb7df4d}";
    public int MousePressureValue { get; set; } = 127;
    public double PositionDeadZone { get; set; } = 0.003;
    public double PositionSmoothing { get; set; } = 0.35;
    public string PressureCurve { get; set; } = "Linear";
    public string InputBackendMode { get; set; } = "Auto";
    public string LowerPenButtonAction { get; set; } = PenButtonActions.None;
    public int LowerPenButtonMidiChannel { get; set; } = 1;
    public int LowerPenButtonCc { get; set; } = 64;
    public int LowerPenButtonNote { get; set; } = 60;
    public string UpperPenButtonAction { get; set; } = PenButtonActions.None;
    public int UpperPenButtonMidiChannel { get; set; } = 1;
    public int UpperPenButtonCc { get; set; } = 65;
    public int UpperPenButtonNote { get; set; } = 61;
    public List<ExpressKeyConfig> ExpressKeys { get; set; } = new();
    public bool SendPressureGlobally { get; set; } = true;
    public bool AllowZoneSwitchWhilePressed { get; set; } = true;
    public int MaxZoneRows { get; set; } = 2;
    public int MaxZonesPerRow { get; set; } = 2;
    public List<ZoneConfig> Zones { get; set; } = new() { ZoneConfig.CreateDefault() };

    public static AppConfig Default => new();

    // Legacy compatibility properties can be moved to a separate partial or kept here for migration
    public string PenButtonAction { get; set; } = string.Empty;
    public int PenButtonMidiChannel { get; set; } = 1;
    public int PenButtonCc { get; set; } = 64;
    public int PenButtonNote { get; set; } = 60;
    public string ExpressKeyAction { get; set; } = string.Empty;
    public int ExpressKeyMidiChannel { get; set; } = 1;
    public int ExpressKeyBaseCc { get; set; } = 70;
    public int ExpressKeyBaseNote { get; set; } = 36;
}
