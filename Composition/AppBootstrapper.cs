using System;
using TabletMidiPad.Core.Interaction;
using TabletMidiPad.Services;
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
        container.AddSingleton<IMidiEngine>(_ => new WinMmMidiEngine());
        container.AddSingleton<IWacomServiceController>(_ => new WacomServiceController());
        container.AddSingleton<IWintabInputBackend>(_ => new WintabInputBackend());
        container.AddSingleton<IWintabEventAdapter>(_ => new WintabEventAdapter());
        container.AddSingleton<IZoneConfigurationService>(_ => new ZoneConfigurationService());
        container.AddSingleton<IConfigValueParser>(_ => new ConfigValueParser());
        container.AddSingleton<IPreferencesDialogService>(c => new PreferencesDialogService(c.GetRequiredService<IConfigValueParser>()));
        container.AddSingleton<IZoneEditorService>(c => new ZoneEditorService(c.GetRequiredService<IConfigValueParser>()));
        container.AddSingleton<IZoneResizeService>(_ => new ZoneResizeService());
        container.AddSingleton<IInputFallbackService>(_ => new InputFallbackService());
        container.AddSingleton<IInputInteractionOrchestrator>(c => new InputInteractionOrchestrator(
            c.GetRequiredService<IPadInteractionEngine>()));
        container.AddSingleton<IInputBackendStatusFormatter>(_ => new InputBackendStatusFormatter());
        container.AddSingleton<IMainWindowWorkflowCoordinator>(_ => new MainWindowWorkflowCoordinator());
        container.AddSingleton<IMainWindowInputBackendCoordinator>(_ => new MainWindowInputBackendCoordinator());
        container.AddSingleton<IWacomServiceWorkflowCoordinator>(_ => new WacomServiceWorkflowCoordinator());
        container.AddSingleton<IMainWindowInputEventFlowCoordinator>(_ => new MainWindowInputEventFlowCoordinator());
        container.AddSingleton<IMainWindowLifecycleCoordinator>(_ => new MainWindowLifecycleCoordinator());
        container.AddSingleton<IMainWindowWintabInputBridge>(c => new MainWindowWintabInputBridge(
            c.GetRequiredService<IWintabEventAdapter>(),
            c.GetRequiredService<IMainWindowInputEventFlowCoordinator>()));
        container.AddSingleton<IMainWindowPadPresentationCoordinator>(_ => new MainWindowPadPresentationCoordinator());
        container.AddSingleton<IMainWindowViewStateMapper>(_ => new MainWindowViewStateMapper());
        container.AddSingleton<IZoneActionWorkflowCoordinator>(c => new ZoneActionWorkflowCoordinator(
            c.GetRequiredService<IZoneConfigurationService>()));
        container.AddSingleton<IZoneOverlayRenderer>(_ => new ZoneOverlayRenderer());
        container.AddSingleton<IPadInteractionEngine>(_ => new PadInteractionEngine());
        container.AddSingleton<IPadInteractionOutcomeApplier>(c => new PadInteractionOutcomeApplier(
            c.GetRequiredService<IMidiEngine>()));

        container.AddTransient<MainViewModel>(_ => new MainViewModel());

        // Views (created by DI so they can receive view models/services later).
        container.AddTransient(c => new MainWindow(
            c.GetRequiredService<IConfigStore>(),
            c.GetRequiredService<IMidiEngine>(),
            c.GetRequiredService<IWacomServiceController>(),
            c.GetRequiredService<IWintabInputBackend>(),
            c.GetRequiredService<IPreferencesDialogService>(),
            c.GetRequiredService<IZoneConfigurationService>(),
            c.GetRequiredService<IZoneEditorService>(),
            c.GetRequiredService<IZoneOverlayRenderer>(),
            c.GetRequiredService<IZoneResizeService>(),
            c.GetRequiredService<IInputFallbackService>(),
            c.GetRequiredService<IInputInteractionOrchestrator>(),
            c.GetRequiredService<IInputBackendStatusFormatter>(),
            c.GetRequiredService<IMainWindowWorkflowCoordinator>(),
            c.GetRequiredService<IMainWindowInputBackendCoordinator>(),
            c.GetRequiredService<IWacomServiceWorkflowCoordinator>(),
            c.GetRequiredService<IMainWindowInputEventFlowCoordinator>(),
            c.GetRequiredService<IMainWindowLifecycleCoordinator>(),
            c.GetRequiredService<IMainWindowWintabInputBridge>(),
            c.GetRequiredService<IMainWindowPadPresentationCoordinator>(),
            c.GetRequiredService<IMainWindowViewStateMapper>(),
            c.GetRequiredService<IZoneActionWorkflowCoordinator>(),
            c.GetRequiredService<MainViewModel>(),
            c.GetRequiredService<IPadInteractionOutcomeApplier>()));

        return container;
    }
}

