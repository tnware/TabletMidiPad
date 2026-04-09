using System.Windows;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services.Zone;

/// <summary>
/// View-owned dependencies for <see cref="IZonePadController"/> (config, selection, and UI sync).
/// </summary>
internal interface IZonePadControllerHost
{
    AppConfig Config { get; }

    int SelectedZoneIndex { get; set; }

    double PadActualWidth { get; }

    double PadActualHeight { get; }

    void CommitCurrentZoneEdits();

    void LoadSelectedZoneIntoEditor();

    void RefreshPadZoneOverlay();

    void SetSettingsResult(string message, SettingsResultKind kind);

    /// <summary>
    /// Clears the pad cursor after a resize completes (restores default hit-test cursor behavior).
    /// </summary>
    void ClearPadCursor();
}
