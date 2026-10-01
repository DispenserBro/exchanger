using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Exchanger.Core.Configuration;

public enum AdvertisementAssetType
{
    Poster,
    Video,
}

public enum AdvertisementValidationField
{
    FallbackText,
    PosterPath,
    VideoPath,
    Playlist,
}

public readonly record struct AdvertisementValidationIssue(
    AdvertisementValidationField Field,
    string Message,
    int VideoIndex = -1);

public sealed class AdvertisementValidationResult
{
    public AdvertisementValidationResult(IReadOnlyList<AdvertisementValidationIssue> issues)
    {
        Issues = issues ?? throw new ArgumentNullException(nameof(issues));
    }

    public IReadOnlyList<AdvertisementValidationIssue> Issues { get; }

    public bool IsValid => Issues.Count == 0;
}

/// <summary>Проверяет только локальные промо-ресурсы и безопасные ограничения плейлиста.</summary>
public static class AdvertisementSettingsValidator
{
    public const int MaximumFallbackTextLength = 120;
    public const int MaximumAssetPathLength = 512;
    public const int MaximumVideoPaths = 12;

    private static readonly HashSet<string> PosterExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ogv",
        ".mp4",
        ".mov",
        ".m4v",
    };

    public static AdvertisementValidationResult Validate(
        AdvertisementSettings settings,
        Func<string, AdvertisementAssetType, bool>? assetExists = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var issues = new List<AdvertisementValidationIssue>();
        if (!settings.Enabled)
        {
            return new AdvertisementValidationResult(issues);
        }

        ValidateFallback(settings.FallbackText, issues);
        if (!string.IsNullOrWhiteSpace(settings.PosterPath))
        {
            ValidateAssetPath(
                settings.PosterPath,
                AdvertisementAssetType.Poster,
                AdvertisementValidationField.PosterPath,
                -1,
                assetExists,
                issues);
        }

        string[] paths = settings.VideoPaths ?? Array.Empty<string>();
        if (paths.Length > MaximumVideoPaths)
        {
            issues.Add(new AdvertisementValidationIssue(
                AdvertisementValidationField.Playlist,
                $"Можно добавить не больше {MaximumVideoPaths} промо-роликов."));
        }

        for (int index = 0; index < paths.Length; index++)
        {
            ValidateAssetPath(
                paths[index],
                AdvertisementAssetType.Video,
                AdvertisementValidationField.VideoPath,
                index,
                assetExists,
                issues);
        }

        foreach (IGrouping<string, (string Path, int Index)> duplicate in paths
                     .Select((path, index) => (Path: AdvertisementVideoLibrary.NormalizeSelectedVideo(path ?? string.Empty), Index: index))
                     .Where(item => item.Path.Length > 0)
                     .GroupBy(item => AdvertisementVideoLibrary.GetComparisonKey(item.Path), StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            foreach ((string _, int index) in duplicate)
            {
                issues.Add(new AdvertisementValidationIssue(
                    AdvertisementValidationField.VideoPath,
                    "Этот промо-ролик уже добавлен.",
                    index));
            }
        }

        return new AdvertisementValidationResult(issues);
    }

    private static void ValidateFallback(
        string? value,
        ICollection<AdvertisementValidationIssue> issues)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            issues.Add(new AdvertisementValidationIssue(
                AdvertisementValidationField.FallbackText,
                "Введите текст, который будет показан вместо промо."));
            return;
        }

        if (trimmed.Length > MaximumFallbackTextLength)
        {
            issues.Add(new AdvertisementValidationIssue(
                AdvertisementValidationField.FallbackText,
                $"Текст вместо промо — не больше {MaximumFallbackTextLength} знаков."));
        }

        if (trimmed.Any(char.IsControl))
        {
            issues.Add(new AdvertisementValidationIssue(
                AdvertisementValidationField.FallbackText,
                "Введите текст вместо промо в одну строку."));
        }
    }

    private static void ValidateAssetPath(
        string? value,
        AdvertisementAssetType assetType,
        AdvertisementValidationField field,
        int videoIndex,
        Func<string, AdvertisementAssetType, bool>? assetExists,
        ICollection<AdvertisementValidationIssue> issues)
    {
        string path = value?.Trim() ?? string.Empty;
        string caption = assetType == AdvertisementAssetType.Poster ? "Постер" : "Промо-ролик";
        if (path.Length == 0)
        {
            issues.Add(new AdvertisementValidationIssue(field, $"Укажите путь к файлу «{caption}».", videoIndex));
            return;
        }

        if (path.Length > MaximumAssetPathLength || path.Any(char.IsControl))
        {
            issues.Add(new AdvertisementValidationIssue(field, $"Проверьте путь к файлу «{caption}».", videoIndex));
            return;
        }

        bool resourcePath = path.StartsWith("res://", StringComparison.OrdinalIgnoreCase)
                            || path.StartsWith("user://", StringComparison.OrdinalIgnoreCase);
        if (!resourcePath && !Path.IsPathFullyQualified(path))
        {
            issues.Add(new AdvertisementValidationIssue(
                field,
                $"Укажите полный путь к файлу «{caption}».",
                videoIndex));
            return;
        }

        string normalized = path.Replace('\\', '/');
        if (normalized.Split('/').Any(segment => segment == ".."))
        {
            issues.Add(new AdvertisementValidationIssue(field, $"Путь к файлу «{caption}» не должен содержать «..».", videoIndex));
            return;
        }

        string extension = Path.GetExtension(path);
        bool supported = assetType == AdvertisementAssetType.Poster
            ? PosterExtensions.Contains(extension)
            : VideoExtensions.Contains(extension);
        if (!supported)
        {
            string formats = assetType == AdvertisementAssetType.Poster
                ? "PNG, JPG, JPEG или WEBP"
                : "OGV, MP4, MOV или M4V";
            issues.Add(new AdvertisementValidationIssue(
                field,
                $"Для файла «{caption}» нужен формат {formats}.",
                videoIndex));
            return;
        }

        if (assetExists is not null && !assetExists(path, assetType))
        {
            issues.Add(new AdvertisementValidationIssue(
                field,
                $"Не удалось открыть файл «{caption}». Проверьте путь.",
                videoIndex));
        }
    }
}
