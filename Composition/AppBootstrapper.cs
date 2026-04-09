using System;
using TabletMidiPad.Core.Interaction;
using TabletMidiPad.Services.Input.Models;
using TabletMidiPad.Services.Midi;
using TabletMidiPad.Services.UiProjection.Models;
using TabletMidiPad.Services.Zone.Models;
using TabletMidiPad.ViewModels;
using TabletMidiPad.Views;

namespace TabletMidiPad.Composition;

/// <summary>
/// Application composition root (dependency injection wiring).
/// </summary>
internal static class AppBootstrapper
{
    /// <summary>
    /// Builds the application service container.
    /// </summary>
    /// <remarks>
    /// This is the single place where concrete implementations are chosen.
    /// </remarks>
    public static SimpleContainer BuildContainer()
    {
        var container = new SimpleContainer();

        // Core services.
        container.AddSingleton<IConfigStore>(_ => new JsonConfigStore(AppConfig.GetConfigPath()));
        container.AddSingleton<IRuntimeConfigService>(c => new RuntimeConfigService(c.GetRequiredService<IConfigStore>()));
        container.AddSingleton<IMidiEngine>(_ => new WinMmMidiEngine());
        container.AddSingleton<IWacomServiceController>(_ => new WacomServiceController());
        container.AddSingleton<IAppTrace>(_ => new TraceAppTrace());
        container.AddSingleton<IWintabInputBackend>(c => new WintabInputBackend(c.GetRequiredService<IAppTrace>()));
        container.AddSingleton<ITabletDevicePickerSyncService>(c => new TabletDevicePickerSyncService(c.GetRequiredService<IWintabInputBackend>()));
        container.AddSingleton<IWintabEventAdapter>(_ => new WintabEventAdapter());
        container.AddSingleton<IZoneConfigurationService>(_ => new ZoneConfigurationService());
        container.AddSingleton<IConfigValueParser>(_ => new ConfigValueParser());
        container.AddTransient<PreferencesViewModel>(_ => new PreferencesViewModel());
        container.AddSingleton<IPreferencesDialogService>(c => new PreferencesDialogService(
            c.GetRequiredService<IConfigValueParser>(),
            () => c.GetRequiredService<PreferencesViewModel>()));
        container.AddSingleton<IZoneEditorService>(c => new ZoneEditorService(c.GetRequiredService<IConfigValueParser>()));
        container.AddSingleton<IZoneResizeService>(_ => new ZoneResizeService());
        container.AddSingleton<IInputFallbackService>(_ => new InputFallbackService());
        container.AddSingleton<IInputInteractionOrchestrator>(c => new InputInteractionOrchestrator(
            c.GetRequiredService<IPadInteractionEngine>()));
        container.AddSingleton<IInputBackendStatusFormatter>(_ => new InputBackendStatusFormatter());
        container.AddSingleton<ISettingsWorkflowCoordinator>(c => new SettingsWorkflowCoordinator(
            c.GetRequiredService<IZoneConfigurationService>()));
        container.AddSingleton<IUiStateProjectionService>(_ => new UiStateProjectionService());
        container.AddSingleton<IInputBackendCoordinator>(_ => new InputBackendCoordinator());
        container.AddSingleton<IWacomServiceWorkflowCoordinator>(_ => new WacomServiceWorkflowCoordinator());
        container.AddSingleton<IPadInputControllerFactory>(c => new PadInputControllerFactory(
            c.GetRequiredService<ITabletDevicePickerSyncService>(),
            c.GetRequiredService<IInputBackendCoordinator>(),
            c.GetRequiredService<IWintabInputBackend>(),
            c.GetRequiredService<IWintabInputBridge>(),
            c.GetRequiredService<IInputFallbackService>(),
            c.GetRequiredService<IInputInteractionOrchestrator>(),
            c.GetRequiredService<IInputBackendStatusFormatter>()));
        container.AddSingleton<IStartupLifecycleService>(c => new StartupLifecycleService(
            c.GetRequiredService<IRuntimeConfigService>(),
            c.GetRequiredService<IInputBackendCoordinator>()));
        container.AddSingleton<IWintabInputBridge>(c => new WintabInputBridge(
            c.GetRequiredService<IWintabEventAdapter>(),
            c.GetRequiredService<IInputBackendCoordinator>()));
        container.AddSingleton<IPadPresentationCoordinator>(_ => new PadPresentationCoordinator());
        container.AddSingleton<IToolbarConfigService>(_ => new ToolbarConfigService());
        container.AddSingleton<IZoneActionWorkflowCoordinator>(c => new ZoneActionWorkflowCoordinator(
            c.GetRequiredService<IZoneConfigurationService>()));
        container.AddSingleton<IZoneOverlayRenderer>(_ => new ZoneOverlayRenderer());
        container.AddSingleton<IPadInteractionEngine>(_ => new PadInteractionEngine());
        container.AddSingleton<IPadInteractionOutcomeApplier>(c => new PadInteractionOutcomeApplier(
            c.GetRequiredService<IMidiEngine>()));
        container.AddSingleton<IMainViewModelFactory>(c => new MainViewModelFactory(
            c.GetRequiredService<IRuntimeConfigService>(),
            c.GetRequiredService<IZoneEditorService>(),
            c.GetRequiredService<IZoneActionWorkflowCoordinator>(),
            c.GetRequiredService<IToolbarConfigService>(),
            c.GetRequiredService<IUiStateProjectionService>()));

        container.AddTransient(CreateMainWindow);

        return container;
    }

    /// <summary>
    /// Builds the service container and resolves the main window. Prefer this at application startup so wiring
    /// stays in one place.
    /// </summary>
    public static (SimpleContainer Container, MainWindow MainWindow) CreateApplicationShell()
    {
        SimpleContainer container = BuildContainer();
        return (container, container.GetRequiredService<MainWindow>());
    }

    private static MainWindow CreateMainWindow(SimpleContainer c)
    {
        return new MainWindow(
            c.GetRequiredService<IRuntimeConfigService>(),
            c.GetRequiredService<IMidiEngine>(),
            c.GetRequiredService<IWacomServiceController>(),
            c.GetRequiredService<IWintabInputBackend>(),
            c.GetRequiredService<IZoneOverlayRenderer>(),
            c.GetRequiredService<IInputBackendStatusFormatter>(),
            c.GetRequiredService<IUiStateProjectionService>(),
            c.GetRequiredService<IWacomServiceWorkflowCoordinator>(),
            c.GetRequiredService<IStartupLifecycleService>(),
            c.GetRequiredService<IPadInputControllerFactory>(),
            c.GetRequiredService<IZoneResizeService>(),
            c.GetRequiredService<IPreferencesDialogService>(),
            c.GetRequiredService<IZoneConfigurationService>(),
            c.GetRequiredService<ISettingsWorkflowCoordinator>(),
            c.GetRequiredService<IPadPresentationCoordinator>(),
            c.GetRequiredService<IMainViewModelFactory>(),
            c.GetRequiredService<IPadInteractionOutcomeApplier>());
    }
}
