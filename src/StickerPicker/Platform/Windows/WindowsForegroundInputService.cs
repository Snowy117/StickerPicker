using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using StickerPicker.Core.Abstractions;

namespace StickerPicker.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsForegroundInputService : IForegroundInputService
{
    private const int ModifierReleaseAttempts = 20;
    private static readonly int[] s_modifierKeys = [0x10, 0x11, 0x12, 0x5B, 0x5C];
    private IntPtr _target;

    public void CaptureTarget()
    {
        var candidate = GetForegroundWindow();
        _target = IsExternalWindow(candidate) ? candidate : IntPtr.Zero;
    }

    public void InvalidateTarget() => _target = IntPtr.Zero;

    public async Task<ForegroundActionResult> ConsumeTargetAsync(
        bool restoreFocus,
        bool sendPaste,
        CancellationToken cancellationToken = default)
    {
        var target = _target;
        _target = IntPtr.Zero;
        if (target == IntPtr.Zero)
        {
            return NoTarget();
        }

        if (!restoreFocus)
        {
            return TargetConsumed();
        }

        if (!IsWindow(target) || !SetForegroundWindow(target) || GetForegroundWindow() != target)
        {
            return TargetConsumed("无法返回唤起前的窗口。");
        }

        if (!sendPaste)
        {
            return FocusRestored();
        }

        if (!await WaitForModifiersReleasedAsync(cancellationToken).ConfigureAwait(false))
        {
            return FocusRestored("修饰键未及时释放，已跳过自动粘贴。");
        }

        if (GetForegroundWindow() != target)
        {
            return TargetConsumed("目标窗口已失去焦点，已跳过自动粘贴。");
        }

        var inputs = CreatePasteInputs();
        var inserted = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (inserted == inputs.Length)
        {
            return new ForegroundActionResult(
                HadTarget: true,
                FocusRestored: true,
                PasteSent: true);
        }

        if (inserted > 0 && !ReleaseInsertedKeys(inputs, (int)inserted))
        {
            return FocusRestored("自动粘贴未完整发送，且按键释放补偿失败。请松开修饰键后重试。");
        }

        return FocusRestored("自动粘贴失败，表情仍已复制到剪贴板。");
    }

    private static ForegroundActionResult NoTarget() => new(
        HadTarget: false,
        FocusRestored: false,
        PasteSent: false);

    private static ForegroundActionResult TargetConsumed(string? failureReason = null) => new(
        HadTarget: true,
        FocusRestored: false,
        PasteSent: false,
        FailureReason: failureReason);

    private static ForegroundActionResult FocusRestored(string? failureReason = null) => new(
        HadTarget: true,
        FocusRestored: true,
        PasteSent: false,
        FailureReason: failureReason);

    private static bool IsExternalWindow(IntPtr window)
    {
        if (window == IntPtr.Zero || !IsWindow(window))
        {
            return false;
        }

        _ = GetWindowThreadProcessId(window, out var processId);
        return processId != Environment.ProcessId;
    }

    private static async Task<bool> WaitForModifiersReleasedAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < ModifierReleaseAttempts; attempt++)
        {
            if (s_modifierKeys.All(key => (GetAsyncKeyState(key) & 0x8000) == 0))
            {
                return true;
            }

            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    private static Input[] CreatePasteInputs() =>
    [
        CreateKeyInput(0x11, keyUp: false),
        CreateKeyInput(0x56, keyUp: false),
        CreateKeyInput(0x56, keyUp: true),
        CreateKeyInput(0x11, keyUp: true),
    ];

    private static Input CreateKeyInput(ushort key, bool keyUp) => new()
    {
        Type = 1,
        Data = new InputUnion
        {
            Keyboard = new KeybdInput { VirtualKey = key, Flags = keyUp ? 0x0002u : 0u },
        },
    };

    private static bool ReleaseInsertedKeys(Input[] inputs, int inserted)
    {
        var held = new List<ushort>();
        for (var index = 0; index < inserted; index++)
        {
            var input = inputs[index].Data.Keyboard;
            if ((input.Flags & 0x0002) == 0)
            {
                held.Add(input.VirtualKey);
            }
            else
            {
                held.Remove(input.VirtualKey);
            }
        }

        var cleanup = held.AsEnumerable().Reverse().Select(key => CreateKeyInput(key, keyUp: true)).ToArray();
        return cleanup.Length == 0
            || SendInput((uint)cleanup.Length, cleanup, Marshal.SizeOf<Input>()) == cleanup.Length;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out int processId);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);
}

[StructLayout(LayoutKind.Sequential)]
internal struct Input
{
    public uint Type;
    public InputUnion Data;
}

[StructLayout(LayoutKind.Explicit)]
internal struct InputUnion
{
    [FieldOffset(0)] public KeybdInput Keyboard;
    [FieldOffset(0)] public MouseInput Mouse;
    [FieldOffset(0)] public HardwareInput Hardware;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KeybdInput
{
    public ushort VirtualKey;
    public ushort ScanCode;
    public uint Flags;
    public uint Time;
    public UIntPtr ExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MouseInput
{
    public int X;
    public int Y;
    public uint MouseData;
    public uint Flags;
    public uint Time;
    public UIntPtr ExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct HardwareInput
{
    public uint Message;
    public ushort ParameterLow;
    public ushort ParameterHigh;
}
