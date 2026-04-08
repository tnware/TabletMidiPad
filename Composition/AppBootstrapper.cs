using System;
using TabletMidiPad.Core.Interaction;
using TabletMidiPad.Services;
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
        container.AddSingleton<IPreferencesDialogService>(_ => new PreferencesDialogService());
        container.AddSingleton<IZoneConfigurationService>(_ => new ZoneConfigurationService());
        container.AddSingleton<IZoneEditorService>(_ => new ZoneEditorService());
        container.AddSingleton<IZoneResizeService>(_ => new ZoneResizeService());
        container.AddSingleton<IInputFallbackService>(_ => new InputFallbackService());
        container.AddSingleton<IInputBackendStatusFormatter>(_ => new InputBackendStatusFormatter());
        container.AddSingleton<IZoneOverlayRenderer>(_ => new ZoneOverlayRenderer());
        container.AddSingleton<IConfigValueParser>(_ => new ConfigValueParser());
        container.AddSingleton<IPadInteractionEngine>(_ => new PadInteractionEngine());

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
            c.GetRequiredService<IConfigValueParser>(),
            c.GetRequiredService<IPadInteractionEngine>(),
            c.GetRequiredService<IZoneResizeService>(),
            c.GetRequiredService<IInputFallbackService>(),
            c.GetRequiredService<IInputBackendStatusFormatter>()));

        return container;
    }
}

