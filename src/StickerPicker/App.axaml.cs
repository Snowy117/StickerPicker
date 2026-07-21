using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Input;
using StickerPicker.Core.Abstractions;
using StickerPicker.Core.Config;
using StickerPicker.Core.Library;
using StickerPicker.Core.Models;
using StickerPicker.Core.Paths;
using StickerPicker.Services;
using StickerPicker.ViewModels;
using StickerPicker.Views;

namespace StickerPicker;

public partial class App : Application
{
    private IHotkeyService? _hotkeyService;
    private AvaloniaWindowChromeService? _windowChrome;
    private MainViewModel? _mainViewModel;
    private Task? _initializationTask;
    private bool _isShuttingDown;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ConfigureDesktop(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private IRelayCommand? ShowWindowCommand { get; set; }
    private IRelayCommand? ExitCommand { get; set; }
    private IRelayCommand? OpenSettingsCommand { get; set; }
    private NativeMenu? TrayMenu { get; set; }

    private void ConfigureDesktop(IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var paths = new AppPaths();
        paths.Resolve();
        var configStore = new ConfigStore(paths);
        var library = new FolderStickerLibrary(paths);
        _windowChrome = new AvaloniaWindowChromeService();

        var clipboard = ServiceFactory.CreateClipboard();
        _hotkeyService = ServiceFactory.CreateHotkey();

        var mainWindow = new MainWindow();
        _mainViewModel = new MainViewModel(
            library,
            configStore,
            paths,
            clipboard,
            _hotkeyService,
            _windowChrome);

        mainWindow.DataContext = _mainViewModel;
        _windowChrome.Attach(mainWindow);
        desktop.MainWindow = mainWindow;

        ApplyTheme(_mainViewModel.Theme);
        _mainViewModel.PropertyChanged += OnMainViewModelPropertyChanged;

        DataContext = this;
        WireTrayCommands();
        AttachTrayIconBindings();
        WireMainWindowOpened(mainWindow);
    }

    private void WireTrayCommands()
    {
        ShowWindowCommand = new RelayCommand(() => _windowChrome?.ToggleVisible());
        ExitCommand = new RelayCommand(() =>
        {
            _isShuttingDown = true;
            if (_initializationTask is { IsFaulted: true } failedInitialization)
            {
                _ = failedInitialization.Exception;
            }

            _initializationTask = null;
            _hotkeyService?.Dispose();
            _mainViewModel?.Shutdown();
            _windowChrome?.Shutdown();
        });
        OpenSettingsCommand = new RelayCommand(() =>
        {
            _windowChrome?.Show();
            if (_mainViewModel is { } vm)
            {
                vm.IsSettingsOpen = true;
            }
        });

        TrayMenu =
        [
            new NativeMenuItem("显示") { Command = ShowWindowCommand },
            new NativeMenuItem("设置") { Command = OpenSettingsCommand },
            new NativeMenuItemSeparator(),
            new NativeMenuItem("退出") { Command = ExitCommand },
        ];
    }

    private void AttachTrayIconBindings()
    {
        if (TrayIcon.GetIcons(this)?.FirstOrDefault() is not { } icon)
        {
            return;
        }

        icon.Command = ShowWindowCommand;
        icon.Menu = TrayMenu;
    }

    private void WireMainWindowOpened(MainWindow mainWindow)
    {
        mainWindow.Opened += OnMainWindowOpened;
    }

    private void OnMainWindowOpened(object? sender, EventArgs e)
    {
        if (sender is not MainWindow mainWindow || _mainViewModel is null || _windowChrome is null)
        {
            return;
        }

        mainWindow.Opened -= OnMainWindowOpened;
        if (_initializationTask is not null)
        {
            return;
        }

        var config = _mainViewModel.CurrentConfig;
        if (config.Window is { Width: > 0, Height: > 0 })
        {
            mainWindow.Width = config.Window.Width;
            mainWindow.Height = config.Window.Height;
        }

        _initializationTask = InitializeMainWindowAsync(config);
    }

    private async Task InitializeMainWindowAsync(AppConfig config)
    {
        if (_mainViewModel is null || _windowChrome is null)
        {
            return;
        }

        await _mainViewModel.InitializeAsync();
        if (!_isShuttingDown)
        {
            _windowChrome.SetTopmost(config.AlwaysOnTop);
        }
    }

    private void OnMainViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(MainViewModel.Theme), StringComparison.Ordinal)
            && _mainViewModel is { Theme: var theme })
        {
            ApplyTheme(theme);
        }
    }

    private void ApplyTheme(string theme)
    {
        RequestedThemeVariant = theme.ToLowerInvariant() switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };

        if (Resources is not ResourceDictionary resources)
        {
            return;
        }

        var useDark = theme.Equals("dark", StringComparison.OrdinalIgnoreCase)
            || (theme.Equals("system", StringComparison.OrdinalIgnoreCase)
                && ActualThemeVariant != ThemeVariant.Light);

        if (theme.Equals("light", StringComparison.OrdinalIgnoreCase))
        {
            useDark = false;
        }

        ApplySteamBrushes(resources, useDark);
    }

    private void ApplySteamBrushes(ResourceDictionary resources, bool useDark)
    {
        if (useDark)
        {
            SetSolidBrush(resources, "SteamBgBrush", "SteamDarkBg");
            SetSolidBrush(resources, "SteamHeaderBrush", "SteamDarkHeader");
            SetSolidBrush(resources, "SteamPanelBrush", "SteamDarkPanel");
            SetSolidBrush(resources, "SteamPanelAltBrush", "SteamDarkPanelAlt");
            SetSolidBrush(resources, "SteamHoverBrush", "SteamDarkHover");
            SetSolidBrush(resources, "SteamBorderBrush", "SteamDarkBorder");
            SetSolidBrush(resources, "SteamBorderSoftBrush", "SteamDarkBorderSoft");
            SetSolidBrush(resources, "SteamTextBrush", "SteamDarkText");
            SetSolidBrush(resources, "SteamTextBrightBrush", "SteamDarkTextBright");
            SetSolidBrush(resources, "SteamMutedBrush", "SteamDarkMuted");
            SetSolidBrush(resources, "SteamAccentBrush", "SteamAccent");
            SetSolidBrush(resources, "SteamAccentDimBrush", "SteamAccentDim");
            SetSolidBrush(resources, "SteamErrorBrush", "SteamErrorDark");
            SetGradientBrush(resources, "SteamHeaderGradientBrush",
                "SteamDarkPanel", "SteamDarkBg");
        }
        else
        {
            SetSolidBrush(resources, "SteamBgBrush", "SteamLightBg");
            SetSolidBrush(resources, "SteamHeaderBrush", "SteamLightHeader");
            SetSolidBrush(resources, "SteamPanelBrush", "SteamLightPanel");
            SetSolidBrush(resources, "SteamPanelAltBrush", "SteamLightPanelAlt");
            SetSolidBrush(resources, "SteamHoverBrush", "SteamLightHover");
            SetSolidBrush(resources, "SteamBorderBrush", "SteamLightBorder");
            SetSolidBrush(resources, "SteamBorderSoftBrush", "SteamLightBorderSoft");
            SetSolidBrush(resources, "SteamTextBrush", "SteamLightText");
            SetSolidBrush(resources, "SteamTextBrightBrush", "SteamLightTextBright");
            SetSolidBrush(resources, "SteamMutedBrush", "SteamLightMuted");
            SetSolidBrush(resources, "SteamAccentBrush", "SteamLightAccent");
            SetSolidBrush(resources, "SteamAccentDimBrush", "SteamLightAccentDim");
            SetSolidBrush(resources, "SteamErrorBrush", "SteamErrorLight");
            SetGradientBrush(resources, "SteamHeaderGradientBrush",
                "SteamLightHeader", "SteamLightBg");
        }
    }

    private void SetSolidBrush(ResourceDictionary resources, string key, string colorKey)
    {
        if (TryGetResource(colorKey, ActualThemeVariant, out var colorObj)
            && colorObj is Avalonia.Media.Color color)
        {
            resources[key] = new Avalonia.Media.SolidColorBrush(color);
        }
    }

    private void SetGradientBrush(
        ResourceDictionary resources,
        string key,
        string startColorKey,
        string endColorKey)
    {
        if (TryGetResource(startColorKey, ActualThemeVariant, out var startObj)
            && startObj is Avalonia.Media.Color start
            && TryGetResource(endColorKey, ActualThemeVariant, out var endObj)
            && endObj is Avalonia.Media.Color end)
        {
            resources[key] = new Avalonia.Media.LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new Avalonia.Media.GradientStop(start, 0),
                    new Avalonia.Media.GradientStop(end, 1),
                },
            };
        }
    }
}
