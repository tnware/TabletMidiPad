using System;
using System.Threading.Tasks;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services.Shell;

/// <summary>
/// Orchestrates main-window cold start (config load hook, MIDI connect presentation) and application exit shutdown.
/// </summary>
/// <remarks>
/// Cold start is orchestrated by <see cref="RunColdStartAsync"/> using an <see cref="IStartupShellHost"/> so the
/// view only implements host hooks. Runtime configuration ownership stays in <see cref="IRuntimeConfigService"/>.
/// Tablet rebind during the session stays on the view; window close uses
/// <see cref="ShutdownForExit"/> so exit sequencing lives in one place.
/// </remarks>
internal interface IStartupLifecycleService
{
    /// <summary>
    /// Loads persisted configuration into <see cref="IRuntimeConfigService.Current"/>.
    /// </summary>
    AppConfig LoadPersistedConfig();

    /// <summary>
    /// Full cold start: load config into the host, load UI, run post-config shell (Wacom + tablet), then MIDI sequence.
    /// </summary>
    Task RunColdStartAsync(
        IStartupShellHost host,
        IMidiEngine midiEngine,
        MainViewModel viewModel,
        Action<Action> marshalToUiThread,
        Action onMidiRoutingReady);

    /// <summary>
    /// Refreshes Wacom service menu state, then either runs full tablet initialization or applies a blocked state when
    /// the service is not running (Wintab requires it). Stylus/mouse-only mode always runs normal coordinator init.
    /// </summary>
    void RunPostConfigLoadShellPhase(StartupShellContext context);

    /// <summary>
    /// Builds lifecycle fields when Wintab is not probed because the Wacom service is unavailable.
    /// </summary>
    InputBackendLifecycleResult BuildTabletStackBlockedLifecycleResult(AppConfig config, WacomServiceStatus wacom);

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
        IWintabInputBackend wintabBackend,
        Action<InputBackendLifecycleResult> applyTabletShutdownState);
}
