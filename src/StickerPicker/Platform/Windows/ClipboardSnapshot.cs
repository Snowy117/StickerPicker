using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace StickerPicker.Platform.Windows;

[SupportedOSPlatform("windows")]
internal sealed class ClipboardSnapshot
{
    private const long PerFormatLimit = 64L * 1024 * 1024;
    private const long TotalLimit = 128L * 1024 * 1024;
    private readonly IReadOnlyList<ClipboardFormatData> _formats;

    private ClipboardSnapshot(IReadOnlyList<ClipboardFormatData> formats) => _formats = formats;

    public static (ClipboardSnapshot? Snapshot, string? Reason) TryCapture()
    {
        var formats = new List<ClipboardFormatData>();
        long total = 0;
        uint previous = 0;
        while (true)
        {
            Marshal.SetLastPInvokeError(0);
            var format = NativeMethods.EnumClipboardFormats(previous);
            if (format == 0)
            {
                return Marshal.GetLastPInvokeError() == 0
                    ? (new ClipboardSnapshot(formats), null)
                    : (null, "无法完整枚举剪贴板格式，本次已跳过恢复。");
            }

            if (!IsMemoryBacked(format))
            {
                return (null, $"剪贴板格式 {format} 无法可靠快照，本次已跳过恢复。");
            }

            var (data, reason) = TryCaptureFormat(format, total);
            if (data is null)
            {
                return (null, reason);
            }

            formats.Add(new ClipboardFormatData(format, data));
            total += data.LongLength;
            previous = format;
        }
    }

    private static (byte[]? Data, string? Reason) TryCaptureFormat(uint format, long currentTotal)
    {
        var handle = NativeMethods.GetClipboardData(format);
        if (handle == IntPtr.Zero)
        {
            return (null, $"剪贴板格式 {format} 为延迟或不可读取数据，本次已跳过恢复。");
        }

        var sizeValue = NativeMethods.GlobalSize(handle).ToUInt64();
        if (sizeValue == 0 || sizeValue > PerFormatLimit)
        {
            var reason = sizeValue > PerFormatLimit
                ? "单个剪贴板格式超过 64 MiB，本次已跳过恢复。"
                : $"剪贴板格式 {format} 不是可复制的全局内存，本次已跳过恢复。";
            return (null, reason);
        }

        var size = checked((long)sizeValue);
        if (currentTotal > TotalLimit - size)
        {
            return (null, "剪贴板快照总量超过 128 MiB，本次已跳过恢复。");
        }

        var pointer = NativeMethods.GlobalLock(handle);
        if (pointer == IntPtr.Zero)
        {
            return (null, $"无法锁定剪贴板格式 {format}，本次已跳过恢复。");
        }

        try
        {
            var data = new byte[checked((int)size)];
            Marshal.Copy(pointer, data, 0, data.Length);
            return (data, null);
        }
        catch (Exception ex) when (ex is OutOfMemoryException or OverflowException)
        {
            return (null, "剪贴板快照内存不足，本次已跳过恢复。");
        }
        finally
        {
            _ = NativeMethods.GlobalUnlock(handle);
        }
    }

    public List<PreparedClipboardFormat>? PrepareHandles()
    {
        var prepared = new List<PreparedClipboardFormat>(_formats.Count);
        foreach (var format in _formats)
        {
            var handle = ClipboardHandles.CreateBytes(format.Data);
            if (handle == IntPtr.Zero)
            {
                ClipboardHandles.Free([.. prepared.Select(item => item.Handle)]);
                return null;
            }

            prepared.Add(new PreparedClipboardFormat(format.Format, handle));
        }

        return prepared;
    }

    private static bool IsMemoryBacked(uint format) => format switch
    {
        1 or 4 or 5 or 6 or 7 or 8 or 10 or 11 or 12 or 13 or 15 or 16 or 17 => true,
        >= 0xC000 and <= 0xFFFF => true,
        _ => false,
    };

    private sealed record ClipboardFormatData(uint Format, byte[] Data);
}

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct PreparedClipboardFormat(uint Format, IntPtr Handle);
