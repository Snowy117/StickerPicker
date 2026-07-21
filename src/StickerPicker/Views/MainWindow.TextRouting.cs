using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

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
        if (string.IsNullOrEmpty(e.Text) || IsGlobalTextRoutingSuppressed(e))
        {
            return;
        }

        // Any editable control already holding focus should receive the text
        // itself; do not hijack typing aimed at the tag editor, hotkey box, etc.
        if (FocusManager.GetFocusedElement() is TextBox)
        {
            return;
        }

        SearchBox.Text = string.Concat(SearchBox.Text, e.Text);
        SearchBox.Focus();
        SearchBox.CaretIndex = SearchBox.Text.Length;
        SearchBox.SelectionStart = SearchBox.CaretIndex;
        SearchBox.SelectionEnd = SearchBox.CaretIndex;
        e.Handled = true;
    }

    private bool IsGlobalTextRoutingSuppressed(TextInputEventArgs e)
    {
        if (OverlayMask.IsVisible
            || TagEditorMask.IsVisible
            || OwnedWindows.Any(window => window.IsVisible))
        {
            return true;
        }

        return e.Source is Visual source
            && GetTopLevel(source) is { } sourceTopLevel
            && sourceTopLevel != this;
    }
}
