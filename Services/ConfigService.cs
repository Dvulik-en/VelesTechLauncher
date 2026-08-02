using System;
using System.IO;
using System.Text.Json;
using VelesTech.Models;

namespace VelesTech.Services;

/// <summary>
/// Загрузка/сохранение конфига лаунчера в %APPDATA%\VelesTech\launcher_config.json
/// </summary>
public static class ConfigService
{
    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VelesTech");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "launcher_config.json");

    private static LauncherConfig? _cache;

    /// <summary>Текущий конфиг (кешируется в памяти)</summary>
    public static LauncherConfig Current
    {
        get
        {
            _cache ??= Load();
            return _cache;
        }
    }

    public static LauncherConfig Load()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<LauncherConfig>(json);
                _cache = config ?? new LauncherConfig();
                return _cache;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigService] Load error: {ex.Message}");
        }

        _cache = new LauncherConfig();
        return _cache;
    }

    public static void Save(LauncherConfig config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(ConfigPath, json);
            _cache = config;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigService] Save error: {ex.Message}");
        }
    }

    public static void Save() => Save(Current);
}
