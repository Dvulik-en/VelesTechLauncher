using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VelesTech.Models;

namespace VelesTech.Services;

/// <summary>
/// Скачивает архив со сборкой через Pterodactyl Client API и распаковывает
/// в каталог сборки.
///
/// Схема работы Pterodactyl API:
///   1) POST /api/client/servers/{serverId}/files/download?file=%2F{fileName}
///      с Bearer ptlc_... токеном → возвращает JSON { "attributes": { "url": "..." } }
///   2) GET по этому одноразовому url → скачивает файл потоком.
///
/// Распаковка — строго через File.Copy (а не Move), чтобы работала между дисками
/// и когда игрок держит папку открытой в проводнике.
/// </summary>
public class ModpackInstaller
{
    private readonly ModpackManifest _manifest;
    private readonly LauncherConfig _config;

    /// <summary>Событие прогресса: (proc %, mbDone, mbTotal, статус текстом)</summary>
    public event Action<double, double, double, string>? OnProgress;

    public ModpackInstaller(ModpackManifest manifest, LauncherConfig config)
    {
        _manifest = manifest;
        _config = config;
    }

    /// <summary>Проверяет — установлен ли уже клиент.</summary>
    public bool IsInstalled()
    {
        string modsFolder = Path.Combine(_config.GameDirectory, "mods");
        string versionFolder = Path.Combine(_config.GameDirectory, "versions", _manifest.VersionFolder);
        return _config.ClientInstalled &&
               Directory.Exists(modsFolder) &&
               Directory.Exists(versionFolder) &&
               Directory.GetFiles(modsFolder).Length > 0;
    }

    /// <summary>Полная переустановка: скачать архив с Pterodactyl и распаковать.</summary>
    public async Task InstallAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(_config.GameDirectory);
        string zipPath = Path.Combine(_config.GameDirectory, "client_package.zip");
        if (File.Exists(zipPath)) File.Delete(zipPath);

