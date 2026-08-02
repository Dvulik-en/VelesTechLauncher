using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using VelesTech.Models;
using VelesTech.Services;

namespace VelesTech.Views;

/// <summary>
/// Окно "шестерёнка" — все настройки лаунчера.
/// - Папка установки сборки
/// - Слайдер выделения ОЗУ
/// - Полноэкранный / разрешение
/// - Своя Java
/// - Кнопка «Проверить/переустановить сборку»
/// - Выход из аккаунта
/// </summary>
public class SettingsWindow : Window
{
    private readonly LauncherConfig _config;

    /// <summary>Игрок нажал «Переустановить сборку» — сообщим главному окну.</summary>
    public Action? OnReinstallRequested;

    /// <summary>Игрок вышел из аккаунта — главное окно должно закрыться.</summary>
    public Action? OnLogout;

    public SettingsWindow(LauncherConfig config)
    {
        _config = config;
        Title = "VELES TECH // НАСТРОЙКИ СИСТЕМЫ";
        Width = 620;
        Height = 700;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        Background = SolidColorBrush.Parse("#1A1A1E");

        SystemDecorations = SystemDecorations.None;

        Background = Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };

        var orange = SolidColorBrush.Parse("#E5731C");
        var muted = SolidColorBrush.Parse("#7E7E84");
        var border = SolidColorBrush.Parse("#2D2D32");

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };
        var stack = new StackPanel
        {
            Margin = new Thickness(25),
            Spacing = 18
        };

        // ============ КАСТОМНЫЙ TITLEBAR ДЛЯ НАСТРОЕК ============
        var titleBar = new Grid
        {
            Height = 32,
            Margin = new Thickness(-25, -25, -25, 10), // Растягиваем плашку на всю ширину
            Background = SolidColorBrush.Parse("#121214")
        };
        titleBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        titleBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        // Перетаскивание окна за шапку
        titleBar.PointerPressed += (s, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        };

        var titleText = new TextBlock
        {
            Text = "⚙  ПАРАМЕТРЫ ФАБРИКИ",
            FontSize = 13,
            FontWeight = FontWeight.Black,
            Foreground = orange,
            LetterSpacing = 1.5,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(15, 0, 0, 0)
        };

        var closeBtn = new Button
        {
            Content = "✕",
            Width = 40,
            Height = 32,
            Background = Brushes.Transparent,
            Foreground = SolidColorBrush.Parse("#88888D"),
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(0)
        };
        closeBtn.Click += (_, _) => Close();
        closeBtn.PointerEntered += (_, _) => { closeBtn.Background = SolidColorBrush.Parse("#C42B1C"); closeBtn.Foreground = Brushes.White; };
        closeBtn.PointerExited += (_, _) => { closeBtn.Background = Brushes.Transparent; closeBtn.Foreground = SolidColorBrush.Parse("#88888D"); };

        Grid.SetColumn(titleText, 0);
        Grid.SetColumn(closeBtn, 1);
        titleBar.Children.Add(titleText);
        titleBar.Children.Add(closeBtn);

        stack.Children.Add(titleBar);
        // =========================================================

        // ============ ПАПКА УСТАНОВКИ ============
        stack.Children.Add(Section("КАТАЛОГ СБОРКИ"));
        var pathText = new TextBox
        {
            Text = _config.GameDirectory,
            IsReadOnly = true,
            FontSize = 12,
            Background = SolidColorBrush.Parse("#1E1E22"),
            Foreground = Brushes.White,
            BorderBrush = border,
            Padding = new Thickness(8),
            Height = 36,
            CornerRadius = new CornerRadius(4)
        };
        var pathBtn = new Button
        {
            Content = "ВЫБРАТЬ ПАПКУ...",
            Height = 36,
            Background = SolidColorBrush.Parse("#2D2D32"),
            Foreground = Brushes.White,
            FontWeight = FontWeight.Bold,
            FontSize = 11,
            CornerRadius = new CornerRadius(4)
        };
        pathBtn.Click += async (_, _) =>
        {
            var storage = StorageProvider;
            if (storage == null) return;
            var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Выберите каталог сборки VelesTech",
                AllowMultiple = false
            });
            if (folders != null && folders.Count > 0)
            {
                _config.GameDirectory = folders[0].Path.LocalPath;
                pathText.Text = _config.GameDirectory;
            }
        };
        var pathRow = new Grid();
        pathRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        pathRow.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(160)));
        Grid.SetColumn(pathText, 0);
        Grid.SetColumn(pathBtn, 1);
        pathBtn.Margin = new Thickness(10, 0, 0, 0);
        pathRow.Children.Add(pathText);
        pathRow.Children.Add(pathBtn);
        stack.Children.Add(pathRow);

        // ============ ОЗУ (СЛАЙДЕР) ============
        stack.Children.Add(Section("ВЫДЕЛЕНИЕ ОПЕРАТИВНОЙ ПАМЯТИ (JVM Heap)"));

        var ramLabel = new TextBlock
        {
            Text = $"{_config.MaxRamMb / 1024.0:F1} ГБ",
            FontSize = 22,
            FontWeight = FontWeight.Black,
            Foreground = orange,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var ramSlider = new Slider
        {
            Minimum = 2,     // 2 ГБ минимум
            Maximum = 16,    // 16 ГБ максимум
            Value = _config.MaxRamMb / 1024.0,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            SmallChange = 1,
            LargeChange = 1
        };
        ramSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(Slider.Value))
            {
                double gb = Math.Round(ramSlider.Value);
                _config.MaxRamMb = (int)(gb * 1024);
                ramLabel.Text = $"{gb:F0} ГБ";
            }
        };
        stack.Children.Add(ramLabel);
        stack.Children.Add(ramSlider);

        // ============ ПОЛНОЭКРАННЫЙ + РАЗРЕШЕНИЕ ============
        stack.Children.Add(Section("ОКНО ИГРЫ"));
        var fullscreenCheck = new CheckBox
        {
            Content = "Запускать в полноэкранном режиме",
            IsChecked = _config.Fullscreen,
            Foreground = Brushes.White
        };
        fullscreenCheck.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(CheckBox.IsChecked))
                _config.Fullscreen = fullscreenCheck.IsChecked ?? false;
        };
        stack.Children.Add(fullscreenCheck);

        var resGrid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        resGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        resGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(20)));
        resGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        var widthBox = MakeNumberBox(_config.WindowWidth);
        var heightBox = MakeNumberBox(_config.WindowHeight);
        widthBox.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(TextBox.Text) && int.TryParse(widthBox.Text, out int v)) _config.WindowWidth = v;
        };
        heightBox.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(TextBox.Text) && int.TryParse(heightBox.Text, out int v)) _config.WindowHeight = v;
        };
        Grid.SetColumn(widthBox, 0);
        Grid.SetColumn(new TextBlock { Text = "×", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Foreground = muted }, 1);
        Grid.SetColumn(heightBox, 2);
        resGrid.Children.Add(widthBox);
        var x = new TextBlock { Text = "×", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Foreground = muted };
        Grid.SetColumn(x, 1);
        resGrid.Children.Add(x);
        resGrid.Children.Add(heightBox);
        stack.Children.Add(resGrid);

        // ============ СВОЯ JAVA ============
        stack.Children.Add(Section("СВОЯ JAVA (необязательно, по умолчанию используется /runtime/)"));
        var javaText = new TextBox
        {
            Text = _config.CustomJavaPath,
            Watermark = "Путь к javaw.exe (например C:\\Java\\jdk-17\\bin\\javaw.exe)",
            FontSize = 12,
            Background = SolidColorBrush.Parse("#1E1E22"),
            Foreground = Brushes.White,
            BorderBrush = border,
            Padding = new Thickness(8),
            Height = 36,
            CornerRadius = new CornerRadius(4)
        };
        var javaBtn = new Button
        {
            Content = "ВЫБРАТЬ...",
            Height = 36,
            Background = SolidColorBrush.Parse("#2D2D32"),
            Foreground = Brushes.White,
            FontWeight = FontWeight.Bold,
            FontSize = 11,
            CornerRadius = new CornerRadius(4)
        };
        javaBtn.Click += async (_, _) =>
        {
            var storage = StorageProvider;
            if (storage == null) return;
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Выберите javaw.exe (Java 17)",
                AllowMultiple = false
            });
            if (files != null && files.Count > 0)
            {
                _config.CustomJavaPath = files[0].Path.LocalPath;
                javaText.Text = _config.CustomJavaPath;
            }
        };
        javaText.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(TextBox.Text))
                _config.CustomJavaPath = javaText.Text ?? "";
        };
        var javaGrid = new Grid();
        javaGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        javaGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(130)));
        Grid.SetColumn(javaText, 0);
        Grid.SetColumn(javaBtn, 1);
        javaBtn.Margin = new Thickness(10, 0, 0, 0);
        javaGrid.Children.Add(javaText);
        javaGrid.Children.Add(javaBtn);
        stack.Children.Add(javaGrid);

        // ============ ПЕРЕУСТАНОВКА СБОРКИ ============
        stack.Children.Add(Section("ЦЕЛОСТНОСТЬ СБОРКИ"));
        var reinstallBtn = new Button
        {
            Content = "ПРОВЕРИТЬ / ПЕРЕУСТАНОВИТЬ СБОРКУ",
            Height = 42,
            Background = SolidColorBrush.Parse("#2D2D32"),
            Foreground = orange,
            FontWeight = FontWeight.Bold,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(4)
        };
        reinstallBtn.Click += (_, _) =>
        {
            _config.ClientInstalled = false; // Сбросим флаг — при следующем запуске лаунчер скачает заново
            ConfigService.Save(_config);
            OnReinstallRequested?.Invoke();
            Close();
        };
        stack.Children.Add(reinstallBtn);

        // ============ ВЫХОД ИЗ АККАУНТА ============
        var logoutBtn = new Button
        {
            Content = "ВЫЙТИ ИЗ АККАУНТА (СМЕНА ЛОГИНА)",
            Height = 42,
            Background = SolidColorBrush.Parse("#3A1E1E"),
            Foreground = SolidColorBrush.Parse("#FF6060"),
            FontWeight = FontWeight.Bold,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(4)
        };
        logoutBtn.Click += (_, _) =>
        {
            AuthService.Logout();
            OnLogout?.Invoke();
            Close();
        };
        stack.Children.Add(logoutBtn);

        // ============ КНОПКИ СНИЗУ ============
        var bottomButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 15, 0, 0)
        };
        var cancelBtn = new Button
        {
            Content = "ОТМЕНА",
            Width = 120,
            Height = 40,
            Background = SolidColorBrush.Parse("#2D2D32"),
            Foreground = Brushes.White
        };
        cancelBtn.Click += (_, _) => Close();
        var saveBtn = new Button
        {
            Content = "СОХРАНИТЬ",
            Width = 160,
            Height = 40,
            Background = orange,
            Foreground = Brushes.White,
            FontWeight = FontWeight.Black
        };
        saveBtn.Click += (_, _) =>
        {
            ConfigService.Save(_config);
            Close();
        };
        bottomButtons.Children.Add(cancelBtn);
        bottomButtons.Children.Add(saveBtn);
        stack.Children.Add(bottomButtons);

        scroll.Content = stack;

        // ВНУТРЕННИЙ БЛОК: Срезает контент по угловому радиусу
        var innerContent = new Border
        {
            Background = SolidColorBrush.Parse("#1A1A1E"),
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            Child = scroll
        };

        // ВНЕШНИЙ БЛОК: Оранжевая рамка + BoxShadow (неоновый ореол + тень)
        var outerBorder = new Border
        {
            Margin = new Thickness(25), // Пространство для рассеивания неонового ореола
            BorderBrush = SolidColorBrush.Parse("#E5731C"),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(8),
            
            // Яркий неоновый оранжевый ореол снаружи + мягкая темная тень снизу
            BoxShadow = BoxShadows.Parse("0 0 22 2 #DCFF6B00, 0 10 30 0 #B4000000"),
            
            Child = innerContent
        };

        Content = outerBorder;
    }
    

    private TextBlock Section(string title) => new()
    {
        Text = title,
        FontSize = 11,
        FontWeight = FontWeight.Bold,
        Foreground = SolidColorBrush.Parse("#7E7E84"),
        LetterSpacing = 1.5,
        Margin = new Thickness(0, 6, 0, 0)
    };

    private TextBox MakeNumberBox(int value) => new()
    {
        Text = value.ToString(),
        FontSize = 13,
        Height = 34,
        Background = SolidColorBrush.Parse("#1E1E22"),
        BorderBrush = SolidColorBrush.Parse("#2D2D32"),
        Foreground = Brushes.White,
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(8)
    };
}