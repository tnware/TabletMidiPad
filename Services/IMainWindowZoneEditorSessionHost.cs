using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services;

/// <summary>
/// View-owned surface for <see cref="IMainWindowZoneEditorSession"/> (runtime config, selection, VM, and chrome refresh hooks).
/// </summary>
internal interface IMainWindowZoneEditorSessionHost
{
    AppConfig Config { get; }

    MainViewModel ViewModel { get; }

    int SelectedZoneIndex { get; set; }

    /// <summary>
    /// True while list/editor fields are being repopulated programmatically (suppresses selection-changed side effects).
    /// </summary>
    bool IsLoadingUi { get; set; }

    void SyncToolbar();

    void RefreshPadZoneOverlay();

    void RefreshInputBackendStatus();
}
