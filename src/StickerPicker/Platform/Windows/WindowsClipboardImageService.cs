using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using StickerPicker.Core.Abstractions;

namespace StickerPicker.Platform.Windows;

/// <summary>
/// CF_HDROP file list + CF_DIB bitmap fallback for chat clients (QQ/WeChat).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsClipboardImageService : IClipboardImageService
{
    private const uint CfHdrop = 15;
    private const uint CfDib = 8;
    private const uint GmemMoveable = 0x0002;

    public bool CopyImageFile(string absolutePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(absolutePath);
        if (!OpenClipboard(IntPtr.Zero))
        {
            return false;
        }

        try
        {
            EmptyClipboard();

            var hdrop = CreateHdrop(fullPath);
            if (hdrop != IntPtr.Zero)
            {
                if (SetClipboardData(CfHdrop, hdrop) == IntPtr.Zero)
                {
                    GlobalFree(hdrop);
                }
            }

            var dib = TryCreateDib(fullPath);
            if (dib != IntPtr.Zero)
            {
                if (SetClipboardData(CfDib, dib) == IntPtr.Zero)
                {
                    GlobalFree(dib);
                }
            }

            return hdrop != IntPtr.Zero || dib != IntPtr.Zero;
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static IntPtr CreateHdrop(string fullPath)
    {
        var pathBytes = System.Text.Encoding.Unicode.GetBytes(fullPath + "\0\0");
        var dropFilesSize = Marshal.SizeOf<DropFiles>();
        var totalSize = dropFilesSize + pathBytes.Length;
        var handle = GlobalAlloc(GmemMoveable, (UIntPtr)totalSize);
        if (handle == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var ptr = GlobalLock(handle);
        if (ptr == IntPtr.Zero)
        {
            GlobalFree(handle);
            return IntPtr.Zero;
        }

        try
        {
            var drop = new DropFiles
            {
                pFiles = (uint)dropFilesSize,
                pt = default,
                fNC = false,
                fWide = true,
            };
            Marshal.StructureToPtr(drop, ptr, false);
            Marshal.Copy(pathBytes, 0, ptr + dropFilesSize, pathBytes.Length);
        }
        finally
        {
            GlobalUnlock(handle);
        }

        return handle;
    }

    private static IntPtr TryCreateDib(string fullPath)
    {
        try
        {
            using var stream = File.OpenRead(fullPath);
            using var sk = new MemoryStream();
            stream.CopyTo(sk);
            var bytes = sk.ToArray();

            if (bytes.Length > 14
                && bytes[0] == (byte)'B'
                && bytes[1] == (byte)'M')
            {
                var dibSize = bytes.Length - 14;
                var handle = GlobalAlloc(GmemMoveable, (UIntPtr)dibSize);
                if (handle == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }

                var ptr = GlobalLock(handle);
                if (ptr == IntPtr.Zero)
                {
                    GlobalFree(handle);
                    return IntPtr.Zero;
                }

                try
                {
                    Marshal.Copy(bytes, 14, ptr, dibSize);
                }
                finally
                {
                    GlobalUnlock(handle);
                }

                return handle;
            }
        }
        catch
        {
            return IntPtr.Zero;
        }

        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DropFiles
    {
        public uint pFiles;
        public Point pt;
        [MarshalAs(UnmanagedType.Bool)] public bool fNC;
        [MarshalAs(UnmanagedType.Bool)] public bool fWide;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);
}
