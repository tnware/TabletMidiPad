using System;
using System.Threading.Tasks;

namespace TabletMidiPad.Services;

/// <summary>
/// Sequences startup/shutdown steps for the main window without WPF dependencies.
/// </summary>
internal interface IMainWindowLifecycleCoordinator
{
    /// <summary>
    /// Loads persisted configuration for startup.
    /// </summary>
    AppConfig LoadStartupConfig(IConfigStore store);

    /// <summary>
    /// Initializes MIDI routing/output; clears cached send state on success.
    /// </summary>
    Task<MidiInitializationResult> TryInitializeMidiAsync(
        IMidiEngine midiEngine,
        AppConfig config,
        Action<string>? onProgress);

    /// <summary>
    /// Shuts down MIDI output for the current config (window close).
    /// </summary>
    void ShutdownMidiOutput(IMidiEngine midiEngine, AppConfig config);

    /// <summary>
    /// Shuts down the tablet/Wintab input backend; returns lifecycle fields for view state sync.
    /// </summary>
    InputBackendLifecycleResult ShutdownTabletInput(
        IMainWindowInputBackendCoordinator inputBackendCoordinator,
        IWintabInputBackend wintabBackend);
}

/// <summary>
/// Outcome of async MIDI initialization during startup.
/// </summary>
internal readonly record struct MidiInitializationResult(bool Succeeded, string? ErrorMessage);
