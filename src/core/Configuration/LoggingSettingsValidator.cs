using System;
using System.Collections.Generic;

namespace Exchanger.Core.Configuration;

public enum LoggingValidationField
{
    MinimumLevel,
    MaxFileSizeMb,
    RetentionDays,
}

public readonly record struct LoggingValidationIssue(LoggingValidationField Field, string Message);

public sealed class LoggingValidationResult
{
    public LoggingValidationResult(IReadOnlyList<LoggingValidationIssue> issues) => Issues = issues;

    public IReadOnlyList<LoggingValidationIssue> Issues { get; }

    public bool IsValid => Issues.Count == 0;
}

/// <summary>Безопасные операторские границы файлового журнала.</summary>
public static class LoggingSettingsValidator
{
    public static readonly string[] AllowedLevels = ["Info", "Warning", "Error"];

    public const int MinimumFileSizeMb = 1;
    public const int MaximumFileSizeMb = 100;
    public const int MinimumRetentionDays = 1;
    public const int MaximumRetentionDays = 365;

    public static LoggingValidationResult Validate(LoggingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var issues = new List<LoggingValidationIssue>();

        if (Array.FindIndex(
                AllowedLevels,
                level => level.Equals(settings.MinimumLevel?.Trim(), StringComparison.OrdinalIgnoreCase)) < 0)
        {
            issues.Add(new LoggingValidationIssue(
                LoggingValidationField.MinimumLevel,
                "Выберите уровень Info, Warning или Error."));
        }

        if (settings.MaxFileSizeMb is < MinimumFileSizeMb or > MaximumFileSizeMb)
        {
            issues.Add(new LoggingValidationIssue(
                LoggingValidationField.MaxFileSizeMb,
                $"Размер файла должен быть от {MinimumFileSizeMb} до {MaximumFileSizeMb} МБ."));
        }

        if (settings.RetentionDays is < MinimumRetentionDays or > MaximumRetentionDays)
        {
            issues.Add(new LoggingValidationIssue(
                LoggingValidationField.RetentionDays,
                $"Срок хранения должен быть от {MinimumRetentionDays} до {MaximumRetentionDays} дней."));
        }

        return new LoggingValidationResult(issues);
    }

    public static string NormalizeLevel(string? value)
    {
        string candidate = value?.Trim() ?? string.Empty;
        foreach (string level in AllowedLevels)
        {
            if (level.Equals(candidate, StringComparison.OrdinalIgnoreCase))
            {
                return level;
            }
        }

        return "Info";
    }
}
