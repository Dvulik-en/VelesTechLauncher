using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Platform;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Version;
using VelesTech.Models;
using VelesTech.Services;

namespace VelesTech.Launcher;

/// <summary>
/// Запуск NeoForge 1.21.1 через CmlLib.Core.
///
/// ВНИМАНИЕ 1: NeoForge для Minecraft 1.21.x работает ТОЛЬКО с Java 21.
///
/// ВНИМАНИЕ 2: секретный ингредиент — 8 JAR-ов из блока "-p" (module-path) в JSON
///             (bootstraplauncher, securejarhandler, asm-*, JarJarFileSystems)
///             ДОЛЖНЫ идти ТОЛЬКО через --module-path и НЕ должны попадать в classpath.
///             Иначе URL.setURLStreamHandlerFactory вызывается дважды →
///             "java.lang.Error: factory already defined". Мы удаляем их из classpath
///             постобработкой аргументов процесса.
/// </summary>
public class MinecraftLauncher
{
    private readonly ModpackManifest _manifest;
    private readonly LauncherConfig _config;
    private readonly AccountData _account;

    public event Action<string>? OnStatus;
    public event Action<string>? OnGameLog;

    // JAR-ы, которые обязаны быть на MODULE-PATH и НЕ в classpath.
    // Проверяем по подстроке (пути в JSON: cpw/mods/bootstraplauncher/2.0.2/... и т.д.)
    private static readonly string[] ModulePathMarkers =
    {
        "bootstraplauncher",
        "securejarhandler",
        "asm-commons",
        "asm-util",
        "asm-analysis",
        "asm-tree",
        "JarJarFileSystems",
    };

    // "asm-9.8.jar" тоже должен уйти на modulepath — но простое "asm" совпадёт со многими
    // не тем чем надо, поэтому матчим точнее ниже (StartsWith("asm-") && EndsWith(".jar")
    // с проверкой на цифры в номере версии).

    public MinecraftLauncher(ModpackManifest manifest, LauncherConfig config, AccountData account)
    {
        _manifest = manifest;
        _config = config;
        _account = account;
    }

