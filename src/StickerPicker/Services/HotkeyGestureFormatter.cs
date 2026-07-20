using Avalonia.Input;

namespace StickerPicker.Services;

/// <summary>Formats Avalonia key events into gestures understood by WindowsHotkeyService.</summary>
public static class HotkeyGestureFormatter
{
    public static bool IsModifierOnly(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin
            or Key.Clear or Key.None;

    public static bool TryFormat(Key key, KeyModifiers modifiers, out string gesture)
    {
        gesture = "";
        if (IsModifierOnly(key))
        {
            return false;
        }

        if (!TryMapKey(key, out var keyToken))
        {
            return false;
        }

        var parts = new List<string>(4);
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            parts.Add("Win");
        }

        if (parts.Count == 0)
        {
            return false;
        }

        parts.Add(keyToken);
        gesture = string.Join('+', parts);
        return true;
    }

    private static bool TryMapKey(Key key, out string token)
    {
        switch (key)
        {
            case >= Key.A and <= Key.Z:
                token = ((char)('A' + (key - Key.A))).ToString();
                return true;
            case >= Key.D0 and <= Key.D9:
                token = ((char)('0' + (key - Key.D0))).ToString();
                return true;
            case >= Key.NumPad0 and <= Key.NumPad9:
                token = ((char)('0' + (key - Key.NumPad0))).ToString();
                return true;
            case >= Key.F1 and <= Key.F24:
                var functionIndex = key - Key.F1 + 1;
                token = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"F{functionIndex}");
                return true;
            default:
                token = "";
                return false;
        }
    }
}
