using System;
using TabletMidiPad.Services;

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

        // Views (created by DI so they can receive view models/services later).
        container.AddTransient(c => new MainWindow(c.GetRequiredService<IConfigStore>()));

        return container;
    }
}

