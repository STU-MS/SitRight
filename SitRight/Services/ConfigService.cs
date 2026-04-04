using System.IO;
using System.Text.Json;
using SitRight.Models;

namespace SitRight.Services;

public class ConfigService
{
    private readonly string _configPath;
    private AppConfig? _cachedConfig;

    public ConfigService(string? configPath = null)
    {
        _configPath = configPath ?? Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "config.json");
    }

    public AppConfig Load()
    {
        if (_cachedConfig != null)
            return _cachedConfig;

        if (File.Exists(_configPath))
        {
            var json = File.ReadAllText(_configPath);
            _cachedConfig = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        else
        {
            _cachedConfig = new AppConfig();
            Save(_cachedConfig);
        }

        return _cachedConfig;
    }

    public void Save(AppConfig config)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(_configPath, json);
        _cachedConfig = config;
    }

    public void Update(Action<AppConfig> updateAction)
    {
        var config = Load();
        updateAction(config);
        Save(config);
    }
}
