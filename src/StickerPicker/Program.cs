using Avalonia;

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

        if (OperatingSystem.IsWindows()
            && !string.Equals(
                Environment.GetEnvironmentVariable("STICKERPICKER_USE_GPU"),
                "1",
                StringComparison.Ordinal))
        {
            builder.With(new Win32PlatformOptions
            {
                // This app renders a small, mostly static surface. Software rendering avoids
                // keeping ANGLE, D3D, and a vendor GPU driver's large process-wide baseline.
                RenderingMode = [Win32RenderingMode.Software],
            });
        }

        return builder;
    }
}
