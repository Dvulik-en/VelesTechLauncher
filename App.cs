using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using VelesTech.Services;
using VelesTech.Views;

namespace VelesTech;

public class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 1) Проверяем — есть ли уже зарегистрированный аккаунт?
            var account = AuthService.GetSavedAccount();

            if (account == null)
            {
                // Первый запуск — открываем окно логина/регистрации
                var loginWindow = new LoginWindow();
                loginWindow.OnLoginSuccess = (acc) =>
                {
                    var main = new MainWindow(acc);
                    desktop.MainWindow = main;
                    main.Show();
                    loginWindow.Close();
                };
                desktop.MainWindow = loginWindow;
            }
            else
            {
                // Уже есть сохранённый аккаунт — сразу главный экран
                desktop.MainWindow = new MainWindow(account);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
