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

        var result = await PromptForNameAsync("新建分类", "分类名称", initial: null);
        if (!string.IsNullOrWhiteSpace(result))
        {
            vm.CreateCategoryCommand.Execute(result);
        }
    }

    private async void OnRenameCategoryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (vm.SelectedCategory is null || vm.SelectedCategory.IsVirtual)
        {
            vm.ErrorMessage = "请先选择真实分类再重命名。";
            return;
        }

        var result = await PromptForNameAsync(
            "重命名分类",
            "新名称",
            initial: vm.SelectedCategory.Name);
        if (!string.IsNullOrWhiteSpace(result))
        {
            vm.RenameCategoryCommand.Execute(result);
        }
    }

    private async void OnDeleteCategoryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (vm.SelectedCategory is null || vm.SelectedCategory.IsVirtual)
        {
            vm.ErrorMessage = "请先选择真实分类再删除。";
            return;
        }

        var name = vm.SelectedCategory.Name;
        var confirmed = await ConfirmAsync(
            "删除分类",
            $"确定删除分类「{name}」？\n非空分类将连同其中的表情文件一并删除。");
        if (!confirmed)
        {
            return;
        }

        vm.DeleteCategoryCommand.Execute(true);
    }

    private async Task<string?> PromptForNameAsync(string title, string label, string? initial)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 320,
            Height = 140,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = BuildNameDialog(label, out var box, out var ok),
        };

        if (!string.IsNullOrEmpty(initial))
        {
            box.Text = initial;
        }

        string? result = null;
        ok.Click += (_, _) =>
        {
            result = box.Text;
            dialog.Close();
        };

        await dialog.ShowDialog(this);
        return result;
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var ok = new Button { Content = "删除", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        var cancel = new Button { Content = "取消", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        var result = false;
        var dialog = new Window
        {
            Title = title,
            Width = 360,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(16),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Children = { cancel, ok },
                    },
                },
            },
        };

        ok.Click += (_, _) =>
        {
            result = true;
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);
        return result;
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
