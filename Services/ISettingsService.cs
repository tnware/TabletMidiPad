using System;
using System.Collections.Generic;
using TabletMidiPad.Models;

namespace TabletMidiPad.Services;

public interface ISettingsService
{
    AppConfig Config { get; }
    void Save();
    void Load();
    void ResetToDefaults();
    AppConfig CloneConfig();
    void ApplyConfig(AppConfig config);
}
