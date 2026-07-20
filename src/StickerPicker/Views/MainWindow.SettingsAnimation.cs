using System.ComponentModel;
using Avalonia.Threading;
using StickerPicker.ViewModels;

namespace StickerPicker.Views;

public partial class MainWindow
{
    private const int OverlayAnimationMs = 160;
    private DispatcherTimer? _overlayCloseTimer;

    private void RegisterSettingsAnimationHandlers()
    {
        DataContextChanged += OnDataContextChanged;
        AttachViewModel(DataContext);
    }

    private void OnDataContextChanged(object? sender, EventArgs e) => AttachViewModel(DataContext);

    private void AttachViewModel(object? dataContext)
    {
        if (dataContext is MainViewModel vm)
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            if (vm.IsSettingsOpen)
            {
                OpenOverlay();
            }
            else
            {
                OverlayMask.IsVisible = false;
            }
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(MainViewModel.IsSettingsOpen), StringComparison.Ordinal) &&
            sender is MainViewModel vm)
        {
            if (vm.IsSettingsOpen)
            {
                OpenOverlay();
            }
            else
            {
                CloseOverlay();
            }
        }
    }

    private void OpenOverlay()
    {
        _overlayCloseTimer?.Stop();
        _overlayCloseTimer = null;
        OverlayMask.IsVisible = true;
        // Fade the mask, not the card: child controls carry their own BrushTransitions
        // and would animate staggered if the card Opacity changed independently.
        Dispatcher.UIThread.Post(() => OverlayMask.Opacity = 1);
    }

    private void CloseOverlay()
    {
        OverlayMask.Opacity = 0;
        _overlayCloseTimer?.Stop();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(OverlayAnimationMs) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            OverlayMask.IsVisible = false;
        };
        _overlayCloseTimer = timer;
        timer.Start();
    }
}
