namespace TabletMidiPad.ViewModels;

public interface IMainWindowActions
{
    void ApplySettings();
    void ResetDefaults();
    void OpenPreferences();
    void Exit();
    void RefreshWacomStatus();
    void ReconnectInput();
    void StartWacomService();
    void ShowAbout();
    void AutoLayoutZones();
    void AddZone();
    void RemoveZone();
}
