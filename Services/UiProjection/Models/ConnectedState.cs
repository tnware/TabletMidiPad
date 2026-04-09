namespace TabletMidiPad.Services.UiProjection.Models;

/// <summary>
/// Main connected-state status labels mapped from configuration.
/// </summary>
internal readonly record struct ConnectedState(
    string StatusText,
    string RoutingText,
    string ActiveZoneText,
    string ConfigSummaryText,
    string ValueText,
    string PenButtonsText,
    string ModifiersText);
