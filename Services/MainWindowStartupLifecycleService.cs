using System;
using System.Threading.Tasks;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services;

/// <summary>
/// Default startup/exit lifecycle orchestration for <see cref="Views.MainWindow"/>.
/// </summary>
internal sealed class MainWindowStartupLifecycleService : IMainWindowStartupLifecycleService
{
    private readonly IMainWindowLifecycleCoordinator _lifecycleCoordinator;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowStartupLifecycleService"/> class.
    /// </summary>
    public MainWindowStartupLifecycleService(IMainWindowLifecycleCoordinator lifecycleCoordinator)
    {
        _lifecycleCoordinator = lifecycleCoordinator ?? throw new ArgumentNullException(nameof(lifecycleCoordinator));
    }

    /// <inheritdoc />
    public AppConfig LoadPersistedConfig(IConfigStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        return _lifecycleCoordinator.LoadStartupConfig(store);
    }

    /// <inheritdoc />
    public async Task RunStartupMidiSequenceAsync(
        IMidiEngine midiEngine,
        AppConfig config,
        MainViewModel viewModel,
        Action<Action> marshalToUiThread,
        Action onMidiRoutingReady)
    {
        ArgumentNullException.ThrowIfNull(midiEngine);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(marshalToUiThread);
        ArgumentNullException.ThrowIfNull(onMidiRoutingReady);

        viewModel.IsMidiStatusPanelVisible = true;
        viewModel.IsMidiProgressVisible = true;
        viewModel.MidiStatusSeverity = MidiStatusSeverity.Normal;
        viewModel.MidiStatusText = "Connecting MIDI...";
        await Task.Yield();

        MidiInitializationResult midiResult = await _lifecycleCoordinator.TryInitializeMidiAsync(
            midiEngine,
            config,
            text => marshalToUiThread(() => viewModel.MidiStatusText = text));

        if (midiResult.Succeeded)
        {
            marshalToUiThread(() =>
            {
                onMidiRoutingReady();
                viewModel.IsMidiStatusPanelVisible = false;
            });
        }
        else
        {
            marshalToUiThread(() =>
            {
                viewModel.IsMidiProgressVisible = false;
                viewModel.MidiStatusSeverity = MidiStatusSeverity.Error;
                viewModel.MidiStatusText = "MIDI init failed";
                viewModel.StatusText = "MIDI init failed";
                viewModel.ValueText = midiResult.ErrorMessage ?? string.Empty;
            });
        }
    }

    /// <inheritdoc />
    public void ShutdownForExit(
        IMidiEngine midiEngine,
        AppConfig config,
        IMainWindowInputBackendCoordinator inputBackendCoordinator,
        IWintabInputBackend wintabBackend,
        Action<InputBackendLifecycleResult> applyTabletShutdownState)
    {
        ArgumentNullException.ThrowIfNull(midiEngine);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(inputBackendCoordinator);
        ArgumentNullException.ThrowIfNull(wintabBackend);
        ArgumentNullException.ThrowIfNull(applyTabletShutdownState);

        _lifecycleCoordinator.ShutdownMidiOutput(midiEngine, config);
        InputBackendLifecycleResult tabletShutdown = _lifecycleCoordinator.ShutdownTabletInput(
            inputBackendCoordinator,
            wintabBackend);
        applyTabletShutdownState(tabletShutdown);
        wintabBackend.Dispose();
    }
}
