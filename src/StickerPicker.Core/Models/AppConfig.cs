namespace StickerPicker.Core.Models;

public sealed class AppConfig
{
    public int Version { get; set; } = 1;

    /// <summary>system | light | dark</summary>
    public string Theme { get; set; } = "system";

    public bool AlwaysOnTop { get; set; } = true;

    public string Hotkey { get; set; } = "Ctrl+Shift+E";

    /// <summary>Null means use the resolved default data root (not stored as absolute default).</summary>
    public string? DataRoot { get; set; }

    public double ThumbnailSize { get; set; } = 96;

    public bool HoverPreview { get; set; } = true;

    public double PreviewOpacity { get; set; } = 0.92;

    public bool UseGpuRendering { get; set; }

    public bool AutoPaste { get; set; }

    public int ClipboardRestoreDelaySeconds { get; set; }

    public bool KeepWindowOpenAfterSelection { get; set; }

    public WindowGeometry Window { get; set; } = new();

    public AppConfig Clone() => new()
    {
        Version = Version,
        Theme = Theme,
        AlwaysOnTop = AlwaysOnTop,
        Hotkey = Hotkey,
        DataRoot = DataRoot,
        ThumbnailSize = ThumbnailSize,
        HoverPreview = HoverPreview,
        PreviewOpacity = PreviewOpacity,
        UseGpuRendering = UseGpuRendering,
        AutoPaste = AutoPaste,
        ClipboardRestoreDelaySeconds = ClipboardRestoreDelaySeconds,
        KeepWindowOpenAfterSelection = KeepWindowOpenAfterSelection,
        Window = Window.Clone(),
    };
}

public sealed class WindowGeometry
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 900;
    public double Height { get; set; } = 640;

    public WindowGeometry Clone() => new()
    {
        X = X,
        Y = Y,
        Width = Width,
        Height = Height,
    };
}
