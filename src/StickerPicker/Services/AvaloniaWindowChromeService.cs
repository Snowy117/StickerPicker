using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using StickerPicker.Core.Abstractions;

namespace StickerPicker.Services;

public sealed class AvaloniaWindowChromeService : IWindowChromeService
{
    private Window? _window;

    public void Attach(Window window) => _window = window;

    public bool IsVisible => _window?.IsVisible == true;

    public void Show()
    {
        RunOnUi(() =>
        {
            if (_window is null)
            {
                return;
            }

            _window.Show();
            _window.Activate();
            if (_window.WindowState == WindowState.Minimized)
            {
                _window.WindowState = WindowState.Normal;
            }
        });
    }

    public void Hide()
    {
        RunOnUi(() => _window?.Hide());
    }

    public void Activate()
    {
        RunOnUi(() =>
        {
            if (_window is null)
            {
                return;
            }

            var topmost = _window.Topmost;
            _window.Activate();
            if (!topmost)
            {
                return;
            }

            _window.Topmost = false;
            _window.Topmost = true;
        });
    }

    public void ToggleVisible()
    {
        RunOnUi(() =>
        {
            if (_window is null)
            {
                return;
            }

            // Surface unless the window is both visible and active. The visible-but-
            // unfocused case (e.g. AlwaysOnTop shadowed by another app) must surface too.
            if (!_window.IsVisible || !_window.IsActive)
            {
                Show();
                Activate();
            }
            else
            {
                _window.Hide();
            }
        });
    }

    public void SetTopmost(bool topmost)
    {
        RunOnUi(() =>
        {
            if (_window is { } window)
            {
                window.Topmost = topmost;
            }
        });
    }

    public void Shutdown()
    {
        RunOnUi(() =>
        {
            if (_window is Views.MainWindow main)
            {
                main.ForceClose();
            }

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        });
    }

    private static void RunOnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }
}
