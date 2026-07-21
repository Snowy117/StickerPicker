using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using StickerPicker.ViewModels;

namespace StickerPicker.Views;

public partial class MainWindow
{
    private const int OverlayAnimationMs = 160;
    private DispatcherTimer? _settingsCloseTimer;
    private DispatcherTimer? _tagEditorCloseTimer;

    private void RegisterSettingsAnimationHandlers()
    {
        DataContextChanged += OnDataContextChanged;
        AttachViewModel(DataContext);
    }

    private void OnDataContextChanged(object? sender, EventArgs e) => AttachViewModel(DataContext);

    private void AttachViewModel(object? dataContext)
    {
        if (dataContext is not MainViewModel vm)
        {
            return;
        }

        vm.PropertyChanged -= OnViewModelPropertyChanged;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        SyncOverlayInitial(OverlayMask, vm.IsSettingsOpen);
        SyncOverlayInitial(TagEditorMask, vm.IsTagEditorOpen);
    }

    private static void SyncOverlayInitial(Border mask, bool isOpen)
    {
        mask.Opacity = isOpen ? 1 : 0;
        mask.IsVisible = isOpen;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainViewModel vm)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(MainViewModel.IsSettingsOpen):
                if (vm.IsSettingsOpen)
                {
                    OpenOverlay(OverlayMask, ref _settingsCloseTimer);
                }
                else
                {
                    CloseOverlay(OverlayMask, ref _settingsCloseTimer);
                }
                break;
            case nameof(MainViewModel.IsTagEditorOpen):
                if (vm.IsTagEditorOpen)
                {
                    OpenOverlay(TagEditorMask, ref _tagEditorCloseTimer);
                }
                else
                {
                    CloseOverlay(TagEditorMask, ref _tagEditorCloseTimer);
                }
                break;
        }
    }

    // Fade the mask, not the card: child controls carry their own BrushTransitions
    // and would animate staggered if the card Opacity changed independently.
    private static void OpenOverlay(Border mask, ref DispatcherTimer? closeTimer)
    {
        closeTimer?.Stop();
        closeTimer = null;
        mask.Opacity = 0;
        mask.IsVisible = true;
        Dispatcher.UIThread.Post(() => mask.Opacity = 1);
    }

    private static void CloseOverlay(Border mask, ref DispatcherTimer? closeTimer)
    {
        mask.Opacity = 0;
        closeTimer?.Stop();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(OverlayAnimationMs) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            mask.IsVisible = false;
        };
        closeTimer = timer;
        timer.Start();
    }
}
