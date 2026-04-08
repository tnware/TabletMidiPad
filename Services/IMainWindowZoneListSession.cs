namespace TabletMidiPad.Services;

/// <summary>
/// Zone list actions for the main window: add, remove, and auto-layout with shared UI refresh sequencing.
/// </summary>
internal interface IMainWindowZoneListSession
{
    void AutoLayoutZones();

    void AddZone();

    void RemoveZone();
}
