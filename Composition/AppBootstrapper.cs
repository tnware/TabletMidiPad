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
        container.AddSingleton<ISettingsService>(c => new SettingsService(c.GetRequiredService<IConfigStore>()));
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
        container.AddSingleton<IMainWindowWorkflowCoordinator>(_ => new MainWindowWorkflowCoordinator());
        container.AddSingleton<IMainWindowInputBackendCoordinator>(_ => new MainWindowInputBackendCoordinator());
        container.AddSingleton<IWacomServiceWorkflowCoordinator>(_ => new WacomServiceWorkflowCoordinator());
        container.AddSingleton<IMainWindowLifecycleCoordinator>(_ => new MainWindowLifecycleCoordinator());
        container.AddSingleton<IMainWindowStartupLifecycleService>(c => new MainWindowStartupLifecycleService(
            c.GetRequiredService<IMainWindowLifecycleCoordinator>()));
        container.AddSingleton<IMainWindowWintabInputBridge>(c => new MainWindowWintabInputBridge(
            c.GetRequiredService<IWintabEventAdapter>(),
            c.GetRequiredService<IMainWindowInputBackendCoordinator>()));
        container.AddSingleton<IMainWindowPadPresentationCoordinator>(_ => new MainWindowPadPresentationCoordinator());
        container.AddSingleton<IMainWindowToolbarSession>(_ => new MainWindowToolbarSession());
        container.AddSingleton<IZoneActionWorkflowCoordinator>(c => new ZoneActionWorkflowCoordinator(
            c.GetRequiredService<IZoneConfigurationService>()));
        container.AddSingleton<IZoneOverlayRenderer>(_ => new ZoneOverlayRenderer());
        container.AddSingleton<IPadInteractionEngine>(_ => new PadInteractionEngine());
        container.AddSingleton<IPadInteractionOutcomeApplier>(c => new PadInteractionOutcomeApplier(
            c.GetRequiredService<IMidiEngine>()));

        container.AddSingleton<ZoneListViewModel>(c => new ZoneListViewModel(
            c.GetRequiredService<ISettingsService>(),
            c.GetRequiredService<IZoneActionWorkflowCoordinator>(),
            c.GetRequiredService<IZoneEditorService>()));
        container.AddSingleton<ZoneEditorViewModel>(c => new ZoneEditorViewModel(
            c.GetRequiredService<ISettingsService>(),
            c.GetRequiredService<IZoneEditorService>(),
            c.GetRequiredService<ZoneListViewModel>()));
        container.AddSingleton<IMainWindowPadInputSession>(c => new MainWindowPadInputSession(
            new MainWindowPadInputSessionHostProxy(c),
            c.GetRequiredService<ITabletDevicePickerSyncService>(),
            c.GetRequiredService<IMainWindowInputBackendCoordinator>(),
            c.GetRequiredService<IMainWindowLifecycleCoordinator>(),
            c.GetRequiredService<IWintabInputBackend>(),
            c.GetRequiredService<IMainWindowWintabInputBridge>(),
            c.GetRequiredService<IInputFallbackService>(),
            c.GetRequiredService<IInputInteractionOrchestrator>(),
            c.GetRequiredService<IInputBackendStatusFormatter>()));
        container.AddSingleton<IMainWindowSettingsSession>(c => new MainWindowSettingsSession(
            new MainWindowSettingsSessionHostProxy(c),
            c.GetRequiredService<IMainWindowWorkflowCoordinator>(),
            c.GetRequiredService<IPreferencesDialogService>(),
            c.GetRequiredService<IZoneConfigurationService>(),
            c.GetRequiredService<IConfigStore>(),
            c.GetRequiredService<IMidiEngine>(),
            c.GetRequiredService<IMainWindowPadInputSession>(),
            c.GetRequiredService<ISettingsService>()));

        container.AddTransient<MainViewModel>(c => new MainViewModel(
            c.GetRequiredService<ZoneListViewModel>(),
            c.GetRequiredService<ZoneEditorViewModel>(),
            c.GetRequiredService<ISettingsService>(),
            c.GetRequiredService<IMainWindowSettingsSession>(),
            c.GetRequiredService<IMainWindowToolbarSession>(),
            c.GetRequiredService<IMainWindowPadInputSession>()));

        container.AddSingleton<MainWindowDependencies>(c => new MainWindowDependencies(
            c.GetRequiredService<IConfigStore>(),
            c.GetRequiredService<IMidiEngine>(),
            c.GetRequiredService<IWacomServiceController>(),
            c.GetRequiredService<IWintabInputBackend>(),
            c.GetRequiredService<ITabletDevicePickerSyncService>(),
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
            c.GetRequiredService<IMainWindowLifecycleCoordinator>(),
            c.GetRequiredService<IMainWindowStartupLifecycleService>(),
            c.GetRequiredService<IMainWindowWintabInputBridge>(),
            c.GetRequiredService<IMainWindowPadPresentationCoordinator>(),
            c.GetRequiredService<IMainWindowToolbarSession>(),
            c.GetRequiredService<IZoneActionWorkflowCoordinator>(),
            c.GetRequiredService<MainViewModel>(),
            c.GetRequiredService<IPadInteractionOutcomeApplier>(),
            c.GetRequiredService<ISettingsService>()));

        container.AddSingleton(c => new MainWindow(c.GetRequiredService<MainWindowDependencies>()));

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

    private sealed class MainWindowSettingsSessionHostProxy : IMainWindowSettingsSessionHost
    {
        private readonly SimpleContainer _c;
        private MainWindow MainWindow => _c.GetRequiredService<MainWindow>();

        public MainWindowSettingsSessionHostProxy(SimpleContainer c) => _c = c;

        public System.Windows.Window OwnerWindow => MainWindow;
        public AppConfig Config => MainWindow.SettingsServiceRef.Config;
        public void ReplaceConfig(AppConfig config) => MainWindow.SettingsServiceRef.UpdateConfig(config, false);
        public PadInputSessionState PadState => MainWindow.PadStateRef;
        public void CommitCurrentZoneEdits() => MainWindow.ZoneEditorSessionRef.CommitCurrentZoneEdits();
        public void SetSettingsResult(string message, SettingsResultKind kind) => MainWindow.SetSettingsResultRef(message, kind);
        public void UpdateUiForCurrentConfig() => MainWindow.ZoneEditorSessionRef.UpdateUiForCurrentConfig();
        public void RefreshPadZoneOverlay() => MainWindow.RefreshPadZoneOverlayRef();
        public void SyncToolbar() => MainWindow.SyncToolbarRef();
        public void LoadConfigIntoUi(AppConfig config) => MainWindow.ZoneEditorSessionRef.LoadConfigIntoUi(config);
    }

    private sealed class MainWindowPadInputSessionHostProxy : IMainWindowPadInputSessionHost
    {
        private readonly SimpleContainer _c;
        private MainWindow MainWindow => _c.GetRequiredService<MainWindow>();

        public MainWindowPadInputSessionHostProxy(SimpleContainer c) => _c = c;

        public System.Windows.Controls.Canvas Pad => MainWindow.PadRef;
        public IPadInputChromeSurface PadChrome => MainWindow.PadSurfaceRef;
        public AppConfig Config => MainWindow.SettingsServiceRef.Config;
        public MainViewModel ViewModel => MainWindow.ViewModelRef;
        public InteractionState InteractionState => MainWindow.InteractionStateRef;
        public IPadInteractionOutcomeApplier OutcomeApplier => MainWindow.OutcomeApplierRef;
        public PadInputSessionState PadState => MainWindow.PadStateRef;
        public int SelectedZoneIndex { get => MainWindow.ViewModelRef.SelectedZoneIndex; set => MainWindow.ViewModelRef.SelectedZoneIndex = value; }
        public bool IsLoadingUi => MainWindow.IsLoadingUiRef;
        public void ApplyInteractionResult(InteractionResult result) => MainWindow.ApplyInteractionResultRef(result);
        public void RefreshPadZoneOverlay() => MainWindow.RefreshPadZoneOverlayRef();
        public void UpdateInputBackendStatus() => MainWindow.UpdateInputBackendStatusRef();
        public void OnRebindCompletedRefreshRouting() => MainWindow.ZoneEditorSessionRef.UpdateUiForCurrentConfig();
        public void BeginPopulateTabletDeviceList() => MainWindow.BeginPopulateTabletDeviceListRef();
        public void EndPopulateTabletDeviceList() => MainWindow.EndPopulateTabletDeviceListRef();
    }
}
