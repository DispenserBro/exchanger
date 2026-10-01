using System;
using System.Collections.Generic;
using System.IO;
using Tomlyn;

namespace Exchanger.Core.TechnicalConfiguration;

/// <summary>
/// Создаёт пользовательскую копию технического TOML и загружает её целиком.
/// Повреждённый пользовательский файл не применяется частично: используется
/// безопасный встроенный профиль, а исходный файл остаётся для диагностики.
/// </summary>
public sealed class TechnicalSettingsService
{
    private readonly string _defaultPath;
    private readonly string _userPath;
    private readonly Action<string>? _warningSink;

    public TechnicalSettingsService(string defaultPath, string userPath, Action<string>? warningSink = null)
    {
        _defaultPath = string.IsNullOrWhiteSpace(defaultPath)
            ? throw new ArgumentException("Не указан путь к встроенной технической конфигурации.", nameof(defaultPath))
            : defaultPath;
        _userPath = string.IsNullOrWhiteSpace(userPath)
            ? throw new ArgumentException("Не указан путь к пользовательской технической конфигурации.", nameof(userPath))
            : userPath;
        _warningSink = warningSink;
    }

    public string UserPath => _userPath;

    public TechnicalSettings Load()
    {
        EnsureUserCopy();

        if (File.Exists(_userPath))
        {
            if (TryLoadFile(_userPath, out TechnicalSettings? userSettings, out string userError))
            {
                return userSettings!;
            }

            _warningSink?.Invoke(
                $"Техническая конфигурация не применена: {userError} Используется встроенный безопасный профиль.");
        }

        if (TryLoadFile(_defaultPath, out TechnicalSettings? defaultSettings, out string defaultError))
        {
            return defaultSettings!;
        }

        throw new InvalidDataException($"Встроенная техническая конфигурация повреждена: {defaultError}");
    }

    public static bool TryParse(
        string content,
        out TechnicalSettings? settings,
        out IReadOnlyList<string> issues)
    {
        ArgumentNullException.ThrowIfNull(content);
        settings = null;
        var resultIssues = new List<string>();

        TechnicalSettingsDocument? document;
        try
        {
            document = TomlSerializer.Deserialize<TechnicalSettingsDocument>(content);
        }
        catch (Exception exception)
        {
            resultIssues.Add($"TOML не разобран: {exception.Message}");
            issues = resultIssues;
            return false;
        }

        if (document is null)
        {
            resultIssues.Add("TOML не содержит конфигурацию.");
            issues = resultIssues;
            return false;
        }

        TechnicalSettings? parsed = Build(document, resultIssues);
        if (parsed is not null)
        {
            resultIssues.AddRange(parsed.Validate());
        }

        if (resultIssues.Count > 0)
        {
            issues = resultIssues;
            return false;
        }

        settings = parsed;
        issues = resultIssues;
        return true;
    }

    private void EnsureUserCopy()
    {
        if (File.Exists(_userPath))
        {
            return;
        }

        try
        {
            string? directory = Path.GetDirectoryName(_userPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(_defaultPath, _userPath, overwrite: false);
        }
        catch (IOException) when (File.Exists(_userPath))
        {
            // Другой экземпляр успел создать тот же файл.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _warningSink?.Invoke(
                "Не удалось создать пользовательскую техническую конфигурацию: нет доступа к каталогу.");
        }
    }

    private static bool TryLoadFile(
        string path,
        out TechnicalSettings? settings,
        out string error)
    {
        try
        {
            string content = File.ReadAllText(path);
            if (TryParse(content, out settings, out IReadOnlyList<string> issues))
            {
                error = string.Empty;
                return true;
            }

            error = string.Join(" ", issues);
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            settings = null;
            error = "файл недоступен для чтения.";
            return false;
        }
    }

