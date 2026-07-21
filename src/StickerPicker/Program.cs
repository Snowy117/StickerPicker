using Avalonia;
using StickerPicker.Core.Config;
using StickerPicker.Core.Models;
using StickerPicker.Core.Paths;

namespace StickerPicker;

internal static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    private static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect();
        builder
            .WithInterFont()
            .LogToTrace();

        return ConfigureWindowsRendering(builder);
    }

    // Software rendering avoids keeping ANGLE, D3D, and a vendor GPU driver's
    // large process-wide baseline for this small, mostly-static surface.
    private static AppBuilder ConfigureWindowsRendering(AppBuilder builder)
    {
        if (!OperatingSystem.IsWindows())
        {
            return builder;
        }

        var useGpu = LoadStartupConfig().UseGpuRendering;
        return useGpu
            ? builder
            : builder.With(new Win32PlatformOptions
            {
                RenderingMode = [Win32RenderingMode.Software],
            });
    }

    // Safe to call pre-Avalonia: AppPaths.Resolve only mkdirs, ConfigStore.Load is read-only.
    private static AppConfig LoadStartupConfig()
    {
        try
        {
            var paths = new AppPaths();
            var store = new ConfigStore(paths);
            return store.Load();
        }
        catch
        {
            return new AppConfig();
        }
    }
}
