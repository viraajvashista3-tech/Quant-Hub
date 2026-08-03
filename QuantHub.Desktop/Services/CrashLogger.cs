using System.IO;

namespace QuantHub.Desktop.Services;

/// <summary>Last-resort crash logging for release builds. Nothing else in this app writes exceptions
/// anywhere durable - without this, a crash outside the debugger (e.g. an unhandled exception from a
/// background fire-and-forget task, or one before the ShellWindow even opens) leaves no trace at all
/// for a user to report back. Deliberately dumb (append a timestamp + exception text to a flat file)
/// rather than a full logging framework, since it only ever needs to run in the crash path itself,
/// which must not itself be able to throw.</summary>
internal static class CrashLogger
{
    private static readonly string LogPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QuantHub", "crash.log");

    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log(e.ExceptionObject as Exception, "AppDomain.UnhandledException");

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log(e.Exception, "TaskScheduler.UnobservedTaskException");
            e.SetObserved();
        };
    }

    public static void Log(Exception? ex, string source)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (dir is not null) Directory.CreateDirectory(dir);
            File.AppendAllText(LogPath,
                $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC] {source}{Environment.NewLine}{ex}{Environment.NewLine}{new string('-', 80)}{Environment.NewLine}");
        }
        catch
        {
            // The crash-logging path must never itself throw and mask the original exception.
        }
    }
}
