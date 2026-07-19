using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using StickerPicker.ViewModels;

namespace StickerPicker.Views;

public partial class MainWindow : Window
{
    private bool _forceClose;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.PersistWindowGeometry(Position.X, Position.Y, Width, Height);
        }

        if (_forceClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private async void OnImportFilesClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "导入表情图片",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Images")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp"],
                },
            ],
        });

        if (files.Count == 0)
        {
            return;
        }

        var paths = files
            .Select(f => f.TryGetLocalPath())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .ToList();
        await vm.ImportPathsCommand.ExecuteAsync(paths);
    }

    private async void OnImportFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "导入文件夹（拍平到当前分类）",
            AllowMultiple = false,
        });

        if (folders.Count == 0)
        {
            return;
        }

        var path = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await vm.ImportPathsCommand.ExecuteAsync([path]);
    }

    private async void OnCreateCategoryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        var dialog = new Window
        {
            Title = "新建分类",
            Width = 320,
            Height = 140,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = BuildNameDialog("分类名称", out var box, out var ok),
        };

        string? result = null;
        ok.Click += (_, _) =>
        {
            result = box.Text;
            dialog.Close();
        };

        await dialog.ShowDialog(this);
        if (!string.IsNullOrWhiteSpace(result))
        {
            vm.CreateCategoryCommand.Execute(result);
        }
    }

    private async void OnPickDataRootClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择数据目录",
            AllowMultiple = false,
        });

        if (folders.Count == 0)
        {
            return;
        }

        var path = folders[0].TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            vm.Settings.ApplyCustomDataRoot(path);
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var delta = e.Delta.Y > 0 ? 8 : -8;
            vm.AdjustThumbnailCommand.Execute(delta);
            e.Handled = true;
        }
    }

    private static StackPanel BuildNameDialog(string label, out TextBox box, out Button ok)
    {
        box = new TextBox { PlaceholderText = label };
        ok = new Button { Content = "确定", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        return new StackPanel
        {
            Margin = new Avalonia.Thickness(16),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = label },
                box,
                ok,
            },
        };
    }
}
