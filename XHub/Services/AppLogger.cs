using System.IO;
using System.Text;

namespace XHub.Services;

public static class AppLogger
{
    private static readonly object Sync = new();

    public static string LogDirectoryPath => Path.Combine(App.AppDataDirectoryPath, "logs");
    public static string CurrentLogFilePath => Path.Combine(LogDirectoryPath, $"app-{DateTime.Now:yyyy-MM-dd}.log");

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception = null)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(LogDirectoryPath);
                using var stream = new FileStream(CurrentLogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\t{level}\t{message}");
                if (exception is not null)
                {
                    writer.WriteLine(exception.ToString());
                }
            }
        }
        catch
        {
        }
    }
}
