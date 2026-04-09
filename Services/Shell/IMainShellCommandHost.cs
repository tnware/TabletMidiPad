using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services.Shell;

/// <summary>
/// View host callbacks required by <see cref="IMainShellCommandController"/>.
/// </summary>
internal interface IMainShellCommandHost
{
    void CommitCurrentZoneEdits();

    void SetSettingsResult(string message, SettingsResultKind kind);

    void SetWacomServiceState(string statusHeader, bool startEnabled);

    void SetWacomServiceStatusHeader(string statusHeader);

    void CloseShell();

    void ShowAboutDialog();
}
