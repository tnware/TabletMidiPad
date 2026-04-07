using System;
using System.IO;
using System.Text.Json;

namespace TabletMidiPad.Services;

/// <summary>
/// Loads and saves <see cref="AppConfig"/> to a JSON file.
/// </summary>
internal sealed class JsonConfigStore : IConfigStore
{
    private readonly string _path;

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
            var config = JsonSerializer.Deserialize<AppConfig>(json);
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

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        File.WriteAllText(_path, JsonSerializer.Serialize(config, options));
    }
}

