using Exchanger.Core.Configuration;
using Godot;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Exchanger.Core.Logging;

/// <summary>
/// Минимальный потокобезопасный журнал приложения с ограничением размера и
/// срока хранения. После инициализации не обращается к объектам Godot.
/// </summary>
public static class AppLogger
{
    private const int RecentEntryCapacity = 20;
    private static readonly object SyncRoot = new();
    private static string? _logFilePath;
    private static long _maxFileSizeBytes = 5L * 1024 * 1024;
    private static AppLogLevel _minimumLevel = AppLogLevel.Info;
    private static readonly Queue<SafeDiagnosticEntry> RecentEntries = new();

    public static void Initialize(LoggingSettings settings)
    {
        lock (SyncRoot)
        {
            string logDirectory = ProjectSettings.GlobalizePath("user://logs");
            Directory.CreateDirectory(logDirectory);

            _maxFileSizeBytes = settings.MaxFileSizeMb * 1024L * 1024L;
#if DEBUG
            _minimumLevel = AppLogLevel.Debug;
#else
            _minimumLevel = ParseLevel(settings.MinimumLevel);
#endif

            _logFilePath = Path.Combine(logDirectory, "exchanger.log");
            RotateIfRequired();
            LogMaintenance.Prune(
                logDirectory,
                settings.RetentionDays,
                _maxFileSizeBytes * 10,
                _logFilePath,
                Path.Combine(logDirectory, "operations.jsonl"),
                Path.Combine(logDirectory, "settings-audit.jsonl"));
            WriteUnsafe(AppLogLevel.Info, "Application", "Журнал приложения инициализирован.");
#if DEBUG
            WriteUnsafe(
                AppLogLevel.Debug,
                "Application",
                "Расширенная безопасная диагностика DEBUG-сборки включена.");
#endif
        }
    }

    [Conditional("DEBUG")]
    public static void Debug(string category, string message) =>
        Write(AppLogLevel.Debug, category, message);

    public static void Info(string category, string message) => Write(AppLogLevel.Info, category, message);

    public static void Warning(string category, string message) => Write(AppLogLevel.Warning, category, message);

    public static void Error(string category, string message, Exception? exception = null)
    {
        string details = exception is null ? message : $"{message} | {exception.GetType().Name}: {exception.Message}";
        Write(AppLogLevel.Error, category, details);
    }

    public static IReadOnlyList<SafeDiagnosticEntry> GetRecentSafeEntries(int maximumCount = 5)
    {
        lock (SyncRoot)
        {
            return RecentEntries.TakeLast(Math.Clamp(maximumCount, 0, RecentEntryCapacity)).ToArray();
        }
    }

    public static void Shutdown()
    {
        lock (SyncRoot)
        {
            if (_logFilePath is null)
            {
                return;
            }

            WriteUnsafe(AppLogLevel.Info, "Application", "Журнал приложения завершён.");
            _logFilePath = null;
        }
    }

    private static void Write(AppLogLevel level, string category, string message)
    {
        lock (SyncRoot)
        {
            if (level < _minimumLevel)
            {
                return;
            }

            RotateIfRequired();
            WriteUnsafe(level, category, message);
        }
    }

    private static void WriteUnsafe(AppLogLevel level, string category, string message)
    {
        DateTimeOffset timestamp = DateTimeOffset.Now;
        string line = $"{timestamp:O} [{FormatLevel(level)}] [{category}] {message}";
        Console.WriteLine(line);

        if (level >= AppLogLevel.Warning)
        {
            RecentEntries.Enqueue(new SafeDiagnosticEntry(
                timestamp,
                level,
                NormalizeCategory(category),
                SafeDiagnosticText.Sanitize(message)));
            while (RecentEntries.Count > RecentEntryCapacity)
            {
                RecentEntries.Dequeue();
            }
        }

        if (_logFilePath is not null)
        {
            File.AppendAllText(_logFilePath, line + System.Environment.NewLine);
        }
    }

    private static void RotateIfRequired()
    {
        if (_logFilePath is null || !File.Exists(_logFilePath))
        {
            return;
        }

        var file = new FileInfo(_logFilePath);
        if (file.Length < _maxFileSizeBytes)
        {
            return;
        }

        string directory = file.DirectoryName ?? string.Empty;
        string archiveName = $"exchanger-{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}.log";
        File.Move(_logFilePath, Path.Combine(directory, archiveName));
    }

    private static AppLogLevel ParseLevel(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "WARNING" => AppLogLevel.Warning,
        "ERROR" => AppLogLevel.Error,
        _ => AppLogLevel.Info,
    };

    private static string FormatLevel(AppLogLevel level) => level == AppLogLevel.Warning ? "WARN" : level.ToString().ToUpperInvariant();

    private static string NormalizeCategory(string? category) => string.IsNullOrWhiteSpace(category)
        ? "Application"
        : category.Trim()[..Math.Min(category.Trim().Length, 48)];
}

public enum AppLogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
}

public readonly record struct SafeDiagnosticEntry(
    DateTimeOffset Timestamp,
    AppLogLevel Level,
    string Category,
    string Message);

public static class SafeDiagnosticText
{
    public static string Sanitize(string? value)
    {
        string text = string.Join(" ", (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (text.Contains("user://", StringComparison.OrdinalIgnoreCase)
            || text.Contains("res://", StringComparison.OrdinalIgnoreCase)
            || text.Contains(":\\", StringComparison.Ordinal))
        {
            return "Подробности с локальным путём скрыты.";
        }

        if (text.Contains("RAW", StringComparison.OrdinalIgnoreCase)
            || text.Contains("FRAME", StringComparison.OrdinalIgnoreCase))
        {
            return "Содержимое протокольного сообщения скрыто.";
        }

        return text.Length <= 180 ? text : text[..180] + "…";
    }
}
