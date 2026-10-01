using Exchanger.Core.Session;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Exchanger.Core.Logging;

public enum OperationJournalEventType
{
    Started,
    Finished,
}

public sealed record OperationJournalEntry
{
    public DateTimeOffset TimestampUtc { get; init; }

    public OperationJournalEventType EventType { get; init; }

    public string OperationId { get; init; } = string.Empty;

    public PaymentMethod PaymentMethod { get; init; }

    public int AmountRubles { get; init; }

    public int BaseTokens { get; init; }

    public int BonusTokens { get; init; }

    public OperationOutcome Outcome { get; init; }

    public SessionErrorCode ErrorCode { get; init; }
}

/// <summary>
/// Чистое JSONL-хранилище жизненного цикла операций. Не содержит карточных
/// реквизитов, сырых ответов оборудования или персональных данных.
/// </summary>
public sealed class OperationJournalStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object _syncRoot = new();
    private readonly string _path;
    private readonly Func<DateTimeOffset> _utcNow;

    public OperationJournalStore(string path, Func<DateTimeOffset>? utcNow = null)
    {
        _path = string.IsNullOrWhiteSpace(path)
            ? throw new ArgumentException("Путь журнала операций не задан.", nameof(path))
            : path;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public void RecordStarted(SessionOperationSnapshot snapshot) => Append(snapshot, OperationJournalEventType.Started);

    public void RecordFinished(SessionOperationSnapshot snapshot) => Append(snapshot, OperationJournalEventType.Finished);

    public IReadOnlyList<OperationJournalEntry> FindUnfinishedOperations()
    {
        lock (_syncRoot)
        {
            return FindUnfinishedUnsafe();
        }
    }

    public IReadOnlyList<OperationJournalEntry> RecoverUnfinishedOperations()
    {
        lock (_syncRoot)
        {
            IReadOnlyList<OperationJournalEntry> unfinished = FindUnfinishedUnsafe();
            foreach (OperationJournalEntry entry in unfinished)
            {
                var snapshot = new SessionOperationSnapshot(
                    entry.OperationId,
                    entry.PaymentMethod,
                    entry.AmountRubles,
                    entry.BaseTokens,
                    entry.BonusTokens,
                    OperationOutcome.UnknownAfterRestart,
                    SessionErrorCode.None);
                AppendUnsafe(snapshot, OperationJournalEventType.Finished);
            }

            return unfinished;
        }
    }

    private void Append(SessionOperationSnapshot snapshot, OperationJournalEventType eventType)
    {
        if (string.IsNullOrWhiteSpace(snapshot.OperationId))
        {
            throw new ArgumentException("Нельзя записать операцию без идентификатора.", nameof(snapshot));
        }

        lock (_syncRoot)
        {
            AppendUnsafe(snapshot, eventType);
        }
    }

    private void AppendUnsafe(SessionOperationSnapshot snapshot, OperationJournalEventType eventType)
    {
        string? directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var entry = new OperationJournalEntry
        {
            TimestampUtc = _utcNow().ToUniversalTime(),
            EventType = eventType,
            OperationId = snapshot.OperationId,
            PaymentMethod = snapshot.PaymentMethod,
            AmountRubles = snapshot.AmountRubles,
            BaseTokens = snapshot.BaseTokens,
            BonusTokens = snapshot.BonusTokens,
            Outcome = snapshot.Outcome,
            ErrorCode = snapshot.ErrorCode,
        };
        File.AppendAllText(_path, JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine);
    }

    private IReadOnlyList<OperationJournalEntry> FindUnfinishedUnsafe()
    {
        if (!File.Exists(_path))
        {
            return Array.Empty<OperationJournalEntry>();
        }

        var open = new Dictionary<string, OperationJournalEntry>(StringComparer.Ordinal);
        foreach (string line in File.ReadLines(_path))
        {
            try
            {
                OperationJournalEntry? entry = JsonSerializer.Deserialize<OperationJournalEntry>(line, JsonOptions);
                if (entry is null || string.IsNullOrWhiteSpace(entry.OperationId))
                {
                    continue;
                }

                if (entry.EventType == OperationJournalEventType.Started)
                {
                    open[entry.OperationId] = entry;
                }
                else
                {
                    open.Remove(entry.OperationId);
                }
            }
            catch (JsonException)
            {
                // Повреждённая строка не мешает восстановить остальные записи.
            }
        }

        return open.Values.OrderBy(entry => entry.TimestampUtc).ToArray();
    }
}
