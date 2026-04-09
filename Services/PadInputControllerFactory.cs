using System;

namespace TabletMidiPad.Services;

/// <summary>
/// Default factory for <see cref="PadInputController"/>.
/// </summary>
internal sealed class PadInputControllerFactory : IPadInputControllerFactory
{
    private readonly ITabletDevicePickerSyncService _tabletDevicePickerSyncService;
    private readonly IInputBackendCoordinator _inputBackendCoordinator;
    private readonly ILifecycleCoordinator _lifecycleCoordinator;
    private readonly IWintabInputBackend _wintabInputBackend;
    private readonly IWintabInputBridge _wintabInputBridge;
    private readonly IInputFallbackService _inputFallbackService;
    private readonly IInputInteractionOrchestrator _inputInteractionOrchestrator;
    private readonly IInputBackendStatusFormatter _inputBackendStatusFormatter;

    public PadInputControllerFactory(
        ITabletDevicePickerSyncService tabletDevicePickerSyncService,
        IInputBackendCoordinator inputBackendCoordinator,
        ILifecycleCoordinator lifecycleCoordinator,
        IWintabInputBackend wintabInputBackend,
        IWintabInputBridge wintabInputBridge,
        IInputFallbackService inputFallbackService,
        IInputInteractionOrchestrator inputInteractionOrchestrator,
        IInputBackendStatusFormatter inputBackendStatusFormatter)
    {
        _tabletDevicePickerSyncService = tabletDevicePickerSyncService ?? throw new ArgumentNullException(nameof(tabletDevicePickerSyncService));
        _inputBackendCoordinator = inputBackendCoordinator ?? throw new ArgumentNullException(nameof(inputBackendCoordinator));
        _lifecycleCoordinator = lifecycleCoordinator ?? throw new ArgumentNullException(nameof(lifecycleCoordinator));
        _wintabInputBackend = wintabInputBackend ?? throw new ArgumentNullException(nameof(wintabInputBackend));
        _wintabInputBridge = wintabInputBridge ?? throw new ArgumentNullException(nameof(wintabInputBridge));
        _inputFallbackService = inputFallbackService ?? throw new ArgumentNullException(nameof(inputFallbackService));
        _inputInteractionOrchestrator = inputInteractionOrchestrator ?? throw new ArgumentNullException(nameof(inputInteractionOrchestrator));
        _inputBackendStatusFormatter = inputBackendStatusFormatter ?? throw new ArgumentNullException(nameof(inputBackendStatusFormatter));
    }

    public IPadInputController Create(IPadInputControllerHost host)
    {
        return new PadInputController(
            host,
            _tabletDevicePickerSyncService,
            _inputBackendCoordinator,
            _lifecycleCoordinator,
            _wintabInputBackend,
            _wintabInputBridge,
            _inputFallbackService,
            _inputInteractionOrchestrator,
            _inputBackendStatusFormatter);
    }
}

