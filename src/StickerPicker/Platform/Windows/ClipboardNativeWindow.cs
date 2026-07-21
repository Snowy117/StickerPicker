using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace StickerPicker.Platform.Windows;

[SupportedOSPlatform("windows")]
internal static class ClipboardNativeWindow
{
    private const uint WmClipboardUpdate = 0x031D;
    private static readonly Lock s_gate = new();
    private static IntPtr s_window;
    private static WindowProcedure? s_windowProcedure;
    private static Action? s_onClipboardUpdated;
    private static bool s_listening;

    public static bool IsListening
    {
        get
        {
            lock (s_gate)
            {
                return s_listening;
            }
        }
    }

    public static IntPtr Ensure(Action onClipboardUpdated)
    {
        lock (s_gate)
        {
            s_onClipboardUpdated = onClipboardUpdated;
            if (s_window == IntPtr.Zero)
            {
                s_window = CreateHiddenWindow();
            }

            if (s_window != IntPtr.Zero && !s_listening)
            {
                s_listening = NativeMethods.AddClipboardFormatListener(s_window);
            }

            return s_window;
        }
    }

    public static void Detach(Action onClipboardUpdated)
    {
        lock (s_gate)
        {
            if (s_onClipboardUpdated != onClipboardUpdated)
            {
                return;
            }

            s_onClipboardUpdated = null;
            if (s_listening)
            {
                _ = NativeMethods.RemoveClipboardFormatListener(s_window);
                s_listening = false;
            }
        }
    }

    private static IntPtr CreateHiddenWindow()
    {
        s_windowProcedure = WindowProcedureImpl;
        const string ClassName = "StickerPickerClipboardOwnerWindow";
        var windowClass = new WindowClass
        {
            Size = (uint)Marshal.SizeOf<WindowClass>(),
            WindowProcedure = Marshal.GetFunctionPointerForDelegate(s_windowProcedure),
            Instance = NativeMethods.GetModuleHandle(moduleName: null),
            ClassName = ClassName,
        };

        var atom = NativeMethods.RegisterClass(windowClass: ref windowClass);
        if (atom == 0 && Marshal.GetLastWin32Error() != 1410)
        {
            return IntPtr.Zero;
        }

        return NativeMethods.CreateWindow(
            0,
            ClassName,
            "StickerPicker Clipboard Owner",
            0,
            0,
            0,
            0,
            0,
            IntPtr.Zero,
            IntPtr.Zero,
            windowClass.Instance,
            IntPtr.Zero);
    }

    private static IntPtr WindowProcedureImpl(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == WmClipboardUpdate)
        {
            s_onClipboardUpdated?.Invoke();
            return IntPtr.Zero;
        }

        return NativeMethods.DefWindowProc(window, message, wParam, lParam);
    }

    private delegate IntPtr WindowProcedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WindowClass
    {
        public uint Size;
        public uint Style;
        public IntPtr WindowProcedure;
        public int ClassExtraBytes;
        public int WindowExtraBytes;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        public string? MenuName;
        public string ClassName;
        public IntPtr SmallIcon;
    }
}
