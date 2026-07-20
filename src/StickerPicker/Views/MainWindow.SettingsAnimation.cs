using System.ComponentModel;
using Avalonia.Media;
using Avalonia.Threading;
using StickerPicker.ViewModels;

namespace StickerPicker.Views;

public partial class MainWindow
{
    private const int OverlayAnimationMs = 160;
    private const double OverlayClosedScale = 0.96;
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
        Dispatcher.UIThread.Post(() =>
        {
            OverlayCard.Opacity = 1;
            if (OverlayCard.RenderTransform is ScaleTransform scale)
            {
                scale.ScaleX = 1;
                scale.ScaleY = 1;
            }
        });
    }

    private void CloseOverlay()
    {
        OverlayCard.Opacity = 0;
        if (OverlayCard.RenderTransform is ScaleTransform scale)
        {
            scale.ScaleX = OverlayClosedScale;
            scale.ScaleY = OverlayClosedScale;
        }

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
