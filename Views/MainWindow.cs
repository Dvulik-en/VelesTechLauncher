using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using VelesTech.Controls;
using VelesTech.Launcher;
using VelesTech.Models;
using VelesTech.Services;

namespace VelesTech.Views;

/// <summary>
/// Главное окно лаунчера VelesTech.
/// </summary>
public class MainWindow : Window
{
    private readonly AccountData _account;
    private readonly LauncherConfig _config;
    private readonly ModpackManifest _manifest;

    private readonly StackPanel _cryptoLogsContainer;
    private readonly StackPanel _statusStack;
    private readonly Button _startBtn;
    private readonly TextBlock _usernameLabel;
    private readonly ProgressBar _launchProgress;
    private readonly TextBlock _launchStatus;

    private CancellationTokenSource? _launchCts;

    public MainWindow(AccountData account)
    {
        _account = account;
        _config = ConfigService.Load();
        _manifest = ManifestLoader.Load();

        Title = "VELES TECH // ИНДУСТРИАЛЬНАЯ КОНСОЛЬ ЗАПУСКА v1.0";
        
        // Размеры окна с учетом отступов (20px) под свечение
        Width = 1040;
        Height = 690;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        CanResize = false;
        
        // Отключаем рамки Windows и делаем холст окна прозрачным
        SystemDecorations = SystemDecorations.None;
        Background = Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Mica, WindowTransparencyLevel.Blur, WindowTransparencyLevel.Transparent };

        var orangeAccent = SolidColorBrush.Parse("#E5731C");
        var panelBg = SolidColorBrush.Parse("#151518");
        var cardBg = SolidColorBrush.Parse("#1E1E22");
        var borderGray = SolidColorBrush.Parse("#2D2D32");
        var textMuted = SolidColorBrush.Parse("#7E7E84");

