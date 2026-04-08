using System;
using System.Threading.Tasks;

namespace TabletMidiPad.Services;

/// <summary>
/// Default lifecycle sequencing for <see cref="Views.MainWindow"/>.
/// </summary>
internal sealed class MainWindowLifecycleCoordinator : IMainWindowLifecycleCoordinator
{
    /// <inheritdoc />
    public AppConfig LoadStartupConfig(IConfigStore store)
    {
        return store.Load();
    }

    /// <inheritdoc />
    public async Task<MidiInitializationResult> TryInitializeMidiAsync(
        IMidiEngine midiEngine,
        AppConfig config,
        Action<string>? onProgress)
    {
        try
        {
            await midiEngine.InitializeAsync(config, onProgress);
            midiEngine.ClearCachedValues();
            return new MidiInitializationResult(Succeeded: true, ErrorMessage: null);
        }
        catch (Exception ex)
        {
            return new MidiInitializationResult(Succeeded: false, ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc />
    public void ShutdownMidiOutput(IMidiEngine midiEngine, AppConfig config)
    {
        midiEngine.Shutdown(config);
    }

    /// <inheritdoc />
    public InputBackendLifecycleResult ShutdownTabletInput(
        IMainWindowInputBackendCoordinator inputBackendCoordinator,
        IWintabInputBackend wintabBackend)
    {
        return inputBackendCoordinator.Shutdown(wintabBackend);
    }
}
