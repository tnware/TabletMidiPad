using System;

namespace TabletMidiPad.Services;

/// <summary>
/// Default implementation of <see cref="ISettingsService"/>.
/// </summary>
internal sealed class SettingsService : ISettingsService
{
    private readonly IConfigStore _configStore;
    private AppConfig _config = AppConfig.Default;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsService"/> class.
    /// </summary>
    /// <param name="configStore">The store used for persistence.</param>
    public SettingsService(IConfigStore configStore)
    {
        _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
    }

    /// <inheritdoc />
    public AppConfig Config => _config;

    /// <inheritdoc />
    public event EventHandler? SettingsUpdated;

    /// <inheritdoc />
    public void Load()
    {
        _config = _configStore.Load();
        NotifyUpdated();
    }

    /// <inheritdoc />
    public void Save()
    {
        _configStore.Save(_config);
        NotifyUpdated();
    }

    /// <inheritdoc />
    public void UpdateConfig(AppConfig config, bool save = true)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        if (save)
        {
            Save();
        }
        else
        {
            NotifyUpdated();
        }
    }

    private void NotifyUpdated()
    {
        SettingsUpdated?.Invoke(this, EventArgs.Empty);
    }
}
