using System;

namespace TabletMidiPad.Services;

/// <summary>
/// Manages application settings, providing access to the current configuration,
/// persistence, and change notification.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the current application configuration.
    /// </summary>
    AppConfig Config { get; }

    /// <summary>
    /// Occurs when the settings have been updated and saved.
    /// </summary>
    event EventHandler? SettingsUpdated;

    /// <summary>
    /// Loads settings from the store.
    /// </summary>
    void Load();

    /// <summary>
    /// Saves the current configuration to the store and notifies listeners.
    /// </summary>
    void Save();

    /// <summary>
    /// Replaces the current configuration with a new one (e.g., from a dialog or reset)
    /// and optionally saves it.
    /// </summary>
    /// <param name="config">The new configuration.</param>
    /// <param name="save">Whether to save immediately.</param>
    void UpdateConfig(AppConfig config, bool save = true);
}
