using Avalonia.Media.Imaging;
using SkiaSharp;

namespace StickerPicker.Services;

/// <summary>
/// Decodes images away from the UI thread while bounding both concurrency and
/// the longest decoded edge. Bounding the longest edge also bounds total pixel
/// memory for unusually tall or wide source images.
/// </summary>
internal static class BoundedImageDecoder
{
    private const int MinimumSide = 32;
    private const int MaximumSide = 480;
    // Some codecs temporarily decode at the source dimensions before scaling.
    // Serializing this work prevents multiple large native pixel buffers from
    // establishing a needlessly high process memory watermark.
    private static readonly SemaphoreSlim s_decodeGate = new(initialCount: 1, maxCount: 1);

    public static async Task<Bitmap?> DecodeAsync(
        string path,
        int maximumSide,
        CancellationToken cancellationToken)
    {
        await s_decodeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                    () => Decode(path, maximumSide, cancellationToken),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException)
        {
            return null;
        }
        finally
        {
            s_decodeGate.Release();
        }
    }

    private static Bitmap? Decode(string path, int maximumSide, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(path))
        {
            return null;
        }

        var boundedSide = Math.Clamp(maximumSide, MinimumSide, MaximumSide);
        using var metadataStream = File.OpenRead(path);
        using var codec = SKCodec.Create(metadataStream);
        if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var decodeStream = File.OpenRead(path);
        if (codec.Info.Width <= boundedSide && codec.Info.Height <= boundedSide)
        {
            return new Bitmap(decodeStream);
        }

        return codec.Info.Width >= codec.Info.Height
            ? Bitmap.DecodeToWidth(decodeStream, boundedSide)
            : Bitmap.DecodeToHeight(decodeStream, boundedSide);
    }
}
