using System.Text;
using Microsoft.Extensions.Logging;
using StoreCrate.Core.Models;
using StoreCrate.Core.Services;

namespace StoreCrate.Core.Logging;

/// <summary>
/// Lightweight always-on file logger writing to %LOCALAPPDATA%\StoreCrate\logs.
/// One file per day, seven days retained. Every write is flushed so a crash or a
/// user report can be diagnosed from the log alone.
/// </summary>
public static class AppLog
{
    private static readonly object Gate = new();
    private static string? _directory;

    /// <summary>The active log directory, or null before initialization.</summary>
    public static string? Directory => _directory;

    /// <summary>Path of today's log file, or null before initialization.</summary>
    public static string? LogFilePath =>
        _directory is null
            ? null
            : Path.Combine(_directory, $"storecrate-{DateTime.Now:yyyyMMdd}.log");

    public static void Initialize()
    {
        lock (Gate)
        {
            if (_directory is not null)
            {
                return;
            }

            try
            {
                _directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "StoreCrate",
                    "logs");
                System.IO.Directory.CreateDirectory(_directory);

                // Keep the last seven days of logs.
                var cutoff = DateTime.Now.AddDays(-7);
                foreach (var file in System.IO.Directory.EnumerateFiles(_directory, "storecrate-*.log"))
                {
                    try
                    {
                        if (File.GetLastWriteTime(file) < cutoff)
                        {
                            File.Delete(file);
                        }
                    }
                    catch
                    {
                        // A locked or undeletable old log is not worth failing over.
                    }
                }

                Write("INFO", $"Log started (StoreCrate {AppInfo.VersionString}, {Environment.OSVersion.VersionString}, {Environment.ProcessPath ?? "unknown path"})", null);
            }
            catch
            {
                // Logging must never break the app; fall back to no logging.
                _directory = null;
            }
        }
    }

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warning(string message, Exception? exception = null) =>
        Write("WARN", message, exception);

    public static void Error(string message, Exception? exception = null) =>
        Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        if (_directory is null)
        {
            return;
        }

        var builder = new StringBuilder(
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}");
        if (exception is not null)
        {
            builder
                .AppendLine()
                .Append("    ")
                .Append(exception.GetType().Name)
                .Append(": ")
                .Append(exception.Message);
            if (exception.StackTrace is { } stack)
            {
                foreach (var line in stack.Split(Environment.NewLine))
                {
                    builder.AppendLine().Append("    ").Append(line.Trim());
                }
            }

            if (exception.InnerException is { } inner)
            {
                builder.AppendLine().Append("    inner ").Append(inner.GetType().Name).Append(": ").Append(inner.Message);
            }
        }

        var path = LogFilePath!;
        try
        {
            lock (Gate)
            {
                File.AppendAllText(path, builder.ToString() + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // Never let logging failures propagate.
        }
    }
}

/// <summary>Routes Microsoft.Extensions.Logging into the file log.</summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName);

    public void Dispose()
    {
    }

    private sealed class FileLogger(string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel is not (LogLevel.None or LogLevel.Trace or LogLevel.Debug);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            var level = logLevel switch
            {
                LogLevel.Critical or LogLevel.Error => "ERROR",
                LogLevel.Warning => "WARN",
                _ => "INFO",
            };

            // The category name is long; keep only the service that logged it.
            var shortCategory = categoryName;
            var dot = categoryName.LastIndexOf('.');
            if (dot >= 0)
            {
                shortCategory = categoryName[(dot + 1)..];
            }

            if (exception is not null)
            {
                Write(level, $"{shortCategory}: {message}", exception);
            }
            else
            {
                Write(level, $"{shortCategory}: {message}", null);
            }
        }

        private static void Write(string level, string message, Exception? exception)
        {
            switch (level)
            {
                case "ERROR":
                    AppLog.Error(message, exception);
                    break;
                case "WARN":
                    AppLog.Warning(message, exception);
                    break;
                default:
                    AppLog.Info(message);
                    break;
            }
        }
    }
}
