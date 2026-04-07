using System;
using TabletMidiPad.Models;

namespace TabletMidiPad.Services;

public interface ISettingsService
{
    AppConfig Config { get; }
    event Action? SettingsUpdated;
    void Load();
    void Save();
    void ResetToDefault();
    void NotifyUpdated();
}
