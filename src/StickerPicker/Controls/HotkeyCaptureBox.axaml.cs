using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using StickerPicker.Services;

namespace StickerPicker.Controls;

public partial class HotkeyCaptureBox : UserControl
{
    public static readonly StyledProperty<string?> GestureProperty =
        AvaloniaProperty.Register<HotkeyCaptureBox, string?>(
            nameof(Gesture),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly RoutedEvent<RoutedEventArgs> GestureCapturedEvent =
        RoutedEvent.Register<HotkeyCaptureBox, RoutedEventArgs>(
            nameof(GestureCaptured),
            RoutingStrategies.Bubble);

    public HotkeyCaptureBox()
    {
        InitializeComponent();
        Focusable = true;
        UpdateDisplay();
    }

    public string? Gesture
    {
        get => GetValue(GestureProperty);
        set => SetValue(GestureProperty, value);
    }

    public event EventHandler<RoutedEventArgs>? GestureCaptured
    {
        add => AddHandler(GestureCapturedEvent, value);
        remove => RemoveHandler(GestureCapturedEvent, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GestureProperty)
        {
            UpdateDisplay();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (HotkeyGestureFormatter.IsModifierOnly(e.Key))
        {
            e.Handled = true;
            return;
        }

        if (!HotkeyGestureFormatter.TryFormat(e.Key, e.KeyModifiers, out var gesture))
        {
            e.Handled = true;
            return;
        }

        Gesture = gesture;
        RaiseEvent(new RoutedEventArgs(GestureCapturedEvent));
        e.Handled = true;
    }

    private void OnBorderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Focus();
        e.Handled = true;
    }

    private void UpdateDisplay()
    {
        if (Display is null)
        {
            return;
        }

        Display.Text = string.IsNullOrWhiteSpace(Gesture)
            ? "点击后按下快捷键"
            : Gesture;
    }
}
