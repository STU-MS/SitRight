using System;
using System.IO;
using System.Text.Json;
using SitRight.Models;

namespace SitRight.Services;

public sealed class ConfigService
{
    private readonly string _configPath;
    private AppConfig? _cachedConfig;

    public ConfigService(string? configDir = null)
    {
        var baseDir = configDir ?? AppDomain.CurrentDomain.BaseDirectory;
        _configPath = Path.Combine(baseDir, "config.json");
    }

    public AppConfig Load()
    {
        if (_cachedConfig is not null)
        {
            return _cachedConfig;
        }

        if (!File.Exists(_configPath))
        {
            _cachedConfig = new AppConfig();
            Save(_cachedConfig);
            return _cachedConfig;
        }

        var json = File.ReadAllText(_configPath);
        _cachedConfig = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        return _cachedConfig;
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
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
