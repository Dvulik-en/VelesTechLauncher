using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace VelesTech.Services;

/// <summary>
/// Ping Minecraft-сервера по протоколу Server List Ping (SLP).
///
/// Автоматически пробует несколько протокольных версий (767=1.21, 765=1.20.5, 754=1.16.5, 47=1.8),
/// потому что некоторые сервера с прокси (BungeeCord/Velocity) отбрасывают соединение,
/// если пришёл "не тот" протокол — а нам всё равно, важен сам факт что сервер отвечает.
/// </summary>
public static class ServerMonitorService
{
    public record ServerStatus(bool IsOnline, string StatusText, string Version, double LoadFactor,
        int Online, int Max);

    // Пробуем несколько протоколов (актуальные первыми)
    private static readonly int[] ProtocolVersions = { 767, 765, 754, 47 };

    public static async Task<ServerStatus> CheckServerAsync(string host, ushort port = 25565)
    {
        foreach (int protocol in ProtocolVersions)
        {
            var result = await TryPingAsync(host, port, protocol);
            if (result.IsOnline) return result;
        }

        // Если ни один SLP не сработал — хотя бы проверим что порт открыт
        if (await IsPortOpenAsync(host, port))
        {
            return new ServerStatus(true, "ОНЛАЙН", $"MC {(char)0x2013}", 0.3, 0, 0);
        }

        return new ServerStatus(false, "ОФФЛАЙН", "Неизвестно", 0.0, 0, 0);
    }

    private static async Task<bool> IsPortOpenAsync(string host, ushort port)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            var delayTask = Task.Delay(3000);
            if (await Task.WhenAny(connectTask, delayTask) == delayTask) return false;
            return client.Connected;
        }
        catch { return false; }
    }

    private static async Task<ServerStatus> TryPingAsync(string host, ushort port, int protocol)
    {
        try
        {
            using var client = new TcpClient();
            client.ReceiveTimeout = 4000;
            client.SendTimeout = 4000;
            var connectTask = client.ConnectAsync(host, port);
            var delayTask = Task.Delay(4000);

            if (await Task.WhenAny(connectTask, delayTask) == delayTask || !client.Connected)
                return new ServerStatus(false, "ОФФЛАЙН", "Неизвестно", 0.0, 0, 0);

            using var stream = client.GetStream();

            byte[] handshakePacket = CreateHandshakePacket(host, port, protocol);
            await stream.WriteAsync(handshakePacket);

            // Request status
            await stream.WriteAsync(new byte[] { 0x01, 0x00 });

            // Читаем ответ с таймаутом
            var readTask = ReadResponseAsync(stream);
            var timeoutTask = Task.Delay(5000);
            if (await Task.WhenAny(readTask, timeoutTask) == timeoutTask)
                return new ServerStatus(false, "ТАЙМАУТ", "Неизвестно", 0.0, 0, 0);

            string jsonString = await readTask;
            if (string.IsNullOrEmpty(jsonString))
                return new ServerStatus(false, "СБОЙ ДАННЫХ", "Неизвестно", 0.0, 0, 0);

            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            int online = 0, max = 0;
            if (root.TryGetProperty("players", out var players))
            {
                if (players.TryGetProperty("online", out var o)) online = o.GetInt32();
                if (players.TryGetProperty("max", out var m)) max = m.GetInt32();
            }

            string versionStr = "1.21.1";
            if (root.TryGetProperty("version", out var versionElement) &&
                versionElement.TryGetProperty("name", out var vName))
            {
                versionStr = vName.GetString() ?? "1.21.1";
                versionStr = System.Text.RegularExpressions.Regex.Replace(versionStr, @"§.", "");
            }

            double loadFactor = max > 0 ? (double)online / max : 0.05;
            if (loadFactor == 0) loadFactor = 0.05;

            return new ServerStatus(true, $"{online} / {max} ИГРОКОВ", versionStr, loadFactor, online, max);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SLP proto={protocol}] {ex.Message}");
            return new ServerStatus(false, "НЕДОСТУПЕН", "Неизвестно", 0.0, 0, 0);
        }
    }

    private static async Task<string> ReadResponseAsync(NetworkStream stream)
    {
        try
        {
            int lenA = await ReadVarIntAsync(stream);
            if (lenA < 0) return "";
            int lenB = await ReadVarIntAsync(stream);
            if (lenB < 0) return "";
            int jsonLength = await ReadVarIntAsync(stream);

            if (jsonLength <= 0 || jsonLength > 5 * 1024 * 1024) return "";

            byte[] jsonBuffer = new byte[jsonLength];
            int totalRead = 0;
            while (totalRead < jsonLength)
            {
                int read = await stream.ReadAsync(jsonBuffer.AsMemory(totalRead, jsonLength - totalRead));
                if (read == 0) return "";
                totalRead += read;
            }
            return Encoding.UTF8.GetString(jsonBuffer);
        }
        catch { return ""; }
    }

    private static byte[] CreateHandshakePacket(string host, ushort port, int protocol)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0x00);
        WriteVarInt(ms, protocol);

        byte[] hostBytes = Encoding.UTF8.GetBytes(host);
        WriteVarInt(ms, hostBytes.Length);
        ms.Write(hostBytes, 0, hostBytes.Length);

        ms.WriteByte((byte)(port >> 8));
        ms.WriteByte((byte)(port & 0xFF));
        ms.WriteByte(0x01); // next state = status

        byte[] packetData = ms.ToArray();
        using var finalMs = new MemoryStream();
        WriteVarInt(finalMs, packetData.Length);
        finalMs.Write(packetData, 0, packetData.Length);
        return finalMs.ToArray();
    }

    private static void WriteVarInt(Stream stream, int value)
    {
        while ((value & 128) != 0)
        {
            stream.WriteByte((byte)(value & 127 | 128));
            value = (int)((uint)value >> 7);
        }
        stream.WriteByte((byte)value);
    }

    /// <summary>
    /// Читаем VarInt. Вместо throw EndOfStreamException — возвращаем -1,
    /// чтобы не засорять окно отладчика "Exception thrown: 'System.IO.EndOfStreamException'".
    /// </summary>
    private static async Task<int> ReadVarIntAsync(Stream stream)
    {
        int numRead = 0, result = 0;
        byte read;
        do
        {
            byte[] buffer = new byte[1];
            int readBytes = await stream.ReadAsync(buffer.AsMemory(0, 1));
            if (readBytes == 0) return -1; // ← БЫЛО: throw new EndOfStreamException();
            read = buffer[0];
            int value = read & 127;
            result |= value << (7 * numRead);
            numRead++;
            if (numRead > 5) return -1; // ← БЫЛО: throw new InvalidDataException();
        } while ((read & 128) != 0);
        return result;
    }
}
