using TabletMidiPad.Models;

namespace TabletMidiPad.Services;

public interface ISettingsService
{
    AppConfig Config { get; }
    void Load();
    void Save();
    void ResetToDefault();
}
