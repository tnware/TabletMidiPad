namespace TabletMidiPad.Services;

/// <summary>
/// Coordinates zone list action workflows for the main window.
/// </summary>
internal interface IZoneActionWorkflowCoordinator
{
    /// <summary>
    /// Applies auto-layout and computes resulting selection + status message.
    /// </summary>
    ZoneActionWorkflowResult AutoLayout(AppConfig config, int selectedZoneIndex);

    /// <summary>
    /// Adds a new zone and computes resulting selection + status message.
    /// </summary>
    ZoneActionWorkflowResult AddZone(AppConfig config);

    /// <summary>
    /// Removes the selected zone and computes resulting selection + status message.
    /// </summary>
    ZoneActionWorkflowResult RemoveZone(AppConfig config, int selectedZoneIndex);
}

/// <summary>
/// Result payload for zone action workflows.
/// </summary>
internal readonly record struct ZoneActionWorkflowResult(
    bool Changed,
    int SelectedZoneIndex,
    string StatusMessage);
