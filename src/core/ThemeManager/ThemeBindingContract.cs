using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using GodotFileAccess = Godot.FileAccess;

namespace Exchanger.Core.Theming;

public enum ThemeBindingContractLoadFailure
{
    None,
    Missing,
    Unreadable,
    HashMismatch,
    Invalid,
}

public sealed class ThemeBindingRequirement
{
    public string Id { get; set; } = string.Empty;
    public string[] Types { get; set; } = [];
    public bool Required { get; set; }
    public string FallbackPath { get; set; } = string.Empty;
}

public sealed class ThemeScreenBindingContract
{
    public string BindingId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string RootName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public ThemeBindingRequirement[] Bindings { get; set; } = [];
}

/// <summary>
/// Загруженный из DLC каталог биндингов, подлинность которого закреплена SHA-256
/// в приложении. Благодаря этому тема несёт человекочитаемый контракт, но не может
/// самостоятельно ослабить требования приложения.
/// </summary>
public sealed class ThemeBindingContract
{
    public const string ResourcePath =
        "res://exchanger_theme_dlc/contracts/screen_bindings.v2.json";
    public const int RequiredBindingCount = 481;

    // SHA-256 точного screen_bindings.v2.json, общего для приложения и Theme Builder.
    public const string ExpectedSha256 =
        "DDDC89118BEEA78A820561464001656136ED42AC1AD5710FB109D25ADAF44143";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IReadOnlyDictionary<string, ThemeScreenBindingContract> _screens;

    private ThemeBindingContract(
        IReadOnlyDictionary<string, ThemeScreenBindingContract> screens)
    {
        _screens = screens;
    }

    public ThemeScreenBindingContract GetScreen(string bindingId) => _screens[bindingId];

    public static ThemeBindingContractLoadFailure TryLoad(out ThemeBindingContract? contract)
    {
        contract = null;
        if (!GodotFileAccess.FileExists(ResourcePath))
        {
            return ThemeBindingContractLoadFailure.Missing;
        }

        byte[] bytes;
        try
        {
            using GodotFileAccess? file = GodotFileAccess.Open(ResourcePath, GodotFileAccess.ModeFlags.Read);
            if (file is null)
            {
                return ThemeBindingContractLoadFailure.Unreadable;
            }

            bytes = file.GetBuffer((long)file.GetLength());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ThemeBindingContractLoadFailure.Unreadable;
        }

        string actualHash = Convert.ToHexString(SHA256.HashData(bytes));
        if (!actualHash.Equals(ExpectedSha256, StringComparison.Ordinal))
        {
            return ThemeBindingContractLoadFailure.HashMismatch;
        }

        ThemeBindingContractDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<ThemeBindingContractDocument>(bytes, JsonOptions);
        }
        catch (JsonException)
        {
            return ThemeBindingContractLoadFailure.Invalid;
        }

        if (!TryValidate(document, out Dictionary<string, ThemeScreenBindingContract>? screens))
        {
            return ThemeBindingContractLoadFailure.Invalid;
        }

        contract = new ThemeBindingContract(screens);
        return ThemeBindingContractLoadFailure.None;
    }

    private static bool TryValidate(
        ThemeBindingContractDocument? document,
        out Dictionary<string, ThemeScreenBindingContract> screens)
    {
        screens = new Dictionary<string, ThemeScreenBindingContract>(StringComparer.Ordinal);
        if (document is null
            || !document.Format.Equals("ExchangerThemeBindingContract", StringComparison.Ordinal)
            || document.Version != ThemeDlcManifestValidator.SupportedFormatVersion
            || !document.ScreenMetadataKey.Equals(ThemeSceneContract.ScreenBindingMetadata, StringComparison.Ordinal)
            || !document.ElementMetadataKey.Equals(ThemeSceneContract.ElementBindingMetadata, StringComparison.Ordinal)
            || document.Screens.Length != ThemeSceneContract.Screens.Count)
        {
            return false;
        }

        var allBindings = new HashSet<string>(StringComparer.Ordinal);
        foreach (ThemeScreenBindingContract screen in document.Screens)
        {
            if (!ThemeSceneContract.TryGetByBindingId(screen.BindingId, out ThemeScreenDescriptor descriptor)
                || !screen.Key.Equals(descriptor.Key, StringComparison.Ordinal)
                || !screen.RootName.Equals(descriptor.RootName, StringComparison.Ordinal)
                || !screen.Path.Equals(descriptor.ScenePath, StringComparison.Ordinal)
                || screen.Bindings.Length == 0
                || !screens.TryAdd(screen.BindingId, screen))
            {
                return false;
            }

            foreach (ThemeBindingRequirement binding in screen.Bindings)
            {
                if (!binding.Required
                    || string.IsNullOrWhiteSpace(binding.FallbackPath)
                    || binding.Types.Length == 0
                    || binding.Types.Any(string.IsNullOrWhiteSpace)
                    || !ThemeSceneContract.IsValidElementBinding(descriptor.Key, binding.Id)
                    || !allBindings.Add(binding.Id))
                {
                    return false;
                }
            }
        }

        foreach (ThemeScreenDescriptor screen in ThemeSceneContract.Screens)
        {
            if (!screens.ContainsKey(screen.BindingId))
            {
                return false;
            }
        }

        return allBindings.Count == RequiredBindingCount;
    }

    private sealed class ThemeBindingContractDocument
    {
        public string Format { get; set; } = string.Empty;
        public int Version { get; set; }
        public string ScreenMetadataKey { get; set; } = string.Empty;
        public string ElementMetadataKey { get; set; } = string.Empty;
        public ThemeScreenBindingContract[] Screens { get; set; } = [];
    }
}
