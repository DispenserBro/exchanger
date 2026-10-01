using Godot;
using Exchanger.Core.Navigation;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using GodotFileAccess = Godot.FileAccess;

namespace Exchanger.Core.Theming;

public enum ThemeDlcLoadStatus
{
    Disabled,
    NotFound,
    InvalidExternalPath,
    InvalidExtension,
    MountFailed,
    ManifestMissing,
    ManifestUnreadable,
    ManifestInvalid,
    ResourceMissing,
    ResourceTypeMismatch,
    BindingContractMissing,
    BindingContractInvalid,
    IncompatibleVisualComposition,
    Loaded,
}

public readonly record struct ThemeDlcLoadResult(
    ThemeDlcLoadStatus Status,
    VendingThemeDefinition? Theme = null,
    ThemeDlcManifestFailure ManifestFailure = ThemeDlcManifestFailure.None,
    ThemeBindingContractLoadFailure BindingContractFailure = ThemeBindingContractLoadFailure.None,
    ThemeScreenSceneFailure ScreenSceneFailure = ThemeScreenSceneFailure.None,
    string ScreenBindingId = "");

/// <summary>
/// Монтирует один внешний .pck без права заменять ресурсы приложения,
/// валидирует manifest и создаёт runtime-описание темы.
/// </summary>
public static class ThemeDlcLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static ThemeDlcLoadResult Load(string configuredPath)
    {
        string? absolutePath = ResolveExternalPath(configuredPath);
        if (absolutePath is null)
        {
            return new ThemeDlcLoadResult(ThemeDlcLoadStatus.InvalidExternalPath);
        }

        if (!Path.GetExtension(absolutePath).Equals(".pck", StringComparison.OrdinalIgnoreCase))
        {
            return new ThemeDlcLoadResult(ThemeDlcLoadStatus.InvalidExtension);
        }

        if (!File.Exists(absolutePath))
        {
            return new ThemeDlcLoadResult(ThemeDlcLoadStatus.NotFound);
        }

        if (!ProjectSettings.LoadResourcePack(absolutePath, replaceFiles: false))
        {
            return new ThemeDlcLoadResult(ThemeDlcLoadStatus.MountFailed);
        }

        if (!GodotFileAccess.FileExists(ThemeDlcManifestValidator.ManifestPath))
        {
            return new ThemeDlcLoadResult(ThemeDlcLoadStatus.ManifestMissing);
        }

        ThemeDlcManifest? manifest;
        try
        {
            using GodotFileAccess? file = GodotFileAccess.Open(
                ThemeDlcManifestValidator.ManifestPath,
                GodotFileAccess.ModeFlags.Read);
            if (file is null)
            {
                return new ThemeDlcLoadResult(ThemeDlcLoadStatus.ManifestUnreadable);
            }

            manifest = JsonSerializer.Deserialize<ThemeDlcManifest>(file.GetAsText(), JsonOptions);
        }
        catch (JsonException)
        {
            return new ThemeDlcLoadResult(ThemeDlcLoadStatus.ManifestUnreadable);
        }

        ThemeDlcManifestFailure failure = ThemeDlcManifestValidator.Validate(manifest);
        if (failure != ThemeDlcManifestFailure.None || manifest is null)
        {
            return new ThemeDlcLoadResult(ThemeDlcLoadStatus.ManifestInvalid, ManifestFailure: failure);
        }

        ThemeBindingContract? bindingContract = null;
        if (manifest.FormatVersion >= ThemeDlcManifestValidator.SupportedFormatVersion)
        {
            ThemeBindingContractLoadFailure contractFailure = ThemeBindingContract.TryLoad(out bindingContract);
            if (contractFailure == ThemeBindingContractLoadFailure.Missing)
            {
                return new ThemeDlcLoadResult(
                    ThemeDlcLoadStatus.BindingContractMissing,
                    BindingContractFailure: contractFailure);
            }

            if (contractFailure != ThemeBindingContractLoadFailure.None || bindingContract is null)
            {
                return new ThemeDlcLoadResult(
                    ThemeDlcLoadStatus.BindingContractInvalid,
                    BindingContractFailure: contractFailure);
            }
        }

        if (!ResourceLoader.Exists(manifest.UiThemePath)
            || (!string.IsNullOrWhiteSpace(manifest.BackgroundTexturePath)
                && !ResourceLoader.Exists(manifest.BackgroundTexturePath))
            || (!string.IsNullOrWhiteSpace(manifest.BackgroundDecorationPath)
                && !ResourceLoader.Exists(manifest.BackgroundDecorationPath))
            || (!string.IsNullOrWhiteSpace(manifest.InteractivePetScene)
                && !ResourceLoader.Exists(manifest.InteractivePetScene))
            || manifest.VisualSlots.Values.Any(path => !ResourceLoader.Exists(path))
            || manifest.ScreenScenes.Values.Any(path => !ResourceLoader.Exists(path)))
        {
            return new ThemeDlcLoadResult(ThemeDlcLoadStatus.ResourceMissing);
        }

        Theme? uiTheme = ResourceLoader.Load<Theme>(manifest.UiThemePath);
        Texture2D? background = string.IsNullOrWhiteSpace(manifest.BackgroundTexturePath)
            ? null
            : ResourceLoader.Load<Texture2D>(manifest.BackgroundTexturePath);
        PackedScene? decoration = string.IsNullOrWhiteSpace(manifest.BackgroundDecorationPath)
            ? null
            : ResourceLoader.Load<PackedScene>(manifest.BackgroundDecorationPath);
        PackedScene? interactivePet = string.IsNullOrWhiteSpace(manifest.InteractivePetScene)
            ? null
            : ResourceLoader.Load<PackedScene>(manifest.InteractivePetScene);
        var visualSlots = new Dictionary<string, PackedScene>(StringComparer.Ordinal);
        foreach ((string slotId, string path) in manifest.VisualSlots)
        {
            PackedScene? scene = ResourceLoader.Load<PackedScene>(path);
            if (scene is null)
            {
                return new ThemeDlcLoadResult(ThemeDlcLoadStatus.ResourceTypeMismatch);
            }

            visualSlots.Add(slotId, scene);
        }

        var screenScenes = new Dictionary<ScreenId, PackedScene>();
        var screenBindingContracts = new Dictionary<ScreenId, ThemeScreenBindingContract>();
        bool? selfContainedScreenVisuals = null;
        foreach ((string screenBindingId, string path) in manifest.ScreenScenes)
        {
            if (!ThemeSceneContract.TryGetByBindingId(screenBindingId, out ThemeScreenDescriptor descriptor))
            {
                return new ThemeDlcLoadResult(
                    ThemeDlcLoadStatus.ManifestInvalid,
                    ManifestFailure: ThemeDlcManifestFailure.InvalidScreenScene);
            }

            PackedScene? scene = ResourceLoader.Load<PackedScene>(path);
            ThemeScreenBindingContract? screenBindingContract =
                bindingContract?.GetScreen(descriptor.BindingId);
            ThemeScreenSceneFailure sceneFailure = ThemeSceneContract.ValidateScene(
                scene,
                descriptor,
                screenBindingContract,
                out bool sceneSelfContainedVisuals);
            if (scene is null || sceneFailure != ThemeScreenSceneFailure.None)
            {
                return new ThemeDlcLoadResult(
                    ThemeDlcLoadStatus.ResourceTypeMismatch,
                    ScreenSceneFailure: sceneFailure,
                    ScreenBindingId: descriptor.BindingId);
            }

            if (selfContainedScreenVisuals is not null
                && selfContainedScreenVisuals.Value != sceneSelfContainedVisuals)
            {
                return new ThemeDlcLoadResult(ThemeDlcLoadStatus.IncompatibleVisualComposition);
            }

            selfContainedScreenVisuals ??= sceneSelfContainedVisuals;

            screenScenes.Add(descriptor.ScreenId, scene);
            if (screenBindingContract is not null)
            {
                screenBindingContracts.Add(descriptor.ScreenId, screenBindingContract);
            }
        }

        bool usesSelfContainedScreenVisuals = selfContainedScreenVisuals == true;
        if (!ThemeSceneContract.IsCompatibleVisualComposition(
                usesSelfContainedScreenVisuals,
                manifest.BackgroundTexturePath,
                manifest.BackgroundDecorationPath))
        {
            return new ThemeDlcLoadResult(ThemeDlcLoadStatus.IncompatibleVisualComposition);
        }
        if (uiTheme is null
            || (!string.IsNullOrWhiteSpace(manifest.BackgroundTexturePath) && background is null)
            || (!string.IsNullOrWhiteSpace(manifest.BackgroundDecorationPath) && decoration is null)
            || (!string.IsNullOrWhiteSpace(manifest.InteractivePetScene) && interactivePet is null)
            || (interactivePet is not null && !InteractivePetContract.IsCompatible(interactivePet)))
        {
            return new ThemeDlcLoadResult(ThemeDlcLoadStatus.ResourceTypeMismatch);
        }

        var definition = new VendingThemeDefinition
        {
            ThemeId = manifest.ThemeId.Trim(),
            DisplayName = manifest.DisplayName.Trim(),
            UiTheme = uiTheme,
            BackgroundTexture = background,
            BackgroundDecoration = decoration,
            InteractivePetScene = interactivePet,
            BackgroundDecorationScale = manifest.BackgroundDecorationScale,
            FallbackColor = Color.FromHtml(manifest.FallbackColor),
            UseNearestTextureFilter = manifest.UseNearestTextureFilter,
            VisualSlots = visualSlots,
            ScreenScenes = screenScenes,
            ScreenBindingContracts = screenBindingContracts,
            SelfContainedScreenVisuals = usesSelfContainedScreenVisuals,
        };
        return new ThemeDlcLoadResult(ThemeDlcLoadStatus.Loaded, definition);
    }

    private static string? ResolveExternalPath(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath)
            || configuredPath.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            string trimmed = configuredPath.Trim();
            if (trimmed.StartsWith("user://", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(ProjectSettings.GlobalizePath(trimmed));
            }

            return Path.IsPathFullyQualified(trimmed)
                ? Path.GetFullPath(trimmed)
                : null;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
