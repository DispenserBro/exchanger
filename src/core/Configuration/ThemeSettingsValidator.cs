using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Exchanger.Core.Configuration;

public enum ThemeValidationField
{
    ExternalDlcPath,
}

public readonly record struct ThemeValidationIssue(ThemeValidationField Field, string Message);

public sealed class ThemeValidationResult
{
    public ThemeValidationResult(IReadOnlyList<ThemeValidationIssue> issues)
    {
        Issues = issues ?? throw new ArgumentNullException(nameof(issues));
    }

    public IReadOnlyList<ThemeValidationIssue> Issues { get; }

    public bool IsValid => Issues.Count == 0;
}

/// <summary>Проверяет путь к единственному внешнему PCK без монтирования пакета.</summary>
public static class ThemeSettingsValidator
{
    public const int MaximumDlcPathLength = 512;

    public static ThemeValidationResult Validate(ThemeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var issues = new List<ThemeValidationIssue>();
        if (!settings.ExternalDlcEnabled)
        {
            return new ThemeValidationResult(issues);
        }

        string path = settings.ExternalDlcPath?.Trim() ?? string.Empty;
        if (path.Length == 0)
        {
            issues.Add(new ThemeValidationIssue(
                ThemeValidationField.ExternalDlcPath,
                "Укажите путь к внешнему DLC или отключите его загрузку."));
            return new ThemeValidationResult(issues);
        }

        if (path.Length > MaximumDlcPathLength || path.Any(char.IsControl))
        {
            issues.Add(new ThemeValidationIssue(
                ThemeValidationField.ExternalDlcPath,
                "Путь к DLC содержит недопустимые символы или слишком длинный."));
            return new ThemeValidationResult(issues);
        }

        bool userPath = path.StartsWith("user://", StringComparison.OrdinalIgnoreCase);
        if (!userPath && !Path.IsPathFullyQualified(path))
        {
            issues.Add(new ThemeValidationIssue(
                ThemeValidationField.ExternalDlcPath,
                "Используйте user:// или абсолютный локальный путь к PCK."));
            return new ThemeValidationResult(issues);
        }

        string normalized = path.Replace('\\', '/');
        if (normalized.Split('/').Any(segment => segment == ".."))
        {
            issues.Add(new ThemeValidationIssue(
                ThemeValidationField.ExternalDlcPath,
                "Переходы «..» в пути к DLC запрещены."));
            return new ThemeValidationResult(issues);
        }

        if (!Path.GetExtension(path).Equals(".pck", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ThemeValidationIssue(
                ThemeValidationField.ExternalDlcPath,
                "Внешняя тема должна быть пакетом с расширением .pck."));
        }

        return new ThemeValidationResult(issues);
    }
}
