namespace TabletMidiPad.Services;

/// <summary>
/// Coordinates shell-level command actions for the main window.
/// </summary>
internal interface IMainShellCommandController
{
    void SaveSettings();

    void ResetDefaults();

    void OpenPreferences();

    void Exit();

    void RefreshWacomStatus();

    void ReconnectPen();

    void StartWacomService();

    void ShowAbout();
}

