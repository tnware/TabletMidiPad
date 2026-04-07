namespace TabletMidiPad.Services;

public interface ISettingsService
{
    AppConfig Load();
    void Save(AppConfig config);
    void Validate(AppConfig config);
    void AutoTileZones(AppConfig config);
}