    public async Task LaunchAsync()
    {
        // 0. Каталоги
        var path = new MinecraftPath(_config.GameDirectory);
        var launcher = new CMLauncher(path);
    

        launcher.FileChanged += e =>
        {
            OnStatus?.Invoke($"[{e.FileKind}] {e.FileName} ({e.ProgressedFileCount}/{e.TotalFileCount})");
        };
        launcher.ProgressChanged += (s, e) =>
        {
            OnStatus?.Invoke($"Загрузка: {e.ProgressPercentage}%");
        };

        string versionToLaunch = _manifest.VersionFolder;
        string versionJsonPath = Path.Combine(
            _config.GameDirectory, "versions", _manifest.VersionFolder, _manifest.VersionJson);

        // 1. Если пользовательского NeoForge-манифеста в архиве нет — генерим
        if (!File.Exists(versionJsonPath))
        {
            OnStatus?.Invoke($"Не найден {_manifest.VersionJson} — генерирую манифест NeoForge...");
            EnsureForgeManifest();
        }
        else
        {
            OnStatus?.Invoke($"Используется существующий JSON: {versionJsonPath}");
        }

        // 2. Ваниль (1.21.1) — нужна для inheritsFrom (её CmlLib может скачать сам)
        OnStatus?.Invoke($"Проверка Minecraft {_manifest.MinecraftVersion}...");
        var vanillaVersion = await launcher.GetVersionAsync(_manifest.MinecraftVersion);

        // 3. Версия сборки — только парсим, НИКАКИХ CheckAndDownloadAsync!
        //    (иначе CmlLib начнёт качать "FTB StoneBlock 4 ftb-stoneblock-4-1.16.0.jar"
        //     с серверов Mojang — которые про такую версию не знают, скачается 0 байт).
        MVersion versionObj;
        try
        {
            OnStatus?.Invoke($"Проверка манифеста {versionToLaunch}...");
            versionObj = await launcher.GetVersionAsync(versionToLaunch);
        }
        catch (Exception ex)
        {
            OnStatus?.Invoke($"Манифест сборки испорчен ({ex.Message}). Fallback → {_manifest.MinecraftVersion}");
            versionObj = vanillaVersion;
            versionToLaunch = _manifest.MinecraftVersion;
        }

        // 4. Проверка Java (major-version должна быть 21)
        string javaPath = ResolveJavaPath();
        string javaVersion = DetectJavaMajorVersion(javaPath);
        OnStatus?.Invoke($"Java: {javaPath} (major version: {javaVersion})");

        if (int.TryParse(javaVersion, out int major) && major < 21)
        {
            throw new InvalidOperationException(
                $"ОШИБКА JAVA: обнаружена Java {major}, но NeoForge {_manifest.ForgeVersion} " +
                $"для Minecraft {_manifest.MinecraftVersion} требует Java 21. " +
                $"Скачайте Zulu/Adoptium JRE 21 и распакуйте в папку {Path.Combine(_config.GameDirectory, "runtime")}");
        }

        // 5. Сессия (пиратка — offline режим)
        var session = new MSession
        {
            Username = _account.Username,
            UUID = string.IsNullOrEmpty(_account.Uuid)
                ? Guid.NewGuid().ToString("N")
                : _account.Uuid,
            AccessToken = "0".PadRight(32, '0'),
            UserType = "mojang",
            ClientToken = "velestech-launcher"
        };

        // 6. НИКАКИХ JVMArguments!
        //    JSON уже содержит правильные --add-opens=java.base/java.lang.invoke=cpw.mods.securejarhandler
        //    (именно =cpw.mods.securejarhandler, а НЕ =ALL-UNNAMED — иначе securejarhandler
        //     не увидит открытые пакеты). Наши свои --add-opens создавали конфликт.
        var launchOption = new MLaunchOption
        {
            Session = session,
            MaximumRamMb = _config.MaxRamMb,
            MinimumRamMb = Math.Min(1024, _config.MaxRamMb / 2),
            ServerIp = _manifest.ServerIp,
            ServerPort = _manifest.ServerPort,
            FullScreen = _config.Fullscreen,
            ScreenWidth = _config.WindowWidth,
            ScreenHeight = _config.WindowHeight,
            VersionType = "release",
            GameLauncherName = "VelesTech",
            GameLauncherVersion = "1.0",
            JavaPath = javaPath
        };

        // На случай если у пользователя JAVA_TOOL_OPTIONS выставлен глобально — уберём.
        //Environment.SetEnvironmentVariable("JAVA_TOOL_OPTIONS", null);
        //Environment.SetEnvironmentVariable("_JAVA_OPTIONS", null);

        OnStatus?.Invoke("Сборка команды запуска...");
        // Убедитесь, что версия NeoForge загружается корректно
        var process = await launcher.CreateProcessAsync("FTB_StoneBlock_4_1.16.0", launchOption, checkAndDownload: true);

        // 7. ГЛАВНЫЙ ФИКС: удаляем modulepath-JAR из classpath
        //    CmlLib собирает ${classpath} из ВСЕХ библиотек JSON. Но 8 JAR-ов ниже
        //    должны идти ТОЛЬКО через -p (модуль-path). Иначе URL.setURLStreamHandlerFactory
        //    вызывается дважды → factory already defined.
        int removed = StripModulePathJarsFromClasspath(process.StartInfo);
        OnStatus?.Invoke($"Modulepath-JAR удалено из classpath: {removed}");

        // Диагностика — печатаем всю команду в отладку
        Debug.WriteLine("=== КОМАНДА ЗАПУСКА ===");
        Debug.WriteLine(process.StartInfo.FileName + " " + process.StartInfo.Arguments);

        // WorkingDirectory — иначе моды (KubeJS, CraftTweaker) не найдут config
        process.StartInfo.WorkingDirectory = _config.GameDirectory;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
        process.StartInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data != null) OnGameLog?.Invoke(args.Data);
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data != null) OnGameLog?.Invoke("[ERR] " + args.Data);
        };

        OnStatus?.Invoke($"Запуск Minecraft {versionToLaunch}...");
        if (!process.Start())
            throw new InvalidOperationException("Не удалось запустить процесс Minecraft");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await Task.Run(() => process.WaitForExit());
        OnStatus?.Invoke("Клиент Minecraft завершён.");
    }

    // ================================================================
    // ГЛАВНЫЙ ФИКС — вырезаем modulepath-JAR из "-cp"
    // ================================================================

    /// <summary>
    /// Ищет в аргументах процесса "-cp <classpath>" и удаляет из него JAR-ы,
    /// которые должны идти через --module-path (-p). Возвращает количество удалённых.
    /// </summary>
    private static int StripModulePathJarsFromClasspath(ProcessStartInfo startInfo)
    {
        // ProcessStartInfo.Arguments — одна строка. Модифицируем её.
        string args = startInfo.Arguments;
        if (string.IsNullOrEmpty(args)) return 0;

        // Находим "-cp <QUOTED_CLASSPATH>". Classpath в кавычках, отделён пробелом.
        // Формат от CmlLib: -cp "C:\..\a.jar;C:\..\b.jar;..." или без кавычек если нет пробелов.
        int cpIdx = args.IndexOf(" -cp ", StringComparison.Ordinal);
        if (cpIdx < 0)
        {
            cpIdx = args.IndexOf(" -classpath ", StringComparison.Ordinal);
            if (cpIdx < 0) return 0;
        }

        // Найти начало classpath-значения (после "-cp ")
        int valueStart = args.IndexOf(' ', cpIdx + 1) + 1; // пропускаем "-cp"
        // Пропускаем пробелы
        while (valueStart < args.Length && args[valueStart] == ' ') valueStart++;

        bool quoted = valueStart < args.Length && args[valueStart] == '"';
        int contentStart = quoted ? valueStart + 1 : valueStart;

        // Найти конец classpath
        int contentEnd;
        if (quoted)
        {
            contentEnd = args.IndexOf('"', contentStart);
            if (contentEnd < 0) return 0;
        }
        else
        {
            contentEnd = args.IndexOf(' ', contentStart);
            if (contentEnd < 0) contentEnd = args.Length;
        }

        string classpath = args.Substring(contentStart, contentEnd - contentStart);

        // Разделитель classpath (Windows = ';', Linux/Mac = ':')
        char sep = OperatingSystem.IsWindows() ? ';' : ':';
        var entries = classpath.Split(sep, StringSplitOptions.RemoveEmptyEntries);

        int removed = 0;
        var filtered = new List<string>(entries.Length);
        foreach (var entry in entries)
        {
            string fileName = Path.GetFileName(entry);
            if (IsModulePathJar(fileName))
            {
                removed++;
                continue;
            }
            filtered.Add(entry);
        }

        if (removed == 0) return 0;

        string newClasspath = string.Join(sep, filtered);

        // Собираем строку обратно
        string before = args.Substring(0, contentStart);
        string after = args.Substring(contentEnd);
        startInfo.Arguments = before + newClasspath + after;

        return removed;
    }

    private static bool IsModulePathJar(string fileName)
    {
        foreach (var marker in ModulePathMarkers)
        {
            if (fileName.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // asm-9.8.jar — совпадёт с "asm-" + цифра. Иначе исключаем false-positive.
        if (fileName.StartsWith("asm-", StringComparison.OrdinalIgnoreCase) &&
            fileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
        {
            // Формат: asm-9.8.jar / asm-9.7.jar / asm-9.6.jar
            // Проверяем что после "asm-" идёт цифра
            if (fileName.Length > 4 && char.IsDigit(fileName[4]))
                return true;
        }

        return false;
    }

    // ================================================================
    // NeoForge manifest generation (fallback)
    // ================================================================

    private void EnsureForgeManifest()
    {
        string versionDir = Path.Combine(_config.GameDirectory, "versions", _manifest.VersionFolder);
        string jsonPath = Path.Combine(versionDir, _manifest.VersionJson);
        if (File.Exists(jsonPath)) return;
        Directory.CreateDirectory(versionDir);

        // 1) Приоритет: встроенный настоящий NeoForge-манифест
        try
        {
            var uri = new Uri($"avares://VelesTech/Assets/EmbeddedManifests/neoforge-{_manifest.ForgeVersion}.json");
            using var stream = AssetLoader.Open(uri);
            using var reader = new StreamReader(stream);
            string rawJson = reader.ReadToEnd();

            string patched = System.Text.RegularExpressions.Regex.Replace(
                rawJson, @"""id""\s*:\s*""[^""]+""", $"\"id\": \"{_manifest.VersionFolder}\"");

            File.WriteAllText(jsonPath, patched);
            OnStatus?.Invoke($"Создан встроенный NeoForge-манифест v{_manifest.ForgeVersion}");
            return;
        }
        catch (Exception ex)
        {
            OnStatus?.Invoke($"[WARN] Не удалось выгрузить встроенный NeoForge-манифест: {ex.Message}");
        }

        // 2) Fallback — ищем в /versions/
        var forgeDirs = Directory.EnumerateDirectories(Path.Combine(_config.GameDirectory, "versions"))
            .Where(d => Path.GetFileName(d).Contains("forge", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var forgeDir in forgeDirs)
        {
            var candidate = Path.Combine(forgeDir, Path.GetFileName(forgeDir) + ".json");
            if (File.Exists(candidate))
            {
                File.Copy(candidate, jsonPath, true);
                OnStatus?.Invoke($"Использован NeoForge-манифест из {Path.GetFileName(forgeDir)}");
                return;
            }
        }

        // 3) Аварийная заглушка
        string fallbackJson = $$"""
        {
          "id": "{{_manifest.VersionFolder}}",
          "inheritsFrom": "{{_manifest.MinecraftVersion}}",
          "type": "release",
          "mainClass": "cpw.mods.bootstraplauncher.BootstrapLauncher",
          "arguments": {
            "game": [
              "--fml.neoForgeVersion", "{{_manifest.ForgeVersion}}",
              "--fml.fmlVersion", "4.0.42",
              "--fml.mcVersion", "{{_manifest.MinecraftVersion}}",
              "--fml.neoFormVersion", "20240808.144430",
              "--launchTarget", "forgeclient"
            ],
            "jvm": []
          }
        }
        """;
        File.WriteAllText(jsonPath, fallbackJson);
        OnStatus?.Invoke("[WARN] Запустится ваниль без модов. Проверь versions/ в сборке!");
    }

    // ================================================================
    // Java resolution + version detection
    // ================================================================

    private string ResolveJavaPath()
    {
        if (!string.IsNullOrWhiteSpace(_config.CustomJavaPath) && File.Exists(_config.CustomJavaPath))
            return _config.CustomJavaPath;

        string runtimeDir = Path.Combine(_config.GameDirectory, "runtime");
        if (Directory.Exists(runtimeDir))
        {
            string preferredExe = OperatingSystem.IsWindows() ? "javaw.exe" : "java";
            string fallbackExe = OperatingSystem.IsWindows() ? "java.exe" : "java";

            string? found = FindExe(runtimeDir, preferredExe) ?? FindExe(runtimeDir, fallbackExe);
            if (!string.IsNullOrEmpty(found))
                return found;
        }

        return OperatingSystem.IsWindows() ? "javaw" : "java";
    }

    private static string? FindExe(string root, string exeName)
    {
        try { return Directory.EnumerateFiles(root, exeName, SearchOption.AllDirectories).FirstOrDefault(); }
        catch { return null; }
    }

    /// <summary>Определяет major-версию Java из файла release рядом с исполнимым.</summary>
    private static string DetectJavaMajorVersion(string javaExePath)
    {
        try
        {
            var binDir = Path.GetDirectoryName(javaExePath);
            if (string.IsNullOrEmpty(binDir)) return "?";
            var jreDir = Path.GetDirectoryName(binDir);
            if (string.IsNullOrEmpty(jreDir)) return "?";

            string releasePath = Path.Combine(jreDir, "release");
            if (!File.Exists(releasePath)) return "?";

            foreach (var line in File.ReadAllLines(releasePath))
            {
                if (line.StartsWith("JAVA_VERSION="))
                {
                    var value = line.Substring("JAVA_VERSION=".Length).Trim('"', ' ');
                    if (value.StartsWith("1."))
                    {
                        var parts = value.Split('.');
                        if (parts.Length >= 2) return parts[1];
                    }
                    else
                    {
                        var parts = value.Split('.');
                        return parts[0];
                    }
                }
            }
            return "?";
        }
        catch
        {
            return "?";
        }
    }
}