        OnProgress?.Invoke(0, 0, 0, "СВЯЗЬ С ХОСТИНГОМ...");

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _manifest.PanelApiToken);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        http.DefaultRequestHeaders.Add("User-Agent", "VelesTech-Launcher/1.0");
        http.Timeout = TimeSpan.FromMinutes(60);

        // 1) Получаем одноразовую ссылку на файл через Pterodactyl API
        string apiLinkUrl = $"{_manifest.PanelUrl.TrimEnd('/')}/api/client/servers/{_manifest.PanelServerId}/files/download" +
                            $"?file=%2F{Uri.EscapeDataString(_manifest.ArchiveFileName)}";

        HttpResponseMessage apiResponse = await http.GetAsync(apiLinkUrl, ct);
        if (!apiResponse.IsSuccessStatusCode)
        {
            string err = await apiResponse.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Pterodactyl API ответил {(int)apiResponse.StatusCode} {apiResponse.StatusCode}. " +
                $"Проверь PanelUrl / PanelServerId / PanelApiToken / ArchiveFileName. Ответ: {Trim(err, 300)}");
        }

        string jsonResponse = await apiResponse.Content.ReadAsStringAsync(ct);
        string realDownloadUrl = ParseDownloadUrl(jsonResponse);
        if (string.IsNullOrEmpty(realDownloadUrl))
            throw new InvalidOperationException($"Не удалось получить прямую ссылку из ответа API: {Trim(jsonResponse, 300)}");

        // 2) Качаем архив
        OnProgress?.Invoke(0, 0, 0, "ПОДКЛЮЧЕНИЕ К ХРАНИЛИЩУ...");
        using (var dlResponse = await http.GetAsync(realDownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            dlResponse.EnsureSuccessStatusCode();
            long? totalBytes = dlResponse.Content.Headers.ContentLength;
            double totalMb = totalBytes.HasValue ? Math.Round((double)totalBytes.Value / 1024 / 1024, 1) : 0;

            using var contentStream = await dlResponse.Content.ReadAsStreamAsync(ct);
            using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[81920];
            long totalRead = 0;
            int read;
            var lastReport = DateTime.MinValue;
            while ((read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                totalRead += read;

                if ((DateTime.UtcNow - lastReport).TotalMilliseconds > 200)
                {
                    lastReport = DateTime.UtcNow;
                    double doneMb = Math.Round((double)totalRead / 1024 / 1024, 1);
                    double pct = totalBytes.HasValue
                        ? Math.Round((double)totalRead / totalBytes.Value * 100, 1)
                        : 0;
                    OnProgress?.Invoke(pct, doneMb, totalMb, $"СКАЧИВАНИЕ: {doneMb} / {totalMb} MB");
                }
            }
        }

        // 3) Распаковка
        OnProgress?.Invoke(100, 0, 0, "РАСПАКОВКА СБОРКИ...");
        await Task.Run(() => ExtractArchiveSafely(zipPath, _config.GameDirectory, ct), ct);

        // 4) Уборка + флаг
        try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { /* архив можно оставить, не критично */ }
        _config.ClientInstalled = true;
        ConfigService.Save(_config);
        OnProgress?.Invoke(100, 0, 0, "СБОРКА УСТАНОВЛЕНА");
    }

    // ==================== РАСПАКОВКА (устойчивая) ====================

    /// <summary>
    /// Распаковка через File.Copy: работает между дисками, не падает если папка
    /// уже частично существует. Сначала распаковываем во временную папку РЯДОМ
    /// с целевой (тот же диск!), потом копируем поверх целевого каталога.
    /// </summary>
    private void ExtractArchiveSafely(string zipPath, string targetDir, CancellationToken ct)
    {
        string tempExtract = Path.Combine(targetDir, "_temp_extract_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        try
        {
            Directory.CreateDirectory(tempExtract);
            ZipFile.ExtractToDirectory(zipPath, tempExtract, overwriteFiles: true);

            // Ищем корень содержимого:
            //   если внутри архива один каталог-обёртка и нет файлов на верхнем уровне —
            //   берём этот каталог как источник
            string sourceDir = tempExtract;
            var innerDirs = Directory.GetDirectories(tempExtract);
            var innerFiles = Directory.GetFiles(tempExtract);
            if (innerDirs.Length == 1 && innerFiles.Length == 0)
                sourceDir = innerDirs[0];

            // Копируем содержимое sourceDir → targetDir (рекурсивно, с перезаписью)
            CopyDirectoryContents(sourceDir, targetDir, ct);
        }
        finally
        {
            // Убираем временную папку
            try
            {
                if (Directory.Exists(tempExtract))
                    Directory.Delete(tempExtract, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Installer] Cleanup warn: {ex.Message}");
            }
        }
    }

    private static void CopyDirectoryContents(string sourceDir, string targetDir, CancellationToken ct)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            ct.ThrowIfCancellationRequested();

            string fileName = Path.GetFileName(file);
            string destFile = Path.Combine(targetDir, fileName);

            // Если целевой файл существует, снимаем атрибут "Только для чтения", иначе File.Copy упадет
            if (File.Exists(destFile))
            {
                var attributes = File.GetAttributes(destFile);
                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(destFile, attributes & ~FileAttributes.ReadOnly);
                }
            }

            // Копируем файл
            File.Copy(file, destFile, overwrite: true);

            // Гарантируем, что скопированный файл тоже не будет "Только для чтения"
            var newAttributes = File.GetAttributes(destFile);
            if ((newAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                File.SetAttributes(destFile, newAttributes & ~FileAttributes.ReadOnly);
            }
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            ct.ThrowIfCancellationRequested();

            string dirName = Path.GetFileName(subDir);
            string destSubDir = Path.Combine(targetDir, dirName);

            CopyDirectoryContents(subDir, destSubDir, ct);
        }
    }

    // ==================== JSON парсинг Pterodactyl ====================

    /// <summary>
    /// Достаёт "url" из ответа Pterodactyl. Формат:
    /// { "object": "signed_url", "attributes": { "url": "https://..." } }
    /// </summary>
    private static string ParseDownloadUrl(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("attributes", out var attrs) &&
                attrs.TryGetProperty("url", out var urlEl))
                return urlEl.GetString() ?? "";

            if (root.TryGetProperty("url", out var direct))
                return direct.GetString() ?? "";
        }
        catch
        {
            // Fallback — грубый regex (на случай если формат изменится)
        }

        const string marker = "\"url\":\"";
        int start = json.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return "";
        start += marker.Length;
        int end = json.IndexOf('"', start);
        if (end < 0) return "";
        return json.Substring(start, end - start).Replace("\\/", "/");
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "...";
}
