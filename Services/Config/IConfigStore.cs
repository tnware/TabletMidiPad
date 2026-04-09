using System;

namespace TabletMidiPad.Services.Config;

/// <summary>
/// Loads and saves the application configuration.
/// </summary>
public interface IConfigStore
{
    /// <summary>
    /// Loads configuration from persistent storage.
    /// </summary>
    /// <returns>The loaded configuration, or defaults when unavailable or invalid.</returns>
    AppConfig Load();

    /// <summary>
    /// Saves configuration to persistent storage.
    /// </summary>
    /// <param name="config">The configuration to save.</param>
    void Save(AppConfig config);
}

