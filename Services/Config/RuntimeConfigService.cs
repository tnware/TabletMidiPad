using System;

namespace TabletMidiPad.Services;

/// <summary>
/// Default runtime configuration owner backed by <see cref="IConfigStore"/>.
/// </summary>
internal sealed class RuntimeConfigService : IRuntimeConfigService
{
    private readonly IConfigStore _configStore;

    public RuntimeConfigService(IConfigStore configStore)
    {
        _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        Current = AppConfig.Default;
    }

    public AppConfig Current { get; private set; }

    public event EventHandler? CurrentChanged;

    public AppConfig Load()
    {
        Current = AppConfig.NormalizeLoaded(_configStore.Load());
        CurrentChanged?.Invoke(this, EventArgs.Empty);
        return Current;
    }

    public void Replace(AppConfig config)
    {
        Current = AppConfig.NormalizeLoaded(config ?? AppConfig.Default);
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ResetToDefaults() => Replace(AppConfig.Default);

    public void SaveCurrent() => _configStore.Save(Current);
}
