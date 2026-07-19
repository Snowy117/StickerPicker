using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StickerPicker.Core.Abstractions;
using StickerPicker.Core.Models;

namespace StickerPicker.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IStickerLibrary _library;
    private readonly IConfigStore _configStore;
    private readonly IAppPaths _paths;
    private readonly IClipboardImageService _clipboard;
    private readonly IHotkeyService _hotkeyService;
    private readonly IWindowChromeService _windowChrome;
    private AppConfig _config;
    private bool _suppressCategoryChange;

    public MainViewModel(
        IStickerLibrary library,
        IConfigStore configStore,
        IAppPaths paths,
        IClipboardImageService clipboard,
        IHotkeyService hotkeyService,
        IWindowChromeService windowChrome)
    {
        _library = library;
        _configStore = configStore;
        _paths = paths;
        _clipboard = clipboard;
        _hotkeyService = hotkeyService;
        _windowChrome = windowChrome;
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
            onDataRootChanged: ReloadLibrary,
            onConfigApplied: ApplyConfigFromSettings);

        Categories = [];
        Stickers = [];
        SelectedCategory = null;
        SearchText = "";
        StatusText = "就绪";
        IsSettingsOpen = false;
    }

    public SettingsViewModel Settings { get; }

    public ObservableCollection<CategoryItemViewModel> Categories { get; }
    public ObservableCollection<StickerItemViewModel> Stickers { get; }

    [ObservableProperty]
    public partial CategoryItemViewModel? SelectedCategory { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial double ThumbnailSize { get; set; }

    [ObservableProperty]
    public partial bool AlwaysOnTop { get; set; }

    [ObservableProperty]
    public partial string Theme { get; set; }

    [ObservableProperty]
    public partial bool IsSettingsOpen { get; set; }

    public void ShowSettings() => IsSettingsOpen = true;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public void Initialize()
    {
        try
        {
            _paths.EnsureDataLayout();
            _library.Refresh();
            RebuildCategories();
            ApplyFilter();
            RegisterHotkeyFromConfig();
            _windowChrome.SetTopmost(_config.AlwaysOnTop);
            StatusText = $"{_library.Stickers.Count} 张表情";
        }
        catch (Exception ex)
        {
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

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedCategoryChanged(CategoryItemViewModel? value)
    {
        if (_suppressCategoryChange)
        {
            return;
        }

        ApplyFilter();
    }

    partial void OnThumbnailSizeChanged(double value)
    {
        _config.ThumbnailSize = value;
        _configStore.Save(_config);
        ApplyFilter();
    }

    [RelayCommand]
    private void RefreshLibrary()
    {
        try
        {
            _library.Refresh();
            RebuildCategories();
            ApplyFilter();
            StatusText = $"已刷新 · {_library.Stickers.Count} 张表情";
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
    private void CreateCategory(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            var created = _library.CreateCategory(name.Trim());
            RebuildCategories();
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == created.Id);
            StatusText = $"已创建分类 {created.Name}";
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void RenameCategory(string? newName)
    {
        if (SelectedCategory is null || SelectedCategory.IsVirtual || string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        try
        {
            var id = SelectedCategory.Id;
            _library.RenameCategory(id, newName.Trim());
            RebuildCategories();
            SelectedCategory = Categories.FirstOrDefault(c =>
                string.Equals(c.Name, newName.Trim(), StringComparison.OrdinalIgnoreCase));
            StatusText = "分类已重命名";
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void DeleteCategory(bool deleteFiles)
    {
        if (SelectedCategory is null || SelectedCategory.IsVirtual)
        {
            return;
        }

        try
        {
            _library.DeleteCategory(SelectedCategory.Id, deleteFiles);
            RebuildCategories();
            SelectedCategory = Categories.FirstOrDefault();
            ApplyFilter();
            StatusText = "分类已删除";
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

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
            var result = await _library.ImportAsync(list, categoryId);
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
    private void SelectSticker(StickerItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            if (!_clipboard.CopyImageFile(item.AbsolutePath))
            {
                ErrorMessage = "复制到剪贴板失败。";
                return;
            }

            ErrorMessage = null;
            StatusText = $"已复制 {item.FileName}";
            _windowChrome.Hide();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void AdjustThumbnail(int delta)
    {
        var next = Math.Clamp(ThumbnailSize + delta, 48, 256);
        ThumbnailSize = next;
    }

    private void ReloadLibrary()
    {
        _library.Refresh();
        RebuildCategories();
        ApplyFilter();
        StatusText = $"数据目录：{_paths.DataRoot}";
    }

    private void ApplyConfigFromSettings(AppConfig config)
    {
        _config = config.Clone();
        Theme = _config.Theme;
        AlwaysOnTop = _config.AlwaysOnTop;
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

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() => _windowChrome.ToggleVisible());
    }

    private void RebuildCategories()
    {
        var previousId = SelectedCategory?.Id ?? Category.AllId;
        _suppressCategoryChange = true;
        try
        {
            Categories.Clear();
            foreach (var category in _library.Categories)
            {
                Categories.Add(new CategoryItemViewModel(category));
            }

            SelectedCategory = Categories.FirstOrDefault(c => c.Id == previousId)
                ?? Categories.FirstOrDefault();
        }
        finally
        {
            _suppressCategoryChange = false;
        }
    }

    private void ApplyFilter()
    {
        var categoryId = SelectedCategory?.Id ?? Category.AllId;
        var results = _library.Query(categoryId, SearchText);
        Stickers.Clear();
        var select = SelectStickerCommand;
        foreach (var sticker in results)
        {
            Stickers.Add(new StickerItemViewModel(sticker, ThumbnailSize, select));
        }

        StatusText = $"{Stickers.Count} 张表情";
    }
}
