using System;

namespace TabletMidiPad.Services.Settings;

/// <summary>
/// Coordinates settings/preferences decisions and validation logic.
/// </summary>
internal interface ISettingsWorkflowCoordinator
{
    int GetEffectiveExpressKeyCount(int detectedExpressKeyCount, int configuredExpressKeyCount);

    WorkflowSaveResult ValidateAndSave(AppConfig config);

    PreferencesWorkflowResult ApplyPreferences(
        AppConfig config,
        PreferencesDialogResult result,
        IntPtr midiOutHandle);
}
