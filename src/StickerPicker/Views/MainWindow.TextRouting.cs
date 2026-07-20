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
        if (SearchBox.IsFocused
            || DataContext is not MainViewModel vm
            || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        SearchBox.Focus();
        vm.SearchText += e.Text;
        e.Handled = true;
    }
}
