using Exchanger.Core.Configuration;
using Godot;
using System;
using System.IO;

namespace Exchanger.Core.Logging;

/// <summary>Godot-композиция безопасного журнала изменения конфигурации.</summary>
public static class ConfigurationAuditJournal
{
    private static readonly object SyncRoot = new();
    private static ConfigurationAuditStore? _store;

    public static void Initialize(LoggingSettings settings)
    {
        lock (SyncRoot)
        {
            string directory = ProjectSettings.GlobalizePath("user://logs");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "settings-audit.jsonl");
            RotateIfRequired(path, settings.MaxFileSizeMb);
            LogMaintenance.Prune(
                directory,
                settings.RetentionDays,
                settings.MaxFileSizeMb * 10L * 1024L * 1024L,
                Path.Combine(directory, "exchanger.log"),
                Path.Combine(directory, "operations.jsonl"),
                path);
            _store = new ConfigurationAuditStore(path);
        }
    }

    public static void Record(SettingsChangeSummary summary, ServiceAccessLevel level)
    {
        lock (SyncRoot)
        {
            string role = level switch
            {
                ServiceAccessLevel.Engineer => "Engineer",
                ServiceAccessLevel.Debug => "Debug",
                _ => "Operator",
            };
            _store?.Record(summary, role);
        }
    }

    public static void Shutdown()
    {
        lock (SyncRoot)
        {
            _store = null;
        }
    }

    private static void RotateIfRequired(string path, int maxFileSizeMb)
    {
        if (!File.Exists(path) || new FileInfo(path).Length < maxFileSizeMb * 1024L * 1024L)
        {
            return;
        }

        string directory = Path.GetDirectoryName(path) ?? string.Empty;
        string archive = $"settings-audit-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}.jsonl";
        File.Move(path, Path.Combine(directory, archive));
    }
}
