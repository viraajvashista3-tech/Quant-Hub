using Avalonia;
using QuantHub.Desktop.Services;

namespace QuantHub.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        CrashLogger.Install();
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            // Belt-and-braces: catches anything that escapes the Avalonia message loop itself
            // (startup failures before AppDomain's handler would otherwise see it).
            CrashLogger.Log(ex, "Main");
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