    private static TechnicalSettings? Build(TechnicalSettingsDocument document, List<string> issues)
    {
        ControllerTechnicalSettingsDocument? controller = RequireSection(document.Controller, "[controller]", issues);
        HopperTechnicalSettingsDocument? hopper1 = RequireSection(document.Hopper1, "[hopper_1]", issues);
        HopperTechnicalSettingsDocument? hopper2 = RequireSection(document.Hopper2, "[hopper_2]", issues);
        ServiceTechnicalSettingsDocument? service = RequireSection(document.Service, "[service]", issues);
        PollingTechnicalSettingsDocument? polling = RequireSection(document.Polling, "[polling]", issues);
        ThemeTechnicalSettings? parsedTheme = document.Theme is null
            ? null
            : BuildTheme(document.Theme, issues);

        int? version = RequireValue(document.Version, "version", issues);
        if (controller is null || hopper1 is null || hopper2 is null || service is null || polling is null)
        {
            return null;
        }

        bool? useMock = RequireValue(controller.UseMock, "controller.use_mock", issues);
        string? port = RequireText(controller.Port, "controller.port", issues);
        int? baudRate = RequireValue(controller.BaudRate, "controller.baud_rate", issues);
        int? reconnect = RequireValue(controller.ReconnectIntervalSeconds, "controller.reconnect_interval_seconds", issues);
        int? handshake = RequireValue(controller.HandshakeTimeoutMilliseconds, "controller.handshake_timeout_ms", issues);
        int? frameTimeout = RequireValue(controller.FrameTimeoutMilliseconds, "controller.frame_timeout_ms", issues);
        int? maxFrameLength = RequireValue(controller.MaximumFrameLength, "controller.max_frame_length", issues);
        int? keepAlive = RequireValue(controller.CashlessKeepAliveSeconds, "controller.cashless_keep_alive_seconds", issues);
        HopperTechnicalSettings? parsedHopper1 = BuildHopper(hopper1, "hopper_1", issues);
        HopperTechnicalSettings? parsedHopper2 = BuildHopper(hopper2, "hopper_2", issues);
        int? serviceInput = RequireValue(service.ButtonInput, "service.button_input", issues);
        bool? serviceActiveHigh = RequireValue(service.ButtonActiveHigh, "service.button_active_high", issues);
        int? inputPoll = RequireValue(polling.InputPollMilliseconds, "polling.input_poll_ms", issues);
        int? stockPoll = RequireValue(polling.StockPollSeconds, "polling.stock_poll_seconds", issues);

        if (issues.Count > 0)
        {
            return null;
        }

        return new TechnicalSettings
        {
            Version = version!.Value,
            Controller = new ControllerTechnicalSettings(
                useMock!.Value,
                port!.Trim(),
                baudRate!.Value,
                reconnect!.Value,
                handshake!.Value,
                frameTimeout!.Value,
                maxFrameLength!.Value,
                keepAlive!.Value),
            Hopper1 = parsedHopper1!,
            Hopper2 = parsedHopper2!,
            Service = new ServiceTechnicalSettings(serviceActiveHigh!.Value, serviceInput!.Value),
            Polling = new PollingTechnicalSettings(inputPoll!.Value, stockPoll!.Value),
            Theme = parsedTheme,
        };
    }

    private static ThemeTechnicalSettings? BuildTheme(
        ThemeTechnicalSettingsDocument document,
        List<string> issues)
    {
        bool? enabled = RequireValue(document.Enabled, "theme.enabled", issues);
        string? directory = RequireText(document.Directory, "theme.directory", issues);
        string? fileName = RequireText(document.FileName, "theme.file_name", issues);
        if (enabled is null || directory is null || fileName is null)
        {
            return null;
        }

        return new ThemeTechnicalSettings(enabled.Value, directory.Trim(), fileName.Trim());
    }

    private static HopperTechnicalSettings? BuildHopper(
        HopperTechnicalSettingsDocument document,
        string prefix,
        List<string> issues)
    {
        bool? enabled = RequireValue(document.Enabled, $"{prefix}.enabled", issues);
        int? stockInput = RequireValue(document.StockSensorInput, $"{prefix}.stock_sensor_input", issues);
        bool? stockActiveHigh = RequireValue(document.StockLowActiveHigh, $"{prefix}.stock_low_active_high", issues);
        int? dispenseInput = RequireValue(document.DispenseSensorInput, $"{prefix}.dispense_sensor_input", issues);
        bool? dispenseActiveHigh = RequireValue(document.DispensePulseActiveHigh, $"{prefix}.dispense_pulse_active_high", issues);
        if (enabled is null || stockInput is null || stockActiveHigh is null || dispenseInput is null || dispenseActiveHigh is null)
        {
            return null;
        }

        return new HopperTechnicalSettings(
            enabled.Value,
            stockInput.Value,
            stockActiveHigh.Value,
            dispenseInput.Value,
            dispenseActiveHigh.Value);
    }

    private static T? RequireValue<T>(T? value, string name, List<string> issues) where T : struct
    {
        if (value is null)
        {
            issues.Add($"Не задан параметр {name}.");
        }

        return value;
    }

    private static T? RequireSection<T>(T? value, string name, List<string> issues) where T : class
    {
        if (value is null)
        {
            issues.Add($"Не задан раздел {name}.");
        }

        return value;
    }

    private static string? RequireText(string? value, string name, List<string> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add($"Не задан параметр {name}.");
            return null;
        }

        return value;
    }
}
