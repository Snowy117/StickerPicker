using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using StickerPicker.ViewModels;

namespace StickerPicker.Views;

public partial class MainWindow
{
    // Tunnel: see text before the focused control. TextInput already carries
    // the final IME-composed string, so CJK works without a KeyDown hook.
    private void RegisterGlobalTextRouting()
    {
        AddHandler(
            TextInputEvent,
            OnGlobalTextInput,
            RoutingStrategies.Tunnel);
    }

    private void OnGlobalTextInput(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        // Any editable control already holding focus should receive the text
        // itself; do not hijack typing aimed at the tag editor, hotkey box, etc.
        if (FocusManager?.GetFocusedElement() is TextBox)
        {
            return;
        }

        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        SearchBox.Focus();
        SearchBox.CaretIndex = SearchBox.Text?.Length ?? 0;
        vm.SearchText += e.Text;
        e.Handled = true;
    }
}
