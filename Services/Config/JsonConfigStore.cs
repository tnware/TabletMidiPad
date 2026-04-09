using System.IO;
using System.Text.Json;

namespace TabletMidiPad.Services.Config;

/// <summary>
/// Loads and saves <see cref="AppConfig"/> to a JSON file.
/// </summary>
internal sealed class JsonConfigStore : IConfigStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions WriteIndentedOptions = new()
    {
        WriteIndented = true
    };

    public JsonConfigStore(string path)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
    }

    public AppConfig Load()
    {
        if (!File.Exists(_path))
        {
            return AppConfig.Default;
        }

        try
        {
            string json = File.ReadAllText(_path);
            AppConfig? config = JsonSerializer.Deserialize<AppConfig>(json);
            if (config is null)
            {
                return AppConfig.Default;
            }

            return AppConfig.NormalizeLoaded(config);
        }
        catch
        {
            return AppConfig.Default;
        }
    }

    public void Save(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        File.WriteAllText(_path, JsonSerializer.Serialize(config, WriteIndentedOptions));
    }
}

