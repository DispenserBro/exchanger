using System;
using System.Collections.Generic;

namespace Exchanger.Core.Theming;

public sealed class ThemeDlcManifest
{
    public string Format { get; set; } = string.Empty;

    public int FormatVersion { get; set; }

    public string ThemeId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string UiThemePath { get; set; } = string.Empty;

    public string BackgroundTexturePath { get; set; } = string.Empty;

    public string BackgroundDecorationPath { get; set; } = string.Empty;

    public float BackgroundDecorationScale { get; set; } = 1f;

    public string InteractivePetScene { get; set; } = string.Empty;

    public Dictionary<string, string> VisualSlots { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> ScreenScenes { get; set; } = new(StringComparer.Ordinal);

    public string FallbackColor { get; set; } = "#061229";

    public bool UseNearestTextureFilter { get; set; }
}

public enum ThemeDlcManifestFailure
{
    None,
    WrongFormat,
    UnsupportedVersion,
    InvalidThemeId,
    InvalidDisplayName,
    InvalidUiThemePath,
    InvalidBackgroundTexturePath,
    InvalidBackgroundDecorationPath,
    InvalidBackgroundDecorationScale,
    InvalidInteractivePetScenePath,
    InvalidVisualSlot,
    InvalidScreenScene,
    InvalidFallbackColor,
}

public static class ThemeDlcManifestValidator
{
    public const string ExpectedFormat = "ExchangerThemeDlc";
    public const int LegacyFormatVersion = 1;
    public const int SupportedFormatVersion = 2;
    public const string ResourceRoot = "res://exchanger_theme_dlc/";
    public const string ManifestPath = ResourceRoot + "manifest.json";

    public static ThemeDlcManifestFailure Validate(ThemeDlcManifest? manifest)
    {
        if (manifest is null || !manifest.Format.Equals(ExpectedFormat, StringComparison.Ordinal))
        {
            return ThemeDlcManifestFailure.WrongFormat;
        }

        if (manifest.FormatVersion is < LegacyFormatVersion or > SupportedFormatVersion)
        {
            return ThemeDlcManifestFailure.UnsupportedVersion;
        }

        if (!IsValidThemeId(manifest.ThemeId))
        {
            return ThemeDlcManifestFailure.InvalidThemeId;
        }

        if (string.IsNullOrWhiteSpace(manifest.DisplayName) || manifest.DisplayName.Trim().Length > 80)
        {
            return ThemeDlcManifestFailure.InvalidDisplayName;
        }

        if (!IsDlcResourcePath(manifest.UiThemePath, ".tres", ".res"))
        {
            return ThemeDlcManifestFailure.InvalidUiThemePath;
        }

        if (!IsOptionalDlcResourcePath(manifest.BackgroundTexturePath, ".png", ".webp", ".jpg", ".jpeg", ".svg"))
        {
            return ThemeDlcManifestFailure.InvalidBackgroundTexturePath;
        }

        if (!IsOptionalDlcResourcePath(manifest.BackgroundDecorationPath, ".tscn", ".scn"))
        {
            return ThemeDlcManifestFailure.InvalidBackgroundDecorationPath;
        }

        if (!float.IsFinite(manifest.BackgroundDecorationScale)
            || manifest.BackgroundDecorationScale is < 0.1f or > 4f)
        {
            return ThemeDlcManifestFailure.InvalidBackgroundDecorationScale;
        }

        if (!IsOptionalDlcResourcePath(manifest.InteractivePetScene, ".tscn", ".scn"))
        {
            return ThemeDlcManifestFailure.InvalidInteractivePetScenePath;
        }

        if (manifest.VisualSlots is null || manifest.VisualSlots.Count > 32)
        {
            return ThemeDlcManifestFailure.InvalidVisualSlot;
        }

        foreach ((string slotId, string path) in manifest.VisualSlots)
        {
            if (!IsValidSlotId(slotId) || !IsDlcResourcePath(path, ".tscn", ".scn"))
            {
                return ThemeDlcManifestFailure.InvalidVisualSlot;
            }
        }

        if (manifest.ScreenScenes is null)
        {
            return ThemeDlcManifestFailure.InvalidScreenScene;
        }

        // Полносценовые темы требуют строгий v2-контракт биндингов. Старый v1
        // допускается только для UI-theme/слотов: иначе новый host может упасть
        // при обращении к отсутствующему обязательному элементу сцены.
        if (manifest.FormatVersion == LegacyFormatVersion && manifest.ScreenScenes.Count > 0)
        {
            return ThemeDlcManifestFailure.InvalidScreenScene;
        }

        if (manifest.FormatVersion >= 2)
        {
            if (manifest.ScreenScenes.Count != ThemeSceneContract.Screens.Count)
            {
                return ThemeDlcManifestFailure.InvalidScreenScene;
            }

            foreach (ThemeScreenDescriptor descriptor in ThemeSceneContract.Screens)
            {
                if (!manifest.ScreenScenes.TryGetValue(descriptor.BindingId, out string? path)
                    || !path.Equals(descriptor.ScenePath, StringComparison.Ordinal))
                {
                    return ThemeDlcManifestFailure.InvalidScreenScene;
                }
            }
        }
        else if (manifest.ScreenScenes.Count > 0)
        {
            return ThemeDlcManifestFailure.InvalidScreenScene;
        }

        if (!IsHexColor(manifest.FallbackColor))
        {
            return ThemeDlcManifestFailure.InvalidFallbackColor;
        }

        return ThemeDlcManifestFailure.None;
    }

    private static bool IsValidThemeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64 || !char.IsLetterOrDigit(value[0]))
        {
            return false;
        }

        foreach (char character in value)
        {
            if (!char.IsLetterOrDigit(character) && character is not ('-' or '_' or '.'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidSlotId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64 || !char.IsLetterOrDigit(value[0]))
        {
            return false;
        }

        foreach (char character in value)
        {
            if (!char.IsLetterOrDigit(character) && character is not ('-' or '_' or '.'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsOptionalDlcResourcePath(string value, params string[] extensions)
    {
        return string.IsNullOrWhiteSpace(value) || IsDlcResourcePath(value, extensions);
    }

    private static bool IsDlcResourcePath(string value, params string[] extensions)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !value.StartsWith(ResourceRoot, StringComparison.Ordinal)
            || value.Contains("..", StringComparison.Ordinal)
            || value.Contains('\\'))
        {
            return false;
        }

        foreach (string extension in extensions)
        {
            if (value.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHexColor(string value)
    {
        string hex = (value ?? string.Empty).Trim().TrimStart('#');
        if (hex.Length is not (6 or 8))
        {
            return false;
        }

        foreach (char character in hex)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
