namespace TabletMidiPad.Services;

/// <summary>
/// Orchestrates loading configuration into zone editors, list sync, selection, and connected-state status labels.
/// </summary>
internal interface IMainWindowZoneEditorSession
{
    void LoadConfigIntoUi(AppConfig config);

    void UpdateUiForCurrentConfig();

    void RefreshZoneList();

    void LoadSelectedZoneIntoEditor();

    void CommitCurrentZoneEdits();

    /// <summary>
    /// Handles zone list selection changes from the view (no-op while <see cref="IMainWindowZoneEditorSessionHost.IsLoadingUi"/>).
    /// </summary>
    void OnZoneListSelectionChanged();
}
