using System;
using System.Threading.Tasks;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services;

/// <summary>
/// Orchestrates main-window cold start (config load hook, MIDI connect presentation) and application exit shutdown.
/// </summary>
/// <remarks>
/// Tablet rebind during the session stays on <see cref="IMainWindowLifecycleCoordinator"/> from the view; only
/// window close uses <see cref="ShutdownForExit"/> here so exit sequencing lives in one place.
/// </remarks>
internal interface IMainWindowStartupLifecycleService
{
    /// <summary>
    /// Loads persisted configuration from disk.
    /// </summary>
    AppConfig LoadPersistedConfig(IConfigStore store);

    /// <summary>
    /// Shows the MIDI connecting UI, runs async MIDI initialization, then applies success or failure presentation on the UI thread.
    /// </summary>
    /// <param name="onMidiRoutingReady">Invoked on the UI thread after MIDI succeeds (e.g. refresh routing labels).</param>
    Task RunStartupMidiSequenceAsync(
        IMidiEngine midiEngine,
        AppConfig config,
        MainViewModel viewModel,
        Action<Action> marshalToUiThread,
        Action onMidiRoutingReady);

    /// <summary>
    /// Shuts down MIDI and tablet input, applies tablet lifecycle state for view sync, then disposes the Wintab backend.
    /// </summary>
    void ShutdownForExit(
        IMidiEngine midiEngine,
        AppConfig config,
        IMainWindowInputBackendCoordinator inputBackendCoordinator,
        IWintabInputBackend wintabBackend,
        Action<InputBackendLifecycleResult> applyTabletShutdownState);
}
