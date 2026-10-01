using System;
using System.Collections.Generic;
using System.Linq;

namespace Exchanger.Core.Configuration;

public enum BrandingValidationField
{
    ApplicationName,
    ShortText,
    SupportPhone,
}

public readonly record struct BrandingValidationIssue(BrandingValidationField Field, string Message);

public sealed class BrandingValidationResult
{
    public BrandingValidationResult(IReadOnlyList<BrandingValidationIssue> issues)
    {
        Issues = issues ?? throw new ArgumentNullException(nameof(issues));
    }

    public IReadOnlyList<BrandingValidationIssue> Issues { get; }

    public bool IsValid => Issues.Count == 0;
}

/// <summary>Проверяет короткие однострочные тексты, видимые пользователю на домашнем экране.</summary>
public static class BrandingSettingsValidator
{
    public const int MaximumApplicationNameLength = 48;
    public const int MaximumShortTextLength = 48;
    public const int MaximumSupportPhoneLength = 80;

    public static BrandingValidationResult Validate(BrandingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var issues = new List<BrandingValidationIssue>();
        ValidateText(
            settings.ApplicationName,
            MaximumApplicationNameLength,
            BrandingValidationField.ApplicationName,
            "Название автомата",
            issues);
        ValidateText(
            settings.ShortText,
            MaximumShortTextLength,
            BrandingValidationField.ShortText,
            "Свой текст",
            issues);
        ValidateText(
            settings.SupportPhone,
            MaximumSupportPhoneLength,
            BrandingValidationField.SupportPhone,
            "Телефон поддержки",
            issues);

        return new BrandingValidationResult(issues);
    }

    private static void ValidateText(
        string? value,
        int maximumLength,
        BrandingValidationField field,
        string caption,
        ICollection<BrandingValidationIssue> issues)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            issues.Add(new BrandingValidationIssue(field, $"Заполните поле «{caption}»."));
            return;
        }

        if (trimmed.Length > maximumLength)
        {
            issues.Add(new BrandingValidationIssue(
                field,
                $"{caption} — не больше {maximumLength} знаков."));
        }

        if (trimmed.Any(char.IsControl))
        {
            issues.Add(new BrandingValidationIssue(field, $"Введите поле «{caption}» в одну строку."));
        }
    }
}
