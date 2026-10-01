using System;
using System.IO;
using System.Text.Json;

namespace Exchanger.Core.Inventory;

/// <summary>Точный внутренний учёт по двум хопперам. Count всегда равен их сумме.</summary>
public sealed record TokenInventorySnapshot
{
    public int Count { get; init; }

    public int Hopper1Count { get; init; }

    public int Hopper2Count { get; init; }

    public long Revision { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }
}

/// <summary>
/// Атомарно хранит учётный остаток отдельно от appsettings.json. При повреждении
/// основного файла восстанавливается из последней проверенной копии.
/// </summary>
public sealed class TokenInventoryStore
{
    public const int MaximumTokenCount = 1_000_000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly object _syncRoot = new();
    private readonly string _path;
    private readonly string _backupPath;
    private readonly string _temporaryPath;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly Action<string>? _warningSink;
    private TokenInventorySnapshot? _current;

    public TokenInventoryStore(
        string path,
        Func<DateTimeOffset>? utcNow = null,
        Action<string>? warningSink = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Путь файла учёта жетонов не задан.", nameof(path));
        }

        _path = Path.GetFullPath(path);
        _backupPath = _path + ".bak";
        _temporaryPath = _path + ".tmp";
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        _warningSink = warningSink;
    }

    public TokenInventorySnapshot Load()
    {
        lock (_syncRoot)
        {
            TokenInventorySnapshot? loaded = TryRead(_path, "основной файл")
                                             ?? TryRead(_backupPath, "резервную копию");
            _current = loaded ?? CreateSnapshot(0, 0, 0);

            if (loaded is null || !File.Exists(_path))
            {
                WriteAtomic(_current);
            }

            return _current;
        }
    }

    /// <summary>Совместимость со старым API: заменяет общий остаток и относит его к первому хопперу.</summary>
    public TokenInventorySnapshot SetCount(int count) => SetCounts(count, 0);

    public TokenInventorySnapshot SetCounts(int hopper1Count, int hopper2Count)
    {
        ValidateCounts(hopper1Count, hopper2Count);
        lock (_syncRoot)
        {
            TokenInventorySnapshot current = EnsureLoaded();
            return Persist(hopper1Count, hopper2Count, current.Revision + 1);
        }
    }

    public TokenInventorySnapshot SetHopperCount(TokenHopper hopper, int count)
    {
        ValidateCount(count, nameof(count));
        lock (_syncRoot)
        {
            TokenInventorySnapshot current = EnsureLoaded();
            return hopper switch
            {
                TokenHopper.Hopper1 => Persist(count, current.Hopper2Count, current.Revision + 1),
                TokenHopper.Hopper2 => Persist(current.Hopper1Count, count, current.Revision + 1),
                _ => throw new ArgumentOutOfRangeException(nameof(hopper)),
            };
        }
    }

    /// <summary>Совместимость со старым API: добавляет жетоны в первый хоппер.</summary>
    public TokenInventorySnapshot Add(int count) => Add(TokenHopper.Hopper1, count);

    public TokenInventorySnapshot Add(TokenHopper hopper, int count)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Добавляемое количество должно быть больше нуля.");
        }

        lock (_syncRoot)
        {
            TokenInventorySnapshot current = EnsureLoaded();
            int hopper1 = current.Hopper1Count;
            int hopper2 = current.Hopper2Count;
            switch (hopper)
            {
                case TokenHopper.Hopper1:
                    hopper1 = checked(hopper1 + count);
                    break;
                case TokenHopper.Hopper2:
                    hopper2 = checked(hopper2 + count);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(hopper));
            }

            ValidateCounts(hopper1, hopper2);
            return Persist(hopper1, hopper2, current.Revision + 1);
        }
    }

    /// <summary>Совместимость со старым API: списывает сначала первый, затем второй хоппер.</summary>
    public TokenInventorySnapshot RemoveDispensed(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        lock (_syncRoot)
        {
            TokenInventorySnapshot current = EnsureLoaded();
            int fromFirst = Math.Min(current.Hopper1Count, count);
            int remaining = count - fromFirst;
            return count == 0
                ? current
                : Persist(
                    current.Hopper1Count - fromFirst,
                    Math.Max(0, current.Hopper2Count - remaining),
                    current.Revision + 1);
        }
    }

    public TokenInventorySnapshot RemoveDispensed(TokenHopper hopper, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        lock (_syncRoot)
        {
            TokenInventorySnapshot current = EnsureLoaded();
            if (count == 0)
            {
                return current;
            }

            return hopper switch
            {
                TokenHopper.Hopper1 => Persist(
                    Math.Max(0, current.Hopper1Count - count),
                    current.Hopper2Count,
                    current.Revision + 1),
                TokenHopper.Hopper2 => Persist(
                    current.Hopper1Count,
                    Math.Max(0, current.Hopper2Count - count),
                    current.Revision + 1),
                _ => throw new ArgumentOutOfRangeException(nameof(hopper)),
            };
        }
    }

    private TokenInventorySnapshot EnsureLoaded() => _current ?? Load();

    private TokenInventorySnapshot Persist(int hopper1Count, int hopper2Count, long revision)
    {
        TokenInventorySnapshot next = CreateSnapshot(hopper1Count, hopper2Count, revision);
        WriteAtomic(next);
        _current = next;
        return next;
    }

    private TokenInventorySnapshot CreateSnapshot(int hopper1Count, int hopper2Count, long revision) => new()
    {
        Count = hopper1Count + hopper2Count,
        Hopper1Count = hopper1Count,
        Hopper2Count = hopper2Count,
        Revision = revision,
        UpdatedAtUtc = _utcNow(),
    };

    private TokenInventorySnapshot? TryRead(string path, string description)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            TokenInventorySnapshot? snapshot = JsonSerializer.Deserialize<TokenInventorySnapshot>(
                File.ReadAllText(path),
                JsonOptions);
            if (snapshot is null || snapshot.Revision < 0)
            {
                return InvalidSnapshot(description);
            }

            int hopper1 = snapshot.Hopper1Count;
            int hopper2 = snapshot.Hopper2Count;
            if (hopper1 == 0 && hopper2 == 0 && snapshot.Count > 0)
            {
                hopper1 = snapshot.Count;
            }

            if (!AreCountsValid(hopper1, hopper2))
            {
                return InvalidSnapshot(description);
            }

            return snapshot with
            {
                Count = hopper1 + hopper2,
                Hopper1Count = hopper1,
                Hopper2Count = hopper2,
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _warningSink?.Invoke($"Не удалось прочитать {description} учёта жетонов: {exception.GetType().Name}.");
            return null;
        }
    }

    private TokenInventorySnapshot? InvalidSnapshot(string description)
    {
        _warningSink?.Invoke($"Не удалось прочитать {description} учёта жетонов: данные некорректны.");
        return null;
    }

    private void WriteAtomic(TokenInventorySnapshot snapshot)
    {
        string? directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            File.WriteAllText(_temporaryPath, JsonSerializer.Serialize(snapshot, JsonOptions));
            TokenInventorySnapshot? verified = TryRead(_temporaryPath, "временный файл");
            if (verified is null
                || verified.Hopper1Count != snapshot.Hopper1Count
                || verified.Hopper2Count != snapshot.Hopper2Count
                || verified.Revision != snapshot.Revision)
            {
                throw new IOException("Временный файл учёта жетонов не прошёл проверку.");
            }

            if (File.Exists(_path))
            {
                File.Copy(_path, _backupPath, overwrite: true);
                File.Move(_temporaryPath, _path, overwrite: true);
            }
            else
            {
                File.Move(_temporaryPath, _path);
            }
        }
        finally
        {
            if (File.Exists(_temporaryPath))
            {
                File.Delete(_temporaryPath);
            }
        }
    }

    private static bool AreCountsValid(int hopper1Count, int hopper2Count) =>
        hopper1Count >= 0
        && hopper2Count >= 0
        && (long)hopper1Count + hopper2Count <= MaximumTokenCount;

    private static void ValidateCounts(int hopper1Count, int hopper2Count)
    {
        if (!AreCountsValid(hopper1Count, hopper2Count))
        {
            throw new ArgumentOutOfRangeException(
                nameof(hopper1Count),
                $"Общий остаток двух хопперов должен быть от 0 до {MaximumTokenCount} жетонов.");
        }
    }

    private static void ValidateCount(int count, string parameterName)
    {
        if (count is < 0 or > MaximumTokenCount)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Остаток должен быть от 0 до {MaximumTokenCount} жетонов.");
        }
    }
}
