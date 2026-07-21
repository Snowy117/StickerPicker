using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StickerPicker.Core.Abstractions;
using StickerPicker.Core.Models;

namespace StickerPicker.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IConfigStore _configStore;
    private readonly IAppPaths _paths;
    private readonly IHotkeyService _hotkeyService;
    private readonly IWindowChromeService _windowChrome;
    private readonly Func<Func<Task>, Task> _onDataRootChanged;
    private readonly Action<AppConfig> _onConfigApplied;
    private readonly AppConfig _config;
    private readonly bool _isReady;

    public SettingsViewModel(
        IConfigStore configStore,
        IAppPaths paths,
        IHotkeyService hotkeyService,
        IWindowChromeService windowChrome,
        AppConfig config,
        Func<Func<Task>, Task> onDataRootChanged,
        Action<AppConfig> onConfigApplied)
    {
        _configStore = configStore;
        _paths = paths;
        _hotkeyService = hotkeyService;
        _windowChrome = windowChrome;
        _onDataRootChanged = onDataRootChanged;
        _onConfigApplied = onConfigApplied;
        _config = config.Clone();

        Theme = _config.Theme;
        AlwaysOnTop = _config.AlwaysOnTop;
        Hotkey = _config.Hotkey;
        HoverPreview = _config.HoverPreview;
        PreviewOpacity = _config.PreviewOpacity;
        UseGpuRendering = _config.UseGpuRendering;
        DataRootDisplay = _paths.DataRoot;
        StatusMessage = "";
        _isReady = true;
    }

    public IReadOnlyList<string> ThemeOptions { get; } = ["system", "dark", "light"];

    [ObservableProperty]
    public partial string Theme { get; set; }

    [ObservableProperty]
    public partial bool AlwaysOnTop { get; set; }

    [ObservableProperty]
    public partial bool HoverPreview { get; set; }

    [ObservableProperty]
    public partial double PreviewOpacity { get; set; }

    [ObservableProperty]
    public partial bool UseGpuRendering { get; set; }

    [ObservableProperty]
    public partial string Hotkey { get; set; }

    [ObservableProperty]
    public partial string DataRootDisplay { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    [ObservableProperty]
    public partial bool CopyOnMigrate { get; set; } = true;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (!_isReady)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(Theme):
            case nameof(AlwaysOnTop):
            case nameof(HoverPreview):
            case nameof(PreviewOpacity):
            case nameof(UseGpuRendering):
                ApplyAndSave();
                break;
        }
    }

    [RelayCommand]
    private void SaveHotkey()
    {
        if (string.IsNullOrWhiteSpace(Hotkey))
        {
            StatusMessage = "热键不能为空。";
            return;
        }

        if (!_hotkeyService.Register(Hotkey.Trim()))
        {
            StatusMessage = "热键注册失败（可能冲突），请更换组合键。";
            Hotkey = _config.Hotkey;
            return;
        }

        _config.Hotkey = Hotkey.Trim();
        Persist();
        StatusMessage = "热键已保存。";
    }

    [RelayCommand]
    private async Task UseDefaultDataRootAsync()
    {
        try
        {
            await _onDataRootChanged(() =>
            {
                _paths.SetDataRoot(customDataRoot: null);
                DataRootDisplay = _paths.DataRoot;
                _config.DataRoot = null;
                Persist();
                return Task.CompletedTask;
            });
            StatusMessage = "已切换到默认数据目录。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"切换失败：{ex.Message}";
        }
    }

    public async Task ApplyCustomDataRootAsync(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            StatusMessage = "路径无效。";
            return;
        }

        try
        {
            await _onDataRootChanged(async () =>
            {
                var previous = _paths.DataRoot;
                var target = Path.GetFullPath(directory);
                if (CopyOnMigrate && !PathsEqual(previous, target) && Directory.Exists(previous))
                {
                    await Task.Run(() => CopyDirectory(previous, target));
                }

                _paths.SetDataRoot(target);
                DataRootDisplay = _paths.DataRoot;
                _config.DataRoot = _paths.DataRoot;
                Persist();
            });
            StatusMessage = "数据目录已更新。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"切换失败：{ex.Message}";
        }
    }

    private void ApplyAndSave()
    {
        _config.Theme = Theme;
        _config.AlwaysOnTop = AlwaysOnTop;
        _config.HoverPreview = HoverPreview;
        _config.PreviewOpacity = PreviewOpacity;
        _config.UseGpuRendering = UseGpuRendering;
        _config.Hotkey = Hotkey;
        Persist();
        _windowChrome.SetTopmost(AlwaysOnTop);
        _onConfigApplied(_config.Clone());
    }

    private void Persist()
    {
        _configStore.Save(_config);
        _onConfigApplied(_config.Clone());
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(
            Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, dir);
            Directory.CreateDirectory(Path.Combine(destination, rel));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, file);
            var destFile = Path.Combine(destination, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
            File.Copy(file, destFile, overwrite: true);
        }
    }
}
