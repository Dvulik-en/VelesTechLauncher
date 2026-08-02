using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VelesTech.Models;

namespace VelesTech.Services;

/// <summary>
/// Локальная авторизация: логин/пароль хранятся в зашифрованном файле на ПК игрока.
/// Пароль сохраняется как SHA256(salt + password), сам файл дополнительно шифруется
/// через Windows DPAPI (ProtectedData) — данные читаемы только для текущего пользователя Windows.
/// </summary>
public static class AuthService
{
    private static readonly string AccountDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VelesTech");

    private static readonly string AccountPath = Path.Combine(AccountDir, "account.dat");

    /// <summary>Возвращает сохранённый аккаунт или null, если игрок ещё не регистрировался</summary>
    public static AccountData? GetSavedAccount()
    {
        try
        {
            if (!File.Exists(AccountPath)) return null;
            byte[] encrypted = File.ReadAllBytes(AccountPath);
            byte[] decrypted = Unprotect(encrypted);
            string json = Encoding.UTF8.GetString(decrypted);
            return JsonSerializer.Deserialize<AccountData>(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] GetSavedAccount error: {ex.Message}");
            return null;
        }
    }

    /// <summary>Регистрация нового аккаунта (перезаписывает существующий!)</summary>
    public static AccountData Register(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Логин не может быть пустым");
        if (password == null || password.Length < 4)
            throw new ArgumentException("Пароль должен быть не короче 4 символов");

        var acc = new AccountData
        {
            Username = username.Trim(),
            Salt = GenerateSalt(),
            Uuid = Guid.NewGuid().ToString("N") // 32 символа без дефисов
        };
        acc.PasswordHash = HashPassword(password, acc.Salt);

        SaveAccount(acc);
        return acc;
    }

    /// <summary>Проверка пароля. Возвращает аккаунт, если пароль верный, иначе null.</summary>
    public static AccountData? Login(string username, string password)
    {
        var saved = GetSavedAccount();
        if (saved == null) return null;

        if (!string.Equals(saved.Username, username?.Trim(), StringComparison.OrdinalIgnoreCase))
            return null;

        var hash = HashPassword(password, saved.Salt);
        return hash == saved.PasswordHash ? saved : null;
    }

    /// <summary>Удаляет сохранённый аккаунт (выход из аккаунта)</summary>
    public static void Logout()
    {
        try
        {
            if (File.Exists(AccountPath)) File.Delete(AccountPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] Logout error: {ex.Message}");
        }
    }

    // ==================== Внутренняя кухня ====================

    private static void SaveAccount(AccountData acc)
    {
        Directory.CreateDirectory(AccountDir);
        string json = JsonSerializer.Serialize(acc);
        byte[] data = Encoding.UTF8.GetBytes(json);
        byte[] protectedData = Protect(data);
        File.WriteAllBytes(AccountPath, protectedData);
    }

    private static string GenerateSalt()
    {
        byte[] bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string HashPassword(string password, string salt)
    {
        using var sha = SHA256.Create();
        byte[] combined = Encoding.UTF8.GetBytes(salt + "::" + password + "::VELES-TECH-PEPPER");
        byte[] hash = sha.ComputeHash(combined);
        return Convert.ToBase64String(hash);
    }

    // DPAPI — доступен только под Windows. На других ОС просто отдаём данные как есть
    // (лаунчер по концепции для Windows, но так проще собирать/тестить на другой ОС).
    private static byte[] Protect(byte[] data)
    {
        if (OperatingSystem.IsWindows()) return WindowsProtect(data);
        return data;
    }

    private static byte[] Unprotect(byte[] data)
    {
        if (OperatingSystem.IsWindows()) return WindowsUnprotect(data);
        return data;
    }

    [SupportedOSPlatform("windows")]
    private static byte[] WindowsProtect(byte[] data) =>
        ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

    [SupportedOSPlatform("windows")]
    private static byte[] WindowsUnprotect(byte[] data) =>
        ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
}
