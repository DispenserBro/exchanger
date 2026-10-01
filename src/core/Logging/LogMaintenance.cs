using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Exchanger.Core.Logging;

public readonly record struct LogMaintenanceResult(int ExpiredDeleted, int QuotaDeleted, long RemainingBytes);

/// <summary>Ограничивает общий объём только известных журналов Exchanger.</summary>
public static class LogMaintenance
{
    private static readonly string[] Patterns = ["exchanger*.log", "operations*.jsonl", "settings-audit*.jsonl"];

    public static LogMaintenanceResult Prune(
        string directory,
        int retentionDays,
        long maximumTotalBytes,
        params string[] protectedPaths)
    {
        if (!Directory.Exists(directory))
        {
            return new LogMaintenanceResult(0, 0, 0);
        }

        string[] protectedFullPaths = (protectedPaths ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToArray();
        var files = Patterns
            .SelectMany(pattern => Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new FileInfo(path))
            .ToList();
        DateTime threshold = DateTime.UtcNow.AddDays(-Math.Clamp(retentionDays, 1, 365));
        int expiredDeleted = 0;
        int quotaDeleted = 0;

        foreach (FileInfo file in files.Where(file => file.LastWriteTimeUtc < threshold).ToArray())
        {
            if (IsProtected(file.FullName, protectedFullPaths) || !TryDelete(file.FullName))
            {
                continue;
            }

            files.Remove(file);
            expiredDeleted++;
        }

        long total = files.Where(file => file.Exists).Sum(file => file.Length);
        foreach (FileInfo file in files
                     .Where(file => !IsProtected(file.FullName, protectedFullPaths))
                     .OrderBy(file => file.LastWriteTimeUtc))
        {
            if (total <= Math.Max(1, maximumTotalBytes))
            {
                break;
            }

            long length = file.Exists ? file.Length : 0;
            if (!TryDelete(file.FullName))
            {
                continue;
            }

            total = Math.Max(0, total - length);
            quotaDeleted++;
        }

        return new LogMaintenanceResult(expiredDeleted, quotaDeleted, total);
    }

    private static bool IsProtected(string path, IEnumerable<string> protectedPaths) =>
        protectedPaths.Contains(Path.GetFullPath(path), StringComparer.OrdinalIgnoreCase);

    private static bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
