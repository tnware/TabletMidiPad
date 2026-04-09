using System.Windows;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services.Settings;

/// <summary>
/// View-owned surface for <see cref="ISettingsWorkflow"/> (dialog owner and VM refresh hooks).
/// </summary>
internal interface ISettingsWorkflowHost
{
    Window OwnerWindow { get; }

    PadInputControllerState PadState { get; }

    void CommitCurrentZoneEdits();

    void SetSettingsResult(string message, SettingsResultKind kind);

    void UpdateUiForCurrentConfig();

    void RefreshPadZoneOverlay();

    void SyncToolbar();

    void LoadConfigIntoUi();
}
