using System;

namespace TabletMidiPad.Services;

/// <summary>
/// Owns the active runtime configuration and its load/replace/reset persistence lifecycle.
/// </summary>
internal interface IRuntimeConfigService
{
    AppConfig Current { get; }

    event EventHandler? CurrentChanged;

    AppConfig Load();

    void Replace(AppConfig config);

    void ResetToDefaults();

    void SaveCurrent();
}
