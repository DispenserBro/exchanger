using Exchanger.Core.Configuration;
using Exchanger.Core.Session;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;

namespace Exchanger.Core.Logging;

/// <summary>
/// Godot-композиция чистого OperationJournalStore.
/// </summary>
public static class OperationJournal
{
    private static readonly object SyncRoot = new();
    private static OperationJournalStore? _store;

    public static void Initialize(LoggingSettings settings)
    {
        lock (SyncRoot)
        {
            string directory = ProjectSettings.GlobalizePath("user://logs");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "operations.jsonl");
            _store = new OperationJournalStore(path);
            IReadOnlyList<OperationJournalEntry> unfinished = _store.RecoverUnfinishedOperations();
            foreach (OperationJournalEntry entry in unfinished)
            {
                AppLogger.Warning(
                    "Recovery",
                    $"operation_id={entry.OperationId} outcome=UnknownAfterRestart action=ManualStatusCheck");
            }

            // Сначала закрываем незавершённые операции, затем архивируем файл:
            // иначе Started на границе ротации мог бы остаться без безопасного итога.
            RotateIfRequired(path, settings.MaxFileSizeMb);
            LogMaintenance.Prune(
                directory,
                settings.RetentionDays,
                settings.MaxFileSizeMb * 10L * 1024L * 1024L,
                Path.Combine(directory, "exchanger.log"),
                path,
                Path.Combine(directory, "settings-audit.jsonl"));
        }
    }

    public static void RecordStarted(SessionOperationSnapshot snapshot)
    {
        lock (SyncRoot)
        {
            _store?.RecordStarted(snapshot);
        }
    }

    public static void RecordFinished(SessionOperationSnapshot snapshot)
    {
        lock (SyncRoot)
        {
            _store?.RecordFinished(snapshot);
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
        string archive = $"operations-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}.jsonl";
        File.Move(path, Path.Combine(directory, archive));
    }
}
