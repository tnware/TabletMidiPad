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
    public void RunPostConfigLoadShellPhase(MainWindowPostConfigLoadShellContext context)
    {
        WacomServiceStatus wacom = context.GetWacomServiceStatus();
        context.ApplyWacomServiceMenuState(wacom);
        if (ShouldProbeWintabStack(context.Config, wacom))
        {
            context.InitializeTabletWhenReady();
        }
        else
        {
            context.ApplyTabletBlockedByWacomService(wacom);
        }
    }

    /// <inheritdoc />
    public InputBackendLifecycleResult BuildTabletStackBlockedLifecycleResult(AppConfig config, WacomServiceStatus wacom)
    {
        ArgumentNullException.ThrowIfNull(config);
        string diagnostics = BuildWintabBlockedDiagnostics(wacom);
        string mode = InputBackendModeNormalizer.Normalize(config.InputBackendMode);
        if (string.Equals(mode, "Wintab Only", StringComparison.Ordinal))
        {
            return new InputBackendLifecycleResult(
                UseWintab: false,
                ExpressKeyCount: 0,
                LastInputBackend: "Wintab unavailable",
                WintabDiagnostics: diagnostics);
        }

        return new InputBackendLifecycleResult(
            UseWintab: false,
            ExpressKeyCount: 0,
            LastInputBackend: "Stylus/Mouse Fallback",
            WintabDiagnostics: diagnostics);
    }

    /// <summary>
    /// When false, the view should not call <see cref="IWintabInputBackend.Initialize"/> for Auto/Wintab-only modes
    /// because the Wacom service is not running.
    /// </summary>
    internal static bool ShouldProbeWintabStack(AppConfig config, WacomServiceStatus wacom)
    {
        ArgumentNullException.ThrowIfNull(config);
        string mode = InputBackendModeNormalizer.Normalize(config.InputBackendMode);
        if (string.Equals(mode, "Stylus/Mouse Only", StringComparison.Ordinal))
        {
            return true;
        }

        return wacom.IsServiceRunning;
    }

    private static string BuildWintabBlockedDiagnostics(WacomServiceStatus wacom)
    {
        if (wacom.IsServiceRunning)
        {
            return "Wacom service is running; tablet stack was skipped unexpectedly.";
        }

        if (wacom.CanStart)
        {
            return "Wacom Professional Service is not running. Start it from Device → Start Wacom Service, then use Reconnect Pen Input.";
        }

        if (wacom.StatusText.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return "Wacom Professional Service was not found. Install Wacom drivers to use Wintab.";
        }

        return "Wacom tablet service is unavailable (WTabletServicePro). Wintab cannot start until the service is running.";
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
