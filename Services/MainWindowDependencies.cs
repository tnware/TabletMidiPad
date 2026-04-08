using System;
using TabletMidiPad.Core.Interaction;
using TabletMidiPad.Services;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services;

/// <summary>
/// A grouping of services required by the main window to reduce constructor bloat.
/// </summary>
internal sealed class MainWindowDependencies
{
    public IConfigStore ConfigStore { get; }
    public IMidiEngine MidiEngine { get; }
    public IWacomServiceController WacomServiceController { get; }
    public IWintabInputBackend WintabInputBackend { get; }
    public ITabletDevicePickerSyncService TabletDevicePickerSyncService { get; }
    public IPreferencesDialogService PreferencesDialogService { get; }
    public IZoneConfigurationService ZoneConfigurationService { get; }
    public IZoneEditorService ZoneEditorService { get; }
    public IZoneOverlayRenderer ZoneOverlayRenderer { get; }
    public IZoneResizeService ZoneResizeService { get; }
    public IInputFallbackService InputFallbackService { get; }
    public IInputInteractionOrchestrator InputInteractionOrchestrator { get; }
    public IInputBackendStatusFormatter InputBackendStatusFormatter { get; }
    public IMainWindowWorkflowCoordinator WorkflowCoordinator { get; }
    public IMainWindowInputBackendCoordinator InputBackendCoordinator { get; }
    public IWacomServiceWorkflowCoordinator WacomServiceWorkflowCoordinator { get; }
    public IMainWindowLifecycleCoordinator LifecycleCoordinator { get; }
    public IMainWindowStartupLifecycleService StartupLifecycle { get; }
    public IMainWindowWintabInputBridge WintabInputBridge { get; }
    public IMainWindowPadPresentationCoordinator PadPresentationCoordinator { get; }
    public IMainWindowToolbarSession ToolbarSession { get; }
    public IZoneActionWorkflowCoordinator ZoneActionWorkflowCoordinator { get; }
    public MainViewModel ViewModel { get; }
    public IPadInteractionOutcomeApplier InteractionOutcomeApplier { get; }
    public ISettingsService SettingsService { get; }

    public MainWindowDependencies(
        IConfigStore configStore,
        IMidiEngine midiEngine,
        IWacomServiceController wacomServiceController,
        IWintabInputBackend wintabInputBackend,
        ITabletDevicePickerSyncService tabletDevicePickerSyncService,
        IPreferencesDialogService preferencesDialogService,
        IZoneConfigurationService zoneConfigurationService,
        IZoneEditorService zoneEditorService,
        IZoneOverlayRenderer zoneOverlayRenderer,
        IZoneResizeService zoneResizeService,
        IInputFallbackService inputFallbackService,
        IInputInteractionOrchestrator inputInteractionOrchestrator,
        IInputBackendStatusFormatter inputBackendStatusFormatter,
        IMainWindowWorkflowCoordinator workflowCoordinator,
        IMainWindowInputBackendCoordinator inputBackendCoordinator,
        IWacomServiceWorkflowCoordinator wacomServiceWorkflowCoordinator,
        IMainWindowLifecycleCoordinator lifecycleCoordinator,
        IMainWindowStartupLifecycleService startupLifecycle,
        IMainWindowWintabInputBridge wintabInputBridge,
        IMainWindowPadPresentationCoordinator padPresentationCoordinator,
        IMainWindowToolbarSession toolbarSession,
        IZoneActionWorkflowCoordinator zoneActionWorkflowCoordinator,
        MainViewModel viewModel,
        IPadInteractionOutcomeApplier interactionOutcomeApplier,
        ISettingsService settingsService)
    {
        ConfigStore = configStore;
        MidiEngine = midiEngine;
        WacomServiceController = wacomServiceController;
        WintabInputBackend = wintabInputBackend;
        TabletDevicePickerSyncService = tabletDevicePickerSyncService;
        PreferencesDialogService = preferencesDialogService;
        ZoneConfigurationService = zoneConfigurationService;
        ZoneEditorService = zoneEditorService;
        ZoneOverlayRenderer = zoneOverlayRenderer;
        ZoneResizeService = zoneResizeService;
        InputFallbackService = inputFallbackService;
        InputInteractionOrchestrator = inputInteractionOrchestrator;
        InputBackendStatusFormatter = inputBackendStatusFormatter;
        WorkflowCoordinator = workflowCoordinator;
        InputBackendCoordinator = inputBackendCoordinator;
        WacomServiceWorkflowCoordinator = wacomServiceWorkflowCoordinator;
        LifecycleCoordinator = lifecycleCoordinator;
        StartupLifecycle = startupLifecycle;
        WintabInputBridge = wintabInputBridge;
        PadPresentationCoordinator = padPresentationCoordinator;
        ToolbarSession = toolbarSession;
        ZoneActionWorkflowCoordinator = zoneActionWorkflowCoordinator;
        ViewModel = viewModel;
        InteractionOutcomeApplier = interactionOutcomeApplier;
        SettingsService = settingsService;
    }
}
