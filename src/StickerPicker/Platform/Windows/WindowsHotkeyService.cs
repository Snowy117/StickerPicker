using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Threading;
using StickerPicker.Core.Abstractions;

namespace StickerPicker.Platform.Windows;

/// <summary>
/// Win32 RegisterHotKey on a message-only window so hide/show of the main UI does not drop the hotkey.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsHotkeyService : IHotkeyService
{
    private const int HotkeyId = 0xE001;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNorepeat = 0x4000;

    private bool _registered;
    private IntPtr _hwnd;
    private string? _activeGesture;

    public event EventHandler? HotkeyPressed;

    public bool Register(string hotkeyGesture)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        if (!TryParseGesture(hotkeyGesture, out var modifiers, out var key))
        {
            return false;
        }

        _hwnd = NativeMessageWindow.Ensure(OnNativeHotkey);
        if (_hwnd == IntPtr.Zero)
        {
            return false;
        }

        var previousGesture = _activeGesture;
        Unregister();
        _registered = RegisterHotKey(_hwnd, HotkeyId, modifiers | ModNorepeat, key);
        if (_registered)
        {
            _activeGesture = hotkeyGesture.Trim();
            return true;
        }

        TryRestorePreviousGesture(previousGesture);
        return false;
    }

    public void Unregister()
    {
        if (!_registered || _hwnd == IntPtr.Zero)
        {
            return;
        }

        UnregisterHotKey(_hwnd, HotkeyId);
        _registered = false;
        _activeGesture = null;
    }

    public void Dispose() => Unregister();

    private void TryRestorePreviousGesture(string? previousGesture)
    {
        if (string.IsNullOrWhiteSpace(previousGesture)
            || !TryParseGesture(previousGesture, out var prevMods, out var prevKey))
        {
            return;
        }

        if (!RegisterHotKey(_hwnd, HotkeyId, prevMods | ModNorepeat, prevKey))
        {
            return;
        }

        _registered = true;
        _activeGesture = previousGesture;
    }

    private void OnNativeHotkey()
    {
        Dispatcher.UIThread.Post(() => HotkeyPressed?.Invoke(this, EventArgs.Empty));
    }

    private static bool TryParseGesture(string gesture, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(gesture))
        {
            return false;
        }

        var parts = gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        return TrySplitModifiersAndKey(parts, out modifiers, out var keyPart)
            && TryMapKeyToken(keyPart, out virtualKey);
    }

    private static bool TrySplitModifiersAndKey(string[] parts, out uint modifiers, out string keyPart)
    {
        modifiers = 0;
        keyPart = "";
        string? foundKey = null;
        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= ModControl;
                    break;
                case "shift":
                    modifiers |= ModShift;
                    break;
                case "alt":
                    modifiers |= ModAlt;
                    break;
                case "win":
                case "meta":
                case "cmd":
                    modifiers |= ModWin;
                    break;
                default:
                    foundKey = part;
                    break;
            }
        }

        if (foundKey is null)
        {
            return false;
        }

        keyPart = foundKey;
        return true;
    }

    private static bool TryMapKeyToken(string keyPart, out uint virtualKey)
    {
        virtualKey = 0;
        if (keyPart.Length == 1)
        {
            var ch = char.ToUpperInvariant(keyPart[0]);
            if (ch is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                virtualKey = ch;
                return true;
            }
        }

        if (!keyPart.StartsWith("F", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(
                keyPart[1..],
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var fn)
            || fn is < 1 or > 24)
        {
            return false;
        }

        virtualKey = 0x70 + (uint)(fn - 1);
        return true;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

/// <summary>
/// Hidden message-only window that receives WM_HOTKEY independently of main window visibility.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class NativeMessageWindow
{
    private const int WmHotkey = 0x0312;
    private static IntPtr s_hwnd;
    private static WndProc? s_wndProc;
    private static Action? s_onHotkey;
    private static readonly Lock s_gate = new();

    public static IntPtr Ensure(Action onHotkey)
    {
        lock (s_gate)
        {
            s_onHotkey = onHotkey;
            if (s_hwnd != IntPtr.Zero)
            {
                return s_hwnd;
            }

            s_wndProc = WndProcImpl;
            const string ClassName = "StickerPickerHotkeyHiddenWindow";
            var wc = new WndClassEx
            {
                cbSize = (uint)Marshal.SizeOf<WndClassEx>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_wndProc),
                hInstance = GetModuleHandle(lpModuleName: null),
                lpszClassName = ClassName,
            };

            var atom = RegisterClassEx(ref wc);
            if (atom == 0)
            {
                var err = Marshal.GetLastWin32Error();
                if (err != 1410)
                {
                    return IntPtr.Zero;
                }
            }

            s_hwnd = CreateWindowEx(
                0,
                ClassName,
                "StickerPickerHotkey",
                0,
                0, 0, 0, 0,
                new IntPtr(-3),
                IntPtr.Zero,
                wc.hInstance,
                IntPtr.Zero);

            return s_hwnd;
        }
    }

    private static IntPtr WndProcImpl(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg != WmHotkey)
        {
            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        s_onHotkey?.Invoke();
        return IntPtr.Zero;
    }

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClassEx
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WndClassEx lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle,
        string lpClassName,
        string lpWindowName,
        int dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
