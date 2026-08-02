using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using VelesTech.Models;
using VelesTech.Services;

namespace VelesTech.Views;

/// <summary>
/// Окно первого запуска: регистрация нового аккаунта / вход.
/// Открывается один раз (при первом запуске лаунчера).
/// Учётные данные хранятся зашифрованно (DPAPI + SHA256).
/// </summary>
public class LoginWindow : Window
{
    /// <summary>Колбэк, который вызывается когда игрок успешно вошёл/зарегистрировался.</summary>
    public Action<AccountData>? OnLoginSuccess;

    private readonly TextBox _usernameBox;
    private readonly TextBox _passwordBox;
    private readonly TextBox _passwordBox2;
    private readonly TextBlock _errorLabel;
    private readonly TextBlock _modeLabel;
    private readonly Button _submitBtn;
    private readonly Button _switchModeBtn;

    private bool _isRegisterMode = true; // По умолчанию — регистрация (первый запуск)

    public LoginWindow()
    {
        Title = "VELES TECH // АВТОРИЗАЦИЯ ОПЕРАТОРА";
        Width = 460;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        CanResize = false;
        Background = SolidColorBrush.Parse("#1A1A1E");

        // Если уже есть аккаунт — сразу режим входа
        _isRegisterMode = AuthService.GetSavedAccount() == null;

        var orange = SolidColorBrush.Parse("#E5731C");
        var borderGray = SolidColorBrush.Parse("#2D2D32");
        var muted = SolidColorBrush.Parse("#7E7E84");

        var mainStack = new StackPanel
        {
            Margin = new Thickness(30),
            Spacing = 14,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Логотип
        var logo = new TextBlock
        {
            Text = "VELES TECH",
            FontSize = 32,
            FontWeight = FontWeight.Black,
            Foreground = orange,
            LetterSpacing = 4,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        mainStack.Children.Add(logo);

        _modeLabel = new TextBlock
        {
            Text = "РЕГИСТРАЦИЯ НОВОГО ОПЕРАТОРА",
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Foreground = muted,
            LetterSpacing = 2,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 15)
        };
        mainStack.Children.Add(_modeLabel);

        // Поле логин
        mainStack.Children.Add(FieldLabel("ЛОГИН / НИК В ИГРЕ"));
        _usernameBox = MakeInput("Alex_Steve");
        mainStack.Children.Add(_usernameBox);

        // Пароль
        mainStack.Children.Add(FieldLabel("ПАРОЛЬ"));
        _passwordBox = MakeInput("••••••••", isPassword: true);
        mainStack.Children.Add(_passwordBox);

        // Подтверждение пароля (только для регистрации)
        var confirmLabel = FieldLabel("ПОВТОРИТЕ ПАРОЛЬ");
        _passwordBox2 = MakeInput("••••••••", isPassword: true);
        mainStack.Children.Add(confirmLabel);
        mainStack.Children.Add(_passwordBox2);

        _errorLabel = new TextBlock
        {
            Text = "",
            Foreground = Brushes.OrangeRed,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };
        mainStack.Children.Add(_errorLabel);

        // Кнопка "ВОЙТИ / ЗАРЕГИСТРИРОВАТЬСЯ"
        _submitBtn = new Button
        {
            Content = "ЗАРЕГИСТРИРОВАТЬСЯ",
            Background = orange,
            Foreground = Brushes.White,
            FontWeight = FontWeight.Black,
            FontSize = 14,
            Height = 48,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 10, 0, 0)
        };
        _submitBtn.Click += (_, _) => Submit();
        mainStack.Children.Add(_submitBtn);

        // Переключение режима
        _switchModeBtn = new Button
        {
            Content = "Уже есть аккаунт? Войти",
            Background = Brushes.Transparent,
            Foreground = muted,
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            BorderThickness = new Thickness(0)
        };
        _switchModeBtn.Click += (_, _) => ToggleMode();
        mainStack.Children.Add(_switchModeBtn);

        Content = mainStack;

        // Инициализация UI по режиму
        ApplyMode();
    }

    private TextBlock FieldLabel(string text) => new()
    {
        Text = text,
        FontSize = 10,
        FontWeight = FontWeight.Bold,
        Foreground = SolidColorBrush.Parse("#7E7E84"),
        LetterSpacing = 1.5
    };

    private TextBox MakeInput(string watermark, bool isPassword = false) => new()
    {
        Watermark = watermark,
        FontSize = 14,
        Height = 40,
        Background = SolidColorBrush.Parse("#1E1E22"),
        BorderBrush = SolidColorBrush.Parse("#2D2D32"),
        BorderThickness = new Thickness(1),
        Foreground = Brushes.White,
        PasswordChar = isPassword ? '•' : '\0',
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(10, 6, 10, 6)
    };

    private void ToggleMode()
    {
        _isRegisterMode = !_isRegisterMode;
        ApplyMode();
    }

    private void ApplyMode()
    {
        if (_isRegisterMode)
        {
            _modeLabel.Text = "РЕГИСТРАЦИЯ НОВОГО ОПЕРАТОРА";
            _submitBtn.Content = "ЗАРЕГИСТРИРОВАТЬСЯ";
            _switchModeBtn.Content = "Уже есть аккаунт? Войти";
            _passwordBox2.IsVisible = true;
        }
        else
        {
            _modeLabel.Text = "ВХОД ОПЕРАТОРА В СЕТЬ";
            _submitBtn.Content = "ВОЙТИ";
            _switchModeBtn.Content = "Нет аккаунта? Создать новый";
            _passwordBox2.IsVisible = false;
        }
        _errorLabel.Text = "";
    }

    private void Submit()
    {
        _errorLabel.Text = "";
        string username = (_usernameBox.Text ?? "").Trim();
        string password = _passwordBox.Text ?? "";

        if (username.Length < 3)
        {
            _errorLabel.Text = "Логин минимум 3 символа";
            return;
        }
        // Классические правила для Minecraft-ника
        if (!System.Text.RegularExpressions.Regex.IsMatch(username, "^[A-Za-z0-9_]{3,16}$"))
        {
            _errorLabel.Text = "Логин: 3–16 символов, только A–Z, 0–9, _";
            return;
        }

        try
        {
            AccountData? acc;
            if (_isRegisterMode)
            {
                string password2 = _passwordBox2.Text ?? "";
                if (password != password2)
                {
                    _errorLabel.Text = "Пароли не совпадают";
                    return;
                }
                if (password.Length < 4)
                {
                    _errorLabel.Text = "Пароль минимум 4 символа";
                    return;
                }
                acc = AuthService.Register(username, password);
            }
            else
            {
                acc = AuthService.Login(username, password);
                if (acc == null)
                {
                    _errorLabel.Text = "Неверный логин или пароль";
                    return;
                }
            }

            OnLoginSuccess?.Invoke(acc);
        }
        catch (Exception ex)
        {
            _errorLabel.Text = ex.Message;
        }
    }
}
