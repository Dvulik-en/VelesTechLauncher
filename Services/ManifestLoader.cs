using System;
using System.IO;
using System.Text.Json;
using VelesTech.Models;

namespace VelesTech.Services;

/// <summary>
/// Загрузка манифеста сборки из двух JSON-файлов:
///   1) launcher.config.json  — публичная часть (в репозитории)
///   2) launcher.secrets.json — приватная часть (в .gitignore)
///
/// Пути поиска (по приоритету):
///   • %APPDATA%\VelesTech\launcher.secrets.json      ← рекомендуется для игроков
///   • рядом с VelesTech.exe (Program Files, portable) ← удобно для админа
///   • Assets\launcher.config.json                     ← вшито в билд, fallback
///
/// Схема: приватный JSON накладывается ПОВЕРХ публичного, поэтому чувствительные
/// поля (panelApiToken, serverIp) есть только в приватном файле.
/// </summary>
public static class ManifestLoader
{
    private const string PublicConfigName = "launcher.config.json";
    private const string SecretsName = "launcher.secrets.json";

    /// <summary>Папка %APPDATA%\VelesTech</summary>
    private static readonly string AppDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VelesTech");

    /// <summary>
    /// Собирает финальный <see cref="ModpackManifest"/> из публичного и приватного JSON.
    /// Если приватный файл не найден — возвращается манифест только с публичной частью
    /// (без Pterodactyl-токена запуск не полетит, зато не упадёт при старте лаунчера).
    /// </summary>
    public static ModpackManifest Load()
    {
        // 1) Публичный конфиг — обязателен
        var publicManifest = LoadPublicConfig() ?? new ModpackManifest();

        // 2) Приватный секрет — накладываем поверх, если нашли
        var secrets = LoadSecrets();
        if (secrets != null)
        {
            MergeSecrets(publicManifest, secrets);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ManifestLoader] {SecretsName} не найден. " +
                $"Положите его в {AppDataDir} или рядом с VelesTech.exe.");
        }

        return publicManifest;
    }

    // ==================== ПУБЛИЧНЫЙ КОНФИГ ====================

    private static ModpackManifest? LoadPublicConfig()
    {
        foreach (var path in EnumeratePublicConfigPaths())
        {
            if (!File.Exists(path)) continue;
            try
            {
                string json = File.ReadAllText(path);
                var manifest = JsonSerializer.Deserialize<ModpackManifest>(json);
                if (manifest != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ManifestLoader] Публичный конфиг: {path}");
                    return manifest;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ManifestLoader] Ошибка чтения {path}: {ex.Message}");
            }
        }
        return null;
    }

    private static System.Collections.Generic.IEnumerable<string> EnumeratePublicConfigPaths()
    {
        // 1) Рядом с exe
        string exeDir = AppContext.BaseDirectory;
        yield return Path.Combine(exeDir, PublicConfigName);

        // 2) В Assets (если билд включил через <Content>)
        yield return Path.Combine(exeDir, "Assets", PublicConfigName);

        // 3) В %APPDATA% (если админ подложил кастомный)
        yield return Path.Combine(AppDataDir, PublicConfigName);
    }

    // ==================== ПРИВАТНЫЕ СЕКРЕТЫ ====================

    private static ModpackManifest? LoadSecrets()
    {
        foreach (var path in EnumerateSecretsPaths())
        {
            if (!File.Exists(path)) continue;
            try
            {
                string json = File.ReadAllText(path);
                var secrets = JsonSerializer.Deserialize<ModpackManifest>(json);
                if (secrets != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ManifestLoader] Секреты: {path}");
                    return secrets;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ManifestLoader] Ошибка чтения {path}: {ex.Message}");
            }
        }
        return null;
    }

    private static System.Collections.Generic.IEnumerable<string> EnumerateSecretsPaths()
    {
        // 1) %APPDATA%\VelesTech\launcher.secrets.json  ← основное место
        yield return Path.Combine(AppDataDir, SecretsName);

        // 2) Рядом с exe (portable-режим, для админа)
        yield return Path.Combine(AppContext.BaseDirectory, SecretsName);
    }

    // ==================== СЛИЯНИЕ ====================

    /// <summary>
    /// Наложить непустые значения из secrets поверх target.
    /// Пустая строка / 0-порт считаются "не задано" и не перезаписывают публичные.
    /// </summary>
    private static void MergeSecrets(ModpackManifest target, ModpackManifest secrets)
    {
        if (!string.IsNullOrWhiteSpace(secrets.DisplayName) && secrets.DisplayName != "Modpack")
            target.DisplayName = secrets.DisplayName;

        if (!string.IsNullOrWhiteSpace(secrets.MinecraftVersion) && secrets.MinecraftVersion != "1.21.1")
            target.MinecraftVersion = secrets.MinecraftVersion;

        if (!string.IsNullOrWhiteSpace(secrets.ForgeVersion))
            target.ForgeVersion = secrets.ForgeVersion;

        if (!string.IsNullOrWhiteSpace(secrets.VersionFolder) && secrets.VersionFolder != "modpack")
            target.VersionFolder = secrets.VersionFolder;
        if (!string.IsNullOrWhiteSpace(secrets.VersionJar) && secrets.VersionJar != "modpack")
            target.VersionJar = secrets.VersionJar;
        if (!string.IsNullOrWhiteSpace(secrets.VersionJson) && secrets.VersionJson != "modpack")
            target.VersionJson = secrets.VersionJson;

        if (!string.IsNullOrWhiteSpace(secrets.ModpackJsonUrl))
            target.ModpackJsonUrl = secrets.ModpackJsonUrl;
        if (!string.IsNullOrWhiteSpace(secrets.FilesBaseUrl))
            target.FilesBaseUrl = secrets.FilesBaseUrl;

        if (!string.IsNullOrWhiteSpace(secrets.PanelUrl))
            target.PanelUrl = secrets.PanelUrl;
        if (!string.IsNullOrWhiteSpace(secrets.PanelServerId))
            target.PanelServerId = secrets.PanelServerId;
        if (!string.IsNullOrWhiteSpace(secrets.PanelApiToken))
            target.PanelApiToken = secrets.PanelApiToken;
        if (!string.IsNullOrWhiteSpace(secrets.ArchiveFileName) && secrets.ArchiveFileName != "client.zip")
            target.ArchiveFileName = secrets.ArchiveFileName;

        if (!string.IsNullOrWhiteSpace(secrets.ServerIp))
            target.ServerIp = secrets.ServerIp;
        if (secrets.ServerPort != 0 && secrets.ServerPort != 25565)
            target.ServerPort = secrets.ServerPort;
    }

    /// <summary>
    /// Проверяет что все критичные приватные поля заданы. Вызывайте перед запуском игры,
    /// чтобы показать понятную ошибку "не найден launcher.secrets.json" вместо
    /// падения на Pterodactyl API с HTTP 401.
    /// </summary>
    public static bool ValidateSecrets(ModpackManifest manifest, out string missing)
    {
        var missingFields = new System.Collections.Generic.List<string>();

        if (string.IsNullOrWhiteSpace(manifest.PanelApiToken)) missingFields.Add("panelApiToken");
        if (string.IsNullOrWhiteSpace(manifest.PanelUrl)) missingFields.Add("panelUrl");
        if (string.IsNullOrWhiteSpace(manifest.PanelServerId)) missingFields.Add("panelServerId");
        if (string.IsNullOrWhiteSpace(manifest.ServerIp)) missingFields.Add("serverIp");

        missing = string.Join(", ", missingFields);
        return missingFields.Count == 0;
    }

    /// <summary>Путь для образца/шаблона secrets — вернём его пользователю в ошибке.</summary>
    public static string GetRecommendedSecretsPath() => Path.Combine(AppDataDir, SecretsName);
}
