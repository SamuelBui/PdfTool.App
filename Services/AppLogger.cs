using System.IO;
using System.Text;

namespace PdfTool.App.Services;

public class AppLogger : IAppLogger
{
    private static readonly object SyncRoot = new();
    private readonly string _logDirectory;

    public AppLogger()
    {
        _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PdfTool.App",
            "Logs");
        Directory.CreateDirectory(_logDirectory);
    }

    public string CurrentLogFilePath => Path.Combine(_logDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");

    public void LogInfo(string message)
        => Write("INFO", message, null);

    public void LogWarning(string message)
        => Write("WARN", message, null);

    public void LogError(string message, Exception? exception = null)
        => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        var builder = new StringBuilder();
        builder.Append('[')
            .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
            .Append("] [")
            .Append(level)
            .Append("] ")
            .AppendLine(message);

        if (exception != null)
        {
            builder.AppendLine(exception.ToString());
        }

        lock (SyncRoot)
        {
            File.AppendAllText(CurrentLogFilePath, builder.ToString(), Encoding.UTF8);
        }
    }
}
