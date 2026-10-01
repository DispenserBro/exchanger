using Godot;
using System;
using System.IO;
using System.Text;
using Exchanger.Core.TechnicalConfiguration;

namespace Exchanger.Core.Configuration;

/// <summary>
/// Загружает встроенную конфигурацию, создаёт пользовательскую копию и
/// применяет безопасные аргументы командной строки.
/// </summary>
public static class AppSettingsLoader
{
    private const string DefaultSettingsPath = "res://config/default_settings.json";
    private const string UserSettingsPath = "user://config/appsettings.json";
    private const string DefaultTechnicalSettingsPath = "res://config/technical_settings.toml";
    private const string UserTechnicalSettingsPath = "user://config/technical_settings.toml";
    private const string MaterializedDefaultSettingsPath = "user://config/.embedded/default_settings.json";
    private const string MaterializedDefaultTechnicalSettingsPath = "user://config/.embedded/technical_settings.toml";

    public static AppSettings Load()
    {
        return Load(CreateService());
    }

    public static SettingsService CreateService()
    {
        return new SettingsService(
            MaterializeEmbeddedResource(DefaultSettingsPath, MaterializedDefaultSettingsPath),
            ProjectSettings.GlobalizePath(UserSettingsPath),
            message => GD.PushWarning(message));
    }

    public static AppSettings Load(SettingsService service)
    {
        ArgumentNullException.ThrowIfNull(service);

        AppSettings settings = service.Load();
        TechnicalSettings technicalSettings = CreateTechnicalSettingsService().Load();
        technicalSettings.ApplyTo(settings.Hardware);
        technicalSettings.ApplyTo(settings.Themes);
        ApplyCommandLineOverrides(settings, OS.GetCmdlineUserArgs());
        settings.Normalize();
        return settings;
    }

    public static TechnicalSettingsService CreateTechnicalSettingsService()
    {
        return new TechnicalSettingsService(
            MaterializeEmbeddedResource(
                DefaultTechnicalSettingsPath,
                MaterializedDefaultTechnicalSettingsPath),
            ProjectSettings.GlobalizePath(UserTechnicalSettingsPath),
            message => GD.PushWarning(message));
    }

    private static string MaterializeEmbeddedResource(string resourcePath, string userCachePath)
    {
        using Godot.FileAccess? source = Godot.FileAccess.Open(
            resourcePath,
            Godot.FileAccess.ModeFlags.Read);
        if (source is null)
        {
            throw new InvalidDataException(
                $"Встроенный ресурс конфигурации недоступен: {resourcePath}.");
        }

        string targetPath = ProjectSettings.GlobalizePath(userCachePath);
        string? targetDirectory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        File.WriteAllText(targetPath, source.GetAsText(), new UTF8Encoding(false));
        return targetPath;
    }

    private static void ApplyCommandLineOverrides(AppSettings settings, string[] arguments)
    {
        foreach (string argument in arguments)
        {
            if (argument.Equals("--windowed", StringComparison.OrdinalIgnoreCase))
            {
                settings.Window.Fullscreen = false;
            }
            else if (argument.Equals("--fullscreen", StringComparison.OrdinalIgnoreCase))
            {
                settings.Window.Fullscreen = true;
            }
            else if (argument.Equals("--reduced-effects", StringComparison.OrdinalIgnoreCase))
            {
                settings.Themes.ReducedEffects = true;
            }
            else if (argument.StartsWith("--theme-dlc=", StringComparison.OrdinalIgnoreCase))
            {
                settings.Themes.ExternalDlcEnabled = true;
                settings.Themes.ExternalDlcPath = argument[12..];
            }
            else if (argument.Equals("--no-theme-dlc", StringComparison.OrdinalIgnoreCase))
            {
                settings.Themes.ExternalDlcEnabled = false;
            }
            else if (argument.StartsWith("--mock-hardware=", StringComparison.OrdinalIgnoreCase)
                     && bool.TryParse(argument[16..], out bool useMock))
            {
                settings.Hardware.UseMock = useMock;
            }
        }
    }
}