        var mainGrid = new Grid();
        mainGrid.RowDefinitions.Add(new RowDefinition(new GridLength(140))); // Шапка
        mainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));     // Контент
        mainGrid.RowDefinitions.Add(new RowDefinition(new GridLength(120))); // Подвал

        // ================= ШАПКА (Со скруглёнными ВЕРХНИМИ углами) =================
        var topHeader = BuildHeader();
        Grid.SetRow(topHeader, 0);

        // ================= ЦЕНТР (ЛОГИ + СТАТУС) =================
        var contentGrid = new Grid { Margin = new Thickness(15, 10, 15, 10) };
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(420)));

        // ЛЕВО — журналы
        var logsMainPanel = new StackPanel { Spacing = 10 };
        logsMainPanel.Children.Add(new TextBlock
        {
            Text = "ЖУРНАЛЫ ФАБРИКИ",
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Foreground = orangeAccent
        });
        var logsScroll = new ScrollViewer
        {
            Height = 340,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        _cryptoLogsContainer = new StackPanel { Spacing = 12 };
        logsScroll.Content = _cryptoLogsContainer;
        logsMainPanel.Children.Add(logsScroll);
        Grid.SetColumn(logsMainPanel, 0);

        // ПРАВО — статус сервера
        var statusMainPanel = new StackPanel { Spacing = 10, Margin = new Thickness(15, 0, 0, 0) };
        statusMainPanel.Children.Add(new TextBlock
        {
            Text = "СТАТУС СИСТЕМЫ",
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Foreground = orangeAccent
        });
        var statusCard = new Border
        {
            Background = cardBg,
            BorderBrush = borderGray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(15),
            Height = 340
        };
        _statusStack = new StackPanel { Spacing = 15 };
        statusCard.Child = _statusStack;
        statusMainPanel.Children.Add(statusCard);
        Grid.SetColumn(statusMainPanel, 1);

        contentGrid.Children.Add(logsMainPanel);
        contentGrid.Children.Add(statusMainPanel);
        Grid.SetRow(contentGrid, 1);

        // ================= НИЗ (ЗАПУСК - Со скруглёнными НИЖНИМИ углами) =================
        var bottomGrid = new Grid();
        bottomGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));

        // Статус запуска
        _launchStatus = new TextBlock
        {
            Text = "",
            FontSize = 11,
            Foreground = textMuted,
            IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(20, -18, 0, 0)
        };
        _launchProgress = new ProgressBar
        {
            Height = 3,
            Value = 0,
            Maximum = 100,
            Foreground = orangeAccent,
            Background = SolidColorBrush.Parse("#1E1E22"),
            IsVisible = false,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -6, 0, 0)
        };

        // Ряд кнопок и профиля
        var actionRow = new Grid { Margin = new Thickness(20, 10) };
        actionRow.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(220))); // Профиль
        actionRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));     // Узел
        actionRow.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(310))); // Кнопки

        // Профиль
        var profileStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 3 };
        _usernameLabel = new TextBlock
        {
            Text = _account.Username,
            FontSize = 16,
            FontWeight = FontWeight.Black,
            Foreground = Brushes.White
        };
        profileStack.Children.Add(_usernameLabel);
        profileStack.Children.Add(new TextBlock
        {
            Text = "● ОПЕРАТОР АВТОРИЗОВАН",
            FontSize = 10,
            FontWeight = FontWeight.Bold,
            Foreground = SolidColorBrush.Parse("#4CAF50"),
            LetterSpacing = 1.2
        });
        Grid.SetColumn(profileStack, 0);

        // Узел (сборка)
        var nodeStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20, 0), Spacing = 5 };
        nodeStack.Children.Add(new TextBlock
        {
            Text = "ВЫБЕРИТЕ ПРОМЫШЛЕННЫЙ УЗЕЛ",
            FontSize = 10,
            Foreground = textMuted,
            FontWeight = FontWeight.Bold,
            LetterSpacing = 1.2
        });
        var nodeSelector = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = cardBg,
            BorderBrush = borderGray
        };
        nodeSelector.Items.Add($"[{_manifest.MinecraftVersion}]  {_manifest.DisplayName}");
        nodeSelector.SelectedIndex = 0;
        nodeStack.Children.Add(nodeSelector);
        Grid.SetColumn(nodeStack, 1);

        // Кнопки: шестерёнка + запуск
        var actionStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        var settingsBtn = new Button
        {
            Content = "⚙",
            Width = 55,
            Height = 55,
            Background = SolidColorBrush.Parse("#2D2D32"),
            Foreground = Brushes.Gray,
            FontSize = 22,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(4)
        };
        settingsBtn.Click += (_, _) => OpenSettings();

        _startBtn = new Button
        {
            Content = "ЗАПУСТИТЬ СИСТЕМУ",
            Width = 230,
            Height = 55,
            Background = orangeAccent,
            Foreground = Brushes.White,
            FontSize = 16,
            FontWeight = FontWeight.Black,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(4)
        };
        _startBtn.Click += async (_, _) => await OnStartClickedAsync();

        actionStack.Children.Add(settingsBtn);
        actionStack.Children.Add(_startBtn);
        Grid.SetColumn(actionStack, 2);

        actionRow.Children.Add(profileStack);
        actionRow.Children.Add(nodeStack);
        actionRow.Children.Add(actionStack);
        Grid.SetRow(actionRow, 0);

        bottomGrid.Children.Add(actionRow);
        bottomGrid.Children.Add(_launchStatus);
        bottomGrid.Children.Add(_launchProgress);

        // Обертка подвала со скруглением только нижних углов!
        var bottomPanel = new Border
        {
            Background = panelBg,
            BorderBrush = borderGray,
            BorderThickness = new Thickness(0, 1, 0, 0),
            CornerRadius = new CornerRadius(0, 0, 12, 12),
            ClipToBounds = true,
            Child = bottomGrid
        };
        Grid.SetRow(bottomPanel, 2);

        mainGrid.Children.Add(topHeader);
        mainGrid.Children.Add(contentGrid);
        mainGrid.Children.Add(bottomPanel);

        // ================= ВНЕШНЯЯ ОБЕРТКА СО СВЕЧЕНИЕМ =================
        var orangeColor = Color.Parse("#FF6B00");

        var outerBorder = new Border
        {
            Margin = new Thickness(20),
            CornerRadius = new CornerRadius(12),
            Background = SolidColorBrush.Parse("#1A1A1E"),
            BorderBrush = SolidColorBrush.Parse("#E5731C"),
            BorderThickness = new Thickness(1),
            
            // Неоновое свечение наружу + мягкая глубокая тень
            BoxShadow = BoxShadows.Parse("0 0 22 2 #DCFF6B00, 0 10 30 0 #B4000000")
        };

        outerBorder.Child = mainGrid;
        Content = outerBorder;

        // Фоновые задачи
        Dispatcher.UIThread.InvokeAsync(LoadTelegramNews);
        _ = StartServerMonitoringAsync();
    }

    // ==================== ШАПКА ====================
    private Control BuildHeader()
    {
        var orange = SolidColorBrush.Parse("#E5731C");
        var topHeaderGrid = new Grid();
        topHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(180)));
        topHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        topHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(180)));

        // Перетаскивание окна за шапку
        topHeaderGrid.PointerPressed += (s, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        };

        // Логотип
        var textGradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(Color.Parse("#FF8C00"), 0.0),
                new GradientStop(Color.Parse("#E5731C"), 0.5),
                new GradientStop(Color.Parse("#993D00"), 1.0)
            }
        };
        var logoStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        logoStack.Children.Add(new TextBlock
        {
            Text = "VELES TECH",
            FontSize = 38,
            FontWeight = FontWeight.Heavy,
            Foreground = textGradient,
            LetterSpacing = 4,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        logoStack.Children.Add(new TextBlock
        {
            Text = "СЕТЬ ПРОМЫШЛЕННОЙ АВТОМАТИЗАЦИИ",
            FontSize = 10,
            FontWeight = FontWeight.Bold,
            Foreground = SolidColorBrush.Parse("#DCDCD0"),
            LetterSpacing = 1.5,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        });
        Grid.SetColumn(logoStack, 1);

        // Механизмы по бокам
        var left = TryBuildSprite("Assets/gear_anim.png", 1798, 30, 5.0);
        var right = TryBuildSprite("Assets/gear_anim.png", 1798, 30, 5.0);
        if (left != null) { Grid.SetColumn(left, 0); topHeaderGrid.Children.Add(left); }
        if (right != null) { Grid.SetColumn(right, 2); topHeaderGrid.Children.Add(right); }

        topHeaderGrid.Children.Add(logoStack);

        // ================= КНОПКИ СВЕРНУТЬ / ЗАКРЫТЬ =================
        var windowControls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 8, 8, 0),
            Spacing = 6
        };

        var minimizeBtn = new Button
        {
            Content = "—",
            Width = 28,
            Height = 28,
            Background = SolidColorBrush.Parse("#30000000"),
            Foreground = Brushes.LightGray,
            FontSize = 12,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(4)
        };
        minimizeBtn.Click += (_, _) => WindowState = WindowState.Minimized;

        var closeBtn = new Button
        {
            Content = "✕",
            Width = 28,
            Height = 28,
            Background = SolidColorBrush.Parse("#40E5731C"),
            Foreground = Brushes.White,
            FontSize = 12,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(4)
        };
        closeBtn.Click += (_, _) => Close();

        windowControls.Children.Add(minimizeBtn);
        windowControls.Children.Add(closeBtn);

        Grid.SetColumn(windowControls, 2);
        topHeaderGrid.Children.Add(windowControls);

        // Обертка шапки со скруглёнными ВЕРХНИМИ углами
        var headerBorder = new Border
        {
            CornerRadius = new CornerRadius(12, 12, 0, 0),
            ClipToBounds = true,
            Child = topHeaderGrid
        };

        // Назначаем картинку фона на скруглённый Border
        try
        {
            var uri = new Uri("avares://VelesTech/Assets/header_bg.png");
            using var stream = AssetLoader.Open(uri);
            var bmp = new Bitmap(stream);
            headerBorder.Background = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill, AlignmentY = AlignmentY.Bottom };
        }
        catch
        {
            headerBorder.Background = SolidColorBrush.Parse("#121214");
        }

        return headerBorder;
    }

    private SpriteAnimationControl? TryBuildSprite(string path, int frames, int fps, double speed)
    {
        try
        {
            return new SpriteAnimationControl(path, frames, fps, speed)
            {
                Width = 60,
                Height = 135,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        catch { return null; }
    }

    // ==================== ЗАПУСК ИГРЫ ====================
    private async Task OnStartClickedAsync()
    {
        _startBtn.IsEnabled = false;
        _launchProgress.IsVisible = true;
        _launchStatus.IsVisible = true;
        _launchProgress.Value = 0;
        _launchCts = new CancellationTokenSource();

        try
        {
            var installer = new ModpackInstaller(_manifest, _config);
            installer.OnProgress += (pct, mbDone, mbTotal, status) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _launchProgress.IsIndeterminate = false;
                    _launchProgress.Value = pct;
                    _launchStatus.Text = status;
                    _startBtn.Content = status;
                });
            };

            if (!installer.IsInstalled())
            {
                _startBtn.Content = "УСТАНОВКА СБОРКИ...";
                await installer.InstallAsync(_launchCts.Token);
            }
            else
            {
                _launchStatus.Text = "Сборка на месте, проверка библиотек...";
            }

            _startBtn.Content = "ЗАПУСК MINECRAFT...";
            _launchProgress.IsIndeterminate = true;

            var launcher = new MinecraftLauncher(_manifest, _config, _account);
            launcher.OnStatus += msg =>
            {
                Dispatcher.UIThread.Post(() => _launchStatus.Text = msg);
            };
            launcher.OnGameLog += msg =>
            {
                System.Diagnostics.Debug.WriteLine("[MC] " + msg);
            };

            await launcher.LaunchAsync();

            _launchStatus.Text = "Клиент Minecraft закрыт.";
        }
        catch (Exception ex)
        {
            _launchStatus.Text = $"ОШИБКА: {ex.Message}";
            _startBtn.Content = "ОШИБКА — см. журнал";
            System.Diagnostics.Debug.WriteLine($"[LAUNCH ERROR] {ex}");
        }
        finally
        {
            _launchProgress.IsIndeterminate = false;
            _launchProgress.Value = 0;
            await Task.Delay(1500);
            _launchProgress.IsVisible = false;
            _launchStatus.IsVisible = false;
            _startBtn.IsEnabled = true;
            _startBtn.Content = "ЗАПУСТИТЬ СИСТЕМУ";
        }
    }

    // ==================== НАСТРОЙКИ ====================
    private void OpenSettings()
    {
        var settings = new SettingsWindow(_config);
        settings.OnReinstallRequested = () =>
        {
            _launchStatus.Text = "Сборка помечена для переустановки. Нажмите «ЗАПУСТИТЬ СИСТЕМУ».";
        };
        settings.OnLogout = () =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var login = new LoginWindow();
                login.OnLoginSuccess = (acc) =>
                {
                    var main = new MainWindow(acc);
                    desktop.MainWindow = main;
                    main.Show();
                    login.Close();
                };
                desktop.MainWindow = login;
                login.Show();
                Close();
            }
        };
        settings.ShowDialog(this);
    }

    // ==================== TELEGRAM ЛЕНТА ====================
    private async void LoadTelegramNews()
    {
        string channelName = "Kimi_Moris";
        string url = $"https://t.me/s/{channelName}";
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            string html = await client.GetStringAsync(url);
            var allPosts = new List<string>();
            string marker = "<div class=\"tgme_widget_message_text js-message_text\"";
            int idx = 0;
            while ((idx = html.IndexOf(marker, idx)) != -1)
            {
                int textStart = html.IndexOf(">", idx) + 1;
                int textEnd = html.IndexOf("</div>", textStart);
                if (textEnd > textStart)
                {
                    string raw = html.Substring(textStart, textEnd - textStart);
                    string clean = raw.Replace("<br/>", "\n").Replace("<br>", "\n");
                    clean = Regex.Replace(clean, "<[^>]*>", "");
                    clean = System.Net.WebUtility.HtmlDecode(clean);
                    if (!string.IsNullOrWhiteSpace(clean)) allPosts.Add(clean.Trim());
                }
                idx = textEnd;
            }

            _cryptoLogsContainer.Children.Clear();
            if (allPosts.Count > 0)
            {
                allPosts.Reverse();
                int show = Math.Min(allPosts.Count, 5);
                for (int i = 0; i < show; i++)
                {
                    _cryptoLogsContainer.Children.Add(CreateNewsCard(
                        SolidColorBrush.Parse("#1E1E22"),
                        SolidColorBrush.Parse("#2D2D32"),
                        SolidColorBrush.Parse("#7E7E84"),
                        $"ОБЪЯВЛЕНИЕ СЕТИ #{i + 1}",
                        DateTime.Now.ToString("dd.MM.yyyy"),
                        allPosts[i]));
                }
            }
            else
            {
                ShowErrorCard("Лента новостей пуста.");
            }
        }
        catch (Exception ex)
        {
            ShowErrorCard($"Не удалось подключиться к сети автоматизации: {ex.Message}");
        }
    }

    private void ShowErrorCard(string msg)
    {
        _cryptoLogsContainer.Children.Clear();
        _cryptoLogsContainer.Children.Add(CreateNewsCard(
            SolidColorBrush.Parse("#1E1E22"),
            SolidColorBrush.Parse("#2D2D32"),
            SolidColorBrush.Parse("#7E7E84"),
            "ОШИБКА СИНХРОНИЗАЦИИ",
            "--.--.----",
            msg));
    }

    private Border CreateNewsCard(IBrush cardBg, IBrush borderGray, IBrush textMuted, string title, string date, string text)
    {
        var card = new Border
        {
            Background = cardBg,
            BorderBrush = borderGray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(15)
        };
        var content = new StackPanel { Spacing = 6 };
        var headerGrid = new Grid();
        headerGrid.Children.Add(new TextBlock
        {
            Text = title.ToUpper(),
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Left
        });
        headerGrid.Children.Add(new TextBlock
        {
            Text = date,
            Foreground = textMuted,
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        });
        var body = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = SolidColorBrush.Parse("#DCDCD0"),
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0)
        };
        content.Children.Add(headerGrid);
        content.Children.Add(body);
        card.Child = content;
        return card;
    }

    // ==================== МОНИТОРИНГ СЕРВЕРА ====================
    private async Task StartServerMonitoringAsync()
    {
        while (true)
        {
            var result = await ServerMonitorService.CheckServerAsync(_manifest.ServerIp, _manifest.ServerPort);

            Dispatcher.UIThread.Post(() =>
            {
                _statusStack.Children.Clear();

                if (result.IsOnline)
                {
                    _statusStack.Children.Add(CreateStatusRow(
                        $"Фабрика Альфа [{result.Version}]",
                        result.StatusText,
                        result.LoadFactor,
                        SolidColorBrush.Parse("#FF6B00")));
                }
                else
                {
                    var placeholder = new TextBlock
                    {
                        Text = "НЕТ ДОСТУПНЫХ СЕРВЕРОВ",
                        FontSize = 12,
                        FontWeight = FontWeight.Bold,
                        Foreground = Brushes.Gray,
                        LetterSpacing = 1.5,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 50, 0, 0)
                    };
                    _statusStack.Children.Add(placeholder);
                }
            });

            await Task.Delay(7000);
        }
    }

    private Grid CreateStatusRow(string title, string status, double progress, IBrush progressColor)
    {
        var rowGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        rowGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        rowGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var textGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        textGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        textGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        var titleBlock = new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Left,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var statusBlock = new TextBlock
        {
            Text = status.ToUpper(),
            FontSize = 11,
            FontWeight = FontWeight.Black,
            Foreground = progressColor,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(10, 0, 0, 0)
        };
        Grid.SetColumn(titleBlock, 0);
        Grid.SetColumn(statusBlock, 1);
        textGrid.Children.Add(titleBlock);
        textGrid.Children.Add(statusBlock);

        var bar = new ProgressBar
        {
            Value = progress * 100,
            Maximum = 100,
            Height = 6,
            Foreground = progressColor,
            Background = SolidColorBrush.Parse("#1E1E22"),
            BorderBrush = SolidColorBrush.Parse("#2D2D32"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        Grid.SetRow(textGrid, 0);
        Grid.SetRow(bar, 1);
        rowGrid.Children.Add(textGrid);
        rowGrid.Children.Add(bar);
        return rowGrid;
    }
}