using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Input;
using StickerPicker.Core.Abstractions;
using StickerPicker.Core.Config;
using StickerPicker.Core.Library;
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

    public IRelayCommand? ShowWindowCommand { get; private set; }
    public IRelayCommand? ExitCommand { get; private set; }
    public IRelayCommand? OpenSettingsCommand { get; private set; }
    public NativeMenu? TrayMenu { get; private set; }

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
        WireMainWindowOpened(mainWindow);
    }

    private void WireTrayCommands()
    {
        ShowWindowCommand = new RelayCommand(() => _windowChrome?.Show());
        ExitCommand = new RelayCommand(() =>
        {
            _hotkeyService?.Dispose();
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

    private void WireMainWindowOpened(MainWindow mainWindow)
    {
        mainWindow.Opened += (_, _) =>
        {
            if (_mainViewModel is null || _windowChrome is null)
            {
                return;
            }

            var config = _mainViewModel.CurrentConfig;
            if (config.Window.Width > 0 && config.Window.Height > 0)
            {
                mainWindow.Width = config.Window.Width;
                mainWindow.Height = config.Window.Height;
            }

            _mainViewModel.Initialize();
            _windowChrome.SetTopmost(config.AlwaysOnTop);
        };
    }

    private void OnMainViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(MainViewModel.Theme), StringComparison.Ordinal)
            && _mainViewModel is not null)
        {
            ApplyTheme(_mainViewModel.Theme);
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
        void SetBrush(string key, string colorKey)
        {
            if (TryGetResource(colorKey, ActualThemeVariant, out var colorObj)
                && colorObj is Avalonia.Media.Color color)
            {
                resources[key] = new Avalonia.Media.SolidColorBrush(color);
            }
        }

        if (useDark)
        {
            SetBrush("SteamBgBrush", "SteamDarkBg");
            SetBrush("SteamPanelBrush", "SteamDarkPanel");
            SetBrush("SteamPanelAltBrush", "SteamDarkPanelAlt");
            SetBrush("SteamBorderBrush", "SteamDarkBorder");
            SetBrush("SteamTextBrush", "SteamDarkText");
            SetBrush("SteamMutedBrush", "SteamDarkMuted");
            SetBrush("SteamAccentBrush", "SteamAccent");
        }
        else
        {
            SetBrush("SteamBgBrush", "SteamLightBg");
            SetBrush("SteamPanelBrush", "SteamLightPanel");
            SetBrush("SteamPanelAltBrush", "SteamLightPanelAlt");
            SetBrush("SteamBorderBrush", "SteamLightBorder");
            SetBrush("SteamTextBrush", "SteamLightText");
            SetBrush("SteamMutedBrush", "SteamLightMuted");
            SetBrush("SteamAccentBrush", "SteamLightAccent");
        }
    }
}
