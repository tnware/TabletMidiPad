using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services.Shell;

/// <summary>
/// View host callbacks required by <see cref="IMainShellCommandController"/>.
/// </summary>
internal interface IMainShellCommandHost
    : ISettingsResultHost
{
    void CommitCurrentZoneEdits();

    void SetWacomServiceState(string statusHeader, bool startEnabled);

    void SetWacomServiceStatusHeader(string statusHeader);

    void CloseShell();

    void ShowAboutDialog();
}
