using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services;

/// <summary>
/// View-owned surface for <see cref="IMainWindowZoneListSession"/> (config, selection, zone list VM sync).
/// </summary>
internal interface IMainWindowZoneListSessionHost
{
    AppConfig Config { get; }

    int SelectedZoneIndex { get; set; }

    void CommitCurrentZoneEdits();

    void RefreshZoneList();

    void NotifySelectedZoneIndexChanged();

    void LoadSelectedZoneIntoEditor();

    void RefreshPadZoneOverlay();

    void SetSettingsResult(string message, SettingsResultKind kind);
}
