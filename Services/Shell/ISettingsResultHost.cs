using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services.Shell;

/// <summary>
/// Contract for hosts that can surface a settings result message to the shell view-model.
/// </summary>
internal interface ISettingsResultHost
{
    /// <summary>
    /// Updates the settings result banner/message shown in the shell.
    /// </summary>
    void SetSettingsResult(string message, SettingsResultKind kind);
}
