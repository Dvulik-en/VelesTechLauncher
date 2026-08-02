using System.Text.Json.Serialization;

namespace VelesTech.Models;

/// <summary>
/// Описание сборки, которую качает и запускает лаунчер.
///
/// Значения-заглушки безопасны для публичного репозитория.
/// Реальные значения (версии, токен Pterodactyl, IP сервера) подставляются
/// из <c>launcher.config.json</c> и <c>launcher.secrets.json</c>
/// в <see cref="VelesTech.Services.ManifestLoader"/>.
/// </summary>
public class ModpackManifest
{
    // ==================== ПУБЛИЧНАЯ ЧАСТЬ (launcher.config.json) ====================

    /// <summary>Отображаемое имя сборки в UI</summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "Modpack";

    /// <summary>Базовая версия Minecraft</summary>
    [JsonPropertyName("minecraftVersion")]
    public string MinecraftVersion { get; set; } = "1.21.1";

    /// <summary>Версия NeoForge</summary>
    [JsonPropertyName("forgeVersion")]
    public string ForgeVersion { get; set; } = "21.1.235";

    /// <summary>Имя папки с версией внутри /versions/</summary>
    [JsonPropertyName("versionFolder")]
    public string VersionFolder { get; set; } = "modpack";

    /// <summary>Имя .jar файла версии (без пути)</summary>
    [JsonPropertyName("versionJar")]
    public string VersionJar { get; set; } = "modpack";

    /// <summary>Имя JSON-манифеста версии NeoForge внутри /versions/{VersionFolder}/</summary>
    [JsonPropertyName("versionJson")]
    public string VersionJson { get; set; } = "modpack";

    /// <summary>URL manifest.json со списком файлов сборки (SHA1 + размеры)</summary>
    [JsonPropertyName("modpackJsonUrl")]
    public string ModpackJsonUrl { get; set; } = "";

    /// <summary>Базовый URL для скачивания файлов сборки (моды, конфиги)</summary>
    [JsonPropertyName("filesBaseUrl")]
    public string FilesBaseUrl { get; set; } = "";

    // ==================== ПРИВАТНАЯ ЧАСТЬ (launcher.secrets.json) ====================

    /// <summary>URL панели Pterodactyl (например https://panel.example.com)</summary>
    [JsonPropertyName("panelUrl")]
    public string PanelUrl { get; set; } = "";

    /// <summary>ID сервера на панели (первая часть UUID до точки)</summary>
    [JsonPropertyName("panelServerId")]
    public string PanelServerId { get; set; } = "";

    /// <summary>Client API токен (ptlc_...). НИКОГДА не коммитить в репозиторий!</summary>
    [JsonPropertyName("panelApiToken")]
    public string PanelApiToken { get; set; } = "";

    /// <summary>Имя архива на панели (в корневой директории сервера)</summary>
    [JsonPropertyName("archiveFileName")]
    public string ArchiveFileName { get; set; } = "client.zip";

    /// <summary>IP игрового сервера для автозахода</summary>
    [JsonPropertyName("serverIp")]
    public string ServerIp { get; set; } = "";

    /// <summary>Порт игрового сервера</summary>
    [JsonPropertyName("serverPort")]
    public ushort ServerPort { get; set; } = 25565;
}
