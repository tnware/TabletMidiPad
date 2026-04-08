using System.Windows;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services;

/// <summary>
/// View-owned surface for <see cref="IMainWindowSettingsSession"/> (config, dialog owner, and VM refresh hooks).
/// </summary>
internal interface IMainWindowSettingsSessionHost
{
    Window OwnerWindow { get; }

    /// <summary>
    /// Current in-memory configuration (mutated by preferences and zone editing).
    /// </summary>
    AppConfig Config { get; }

    /// <summary>
    /// Replaces the in-memory configuration (e.g. reset to defaults).
    /// </summary>
    void ReplaceConfig(AppConfig config);

    PadInputSessionState PadState { get; }

    void CommitCurrentZoneEdits();

    void SetSettingsResult(string message, SettingsResultKind kind);

    void UpdateUiForCurrentConfig();

    void RefreshPadZoneOverlay();

    void SyncToolbar();

    void LoadConfigIntoUi(AppConfig config);
}
