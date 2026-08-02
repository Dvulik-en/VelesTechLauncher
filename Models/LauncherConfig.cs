using System;
using System.IO;

namespace VelesTech.Models;

/// <summary>
/// Пользовательский конфиг лаунчера. Сохраняется в %APPDATA%\VelesTech\launcher_config.json
/// </summary>
public class LauncherConfig
{
    /// <summary>Каталог, куда установлена сборка (mods, config, versions, runtime и т.д.)</summary>
    public string GameDirectory { get; set; } = DefaultGameDir();

    /// <summary>Сколько ОЗУ выделять JVM, в мегабайтах</summary>
    public int MaxRamMb { get; set; } = 4096;

    /// <summary>Полноэкранный режим</summary>
    public bool Fullscreen { get; set; } = false;

    /// <summary>Разрешение окна (используется только если Fullscreen = false)</summary>
    public int WindowWidth { get; set; } = 1280;
    public int WindowHeight { get; set; } = 720;

    /// <summary>Путь к своей Java (если пусто — используется runtime внутри сборки)</summary>
    public string CustomJavaPath { get; set; } = string.Empty;

    /// <summary>Флаг: клиент уже был скачан и распакован</summary>
    public bool ClientInstalled { get; set; } = false;

    private static string DefaultGameDir()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".velestech"
        );
    }
}
