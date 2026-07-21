using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace StickerPicker.Platform.Windows;

[SupportedOSPlatform("windows")]
internal static class ClipboardHandles
{
    private const uint GmemMoveable = 0x0002;

    public static IntPtr CreateBytes(byte[] bytes)
    {
        var handle = NativeMethods.GlobalAlloc(GmemMoveable, (UIntPtr)bytes.Length);
        if (handle == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var pointer = NativeMethods.GlobalLock(handle);
        if (pointer == IntPtr.Zero)
        {
            _ = NativeMethods.GlobalFree(handle);
            return IntPtr.Zero;
        }

        try
        {
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
        }
        finally
        {
            _ = NativeMethods.GlobalUnlock(handle);
        }

        return handle;
    }

    public static IntPtr CreateHdrop(string fullPath)
    {
        var pathBytes = Encoding.Unicode.GetBytes(fullPath + "\0\0");
        var headerSize = Marshal.SizeOf<DropFiles>();
        var handle = NativeMethods.GlobalAlloc(GmemMoveable, (UIntPtr)(headerSize + pathBytes.Length));
        if (handle == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var pointer = NativeMethods.GlobalLock(handle);
        if (pointer == IntPtr.Zero)
        {
            _ = NativeMethods.GlobalFree(handle);
            return IntPtr.Zero;
        }

        try
        {
            var header = new DropFiles { FileOffset = (uint)headerSize, IsWide = true };
            Marshal.StructureToPtr(header, pointer, fDeleteOld: false);
            Marshal.Copy(pathBytes, 0, pointer + headerSize, pathBytes.Length);
        }
        finally
        {
            _ = NativeMethods.GlobalUnlock(handle);
        }

        return handle;
    }

    public static IntPtr TryCreateDib(string fullPath)
    {
        try
        {
            var bytes = File.ReadAllBytes(fullPath);
            if (bytes.Length <= 14 || bytes[0] != (byte)'B' || bytes[1] != (byte)'M')
            {
                return IntPtr.Zero;
            }

            return CreateBytes(bytes[14..]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OutOfMemoryException)
        {
            return IntPtr.Zero;
        }
    }

    public static void Free(params IntPtr[] handles)
    {
        foreach (var handle in handles)
        {
            if (handle != IntPtr.Zero)
            {
                _ = NativeMethods.GlobalFree(handle);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DropFiles
    {
        public uint FileOffset;
        public Point DropPoint;
        [MarshalAs(UnmanagedType.Bool)] public bool IsNonClient;
        [MarshalAs(UnmanagedType.Bool)] public bool IsWide;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }
}
