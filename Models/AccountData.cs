namespace VelesTech.Models;

/// <summary>
/// Данные аккаунта игрока (хранятся в зашифрованном виде на ПК).
/// PasswordHash — SHA256 от пароля + соли.
/// </summary>
public class AccountData
{
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Salt { get; set; } = "";
    /// <summary>Постоянный UUID игрока, привязан к аккаунту (генерится один раз)</summary>
    public string Uuid { get; set; } = "";
}
