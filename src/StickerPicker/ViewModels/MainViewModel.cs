using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StickerPicker.Core.Abstractions;
using StickerPicker.Core.Models;
using StickerPicker.Services;

namespace StickerPicker.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IStickerLibrary _library;
    private readonly IConfigStore _configStore;
    private readonly IAppPaths _paths;
    private readonly IHotkeyService _hotkeyService;
    private readonly IWindowChromeService _windowChrome;
    private readonly SemaphoreSlim _libraryOperationGate = new(1, 1);
    private IReadOnlyList<Category> _libraryCategories = [];
    private IReadOnlyList<Sticker> _libraryStickers = [];
    private AppConfig _config;
    private bool _suppressCategoryChange;
    private readonly bool _isReady;
    private DispatcherTimer? _thumbnailSaveTimer;
    private DispatcherTimer? _thumbnailResizeTimer;
    private DispatcherTimer? _thumbnailDecodeTimer;
    private DispatcherTimer? _searchTimer;
    private bool _isShutdown;

    public MainViewModel(
        IStickerLibrary library,
        IConfigStore configStore,
        IAppPaths paths,
        IClipboardImageService clipboard,
        IHotkeyService hotkeyService,
        IWindowChromeService windowChrome,
        IForegroundInputService foregroundInput)
    {
        _library = library;
        _configStore = configStore;
        _paths = paths;
        _hotkeyService = hotkeyService;
        _windowChrome = windowChrome;
        _clipboard = clipboard;
        _foregroundInput = foregroundInput;
        _selection = new SelectionCoordinator(clipboard, foregroundInput, windowChrome, TimeProvider.System);
        _clipboard.RecoveryInvalidated += OnRecoveryInvalidated;
        _config = _configStore.Load();

        ThumbnailSize = _config.ThumbnailSize;
        AlwaysOnTop = _config.AlwaysOnTop;
        Theme = _config.Theme;

        Settings = new SettingsViewModel(
            _configStore,
            _paths,
            _hotkeyService,
            _windowChrome,
            _config,
            onDataRootChanged: ChangeDataRootAsync,
            onConfigApplied: ApplyConfigFromSettings);

        _isReady = true;
    }

    public SettingsViewModel Settings { get; }

    public ObservableCollection<CategoryItemViewModel> Categories { get; } = [];
    public ObservableCollection<StickerItemViewModel> Stickers { get; } = [];

    [ObservableProperty]
    public partial CategoryItemViewModel? SelectedCategory { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial string StatusText { get; set; } = "就绪";

    [ObservableProperty]
    public partial string HoveredFileName { get; set; } = "";

    [ObservableProperty]
    public partial double ThumbnailSize { get; set; }

    [ObservableProperty]
    public partial bool AlwaysOnTop { get; set; }

    [ObservableProperty]
    public partial string Theme { get; set; }

    [ObservableProperty]
    public partial bool IsSettingsOpen { get; set; }

    [ObservableProperty]
    public partial bool IsTagEditorOpen { get; set; }

    [ObservableProperty]
    public partial StickerItemViewModel? TagEditorTarget { get; set; }

    public void ShowSettings() => IsSettingsOpen = true;

    public void OpenTagEditor(StickerItemViewModel item)
    {
        TagEditorTarget = item;
        IsTagEditorOpen = true;
    }

    public void CloseTagEditor()
    {
        IsTagEditorOpen = false;
        TagEditorTarget = null;
    }

    [RelayCommand]
    private void CloseTagEditorCommand() => CloseTagEditor();

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public async Task InitializeAsync()
    {
        try
        {
            await RunLibraryOperationAsync(async () =>
            {
                await Task.Run(() =>
                {
                    _paths.EnsureDataLayout();
                    _library.Refresh();
                });
            });
            if (_isShutdown)
            {
                return;
            }

            RebuildCategories();
            ApplyFilter();
            RegisterHotkeyFromConfig();
            _windowChrome.SetTopmost(_config.AlwaysOnTop);
            StatusText = $"{_libraryStickers.Count} 张表情";
        }
        catch (Exception ex)
        {
            if (_isShutdown)
            {
                return;
            }

            ErrorMessage = ex.Message;
            StatusText = "启动失败";
        }
    }

    public void PersistWindowGeometry(double x, double y, double width, double height)
    {
        _config.Window.X = x;
        _config.Window.Y = y;
        _config.Window.Width = width;
        _config.Window.Height = height;
        _configStore.Save(_config);
    }

    public AppConfig CurrentConfig => _config.Clone();

    partial void OnSelectedCategoryChanged(CategoryItemViewModel? value)
    {
        _ = value;
        if (!_isReady || _suppressCategoryChange)
        {
            return;
        }

        ApplyFilter();
    }

    public void Shutdown()
    {
        _isShutdown = true;
        _thumbnailResizeTimer?.Stop();
        _thumbnailDecodeTimer?.Stop();
        _thumbnailSaveTimer?.Stop();
        _searchTimer?.Stop();
        ShutdownSelection();
        DisposeStickers(Stickers);
        Stickers.Clear();
    }

    [RelayCommand]
    private async Task RefreshLibraryAsync()
    {
        try
        {
            await RunLibraryOperationAsync(() => Task.Run(_library.Refresh));
            if (_isShutdown)
            {
                return;
            }

            RebuildCategories();
            ApplyFilter(forceThumbnailReload: true);
            StatusText = $"已刷新 · {_libraryStickers.Count} 张表情";
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void OpenSettings() => IsSettingsOpen = true;

    [RelayCommand]
    private void CloseSettings() => IsSettingsOpen = false;

    [RelayCommand]
    private async Task ImportPathsAsync(IEnumerable<string>? paths)
    {
        if (paths is null)
        {
            return;
        }

        var list = paths.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (list.Count == 0)
        {
            return;
        }

        try
        {
            var categoryId = SelectedCategory?.Id;
            var result = await RunLibraryOperationAsync(
                () => Task.Run(() => _library.ImportAsync(list, categoryId)));
            if (_isShutdown)
            {
                return;
            }

            RebuildCategories();
            ApplyFilter();
            StatusText = result.Summary;
            ErrorMessage = result.Failed > 0
                ? $"部分失败：{string.Join(", ", result.FailedPaths.Take(3))}"
                : null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void AdjustThumbnail(int delta)
    {
        ThumbnailSize = Math.Clamp(ThumbnailSize + delta, 48, 256);
    }

    private async Task ChangeDataRootAsync(Func<Task> changeDataRoot)
    {
        await RunLibraryOperationAsync(async () =>
        {
            await changeDataRoot();
            await Task.Run(_library.Refresh);
        });
        if (_isShutdown)
        {
            return;
        }

        RebuildCategories();
        ApplyFilter();
        StatusText = $"数据目录：{_paths.DataRoot}";
    }

    private async Task RunLibraryOperationAsync(Func<Task> operation)
    {
        await _libraryOperationGate.WaitAsync();
        try
        {
            await operation();
            RefreshLibrarySnapshot();
        }
        finally
        {
            _libraryOperationGate.Release();
        }
    }

    private async Task<T> RunLibraryOperationAsync<T>(Func<Task<T>> operation)
    {
        await _libraryOperationGate.WaitAsync();
        try
        {
            var result = await operation();
            RefreshLibrarySnapshot();
            return result;
        }
        finally
        {
            _libraryOperationGate.Release();
        }
    }

    private void RefreshLibrarySnapshot()
    {
        _libraryCategories = [.. _library.Categories];
        _libraryStickers = [.. _library.Stickers];
    }

    private void ApplyConfigFromSettings(AppConfig config)
    {
        _config = config.Clone();
        Theme = _config.Theme;
        AlwaysOnTop = _config.AlwaysOnTop;
        if (_config.ClipboardRestoreDelaySeconds == 0)
        {
            StopRestoreCountdown(cancelClipboard: true);
        }
    }

    private void RegisterHotkeyFromConfig()
    {
        _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        if (!_hotkeyService.Register(_config.Hotkey))
        {
            ErrorMessage = $"热键 {_config.Hotkey} 注册失败，请在设置中更换。";
        }
    }

    private void RebuildCategories()
    {
        var previousId = SelectedCategory?.Id ?? Category.AllId;
        _suppressCategoryChange = true;
        try
        {
            Categories.Clear();
            foreach (var category in _libraryCategories)
            {
                Categories.Add(new CategoryItemViewModel(category));
            }

            SelectedCategory = Categories.FirstOrDefault(c =>
                    string.Equals(c.Id, previousId, StringComparison.Ordinal))
                ?? Categories.FirstOrDefault();
        }
        finally
        {
            _suppressCategoryChange = false;
        }
    }

    private void DisposeStickers(IEnumerable<StickerItemViewModel> stickers)
    {
        foreach (var sticker in stickers)
        {
            _activeStickers.Remove(sticker);
            sticker.Dispose();
        }
    }
}
