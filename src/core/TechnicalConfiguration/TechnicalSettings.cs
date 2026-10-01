using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using Exchanger.Core.Configuration;

namespace Exchanger.Core.TechnicalConfiguration;

/// <summary>
/// Монтажные параметры автомата. Они загружаются только из отдельного TOML-файла
/// и намеренно не входят в интерфейс настроек владельца.
/// </summary>
public sealed class TechnicalSettings
{
    public const int CurrentVersion = 1;

    public required int Version { get; init; }

    public required ControllerTechnicalSettings Controller { get; init; }

    public required HopperTechnicalSettings Hopper1 { get; init; }

    public required HopperTechnicalSettings Hopper2 { get; init; }

    public required ServiceTechnicalSettings Service { get; init; }

    public required PollingTechnicalSettings Polling { get; init; }

    public ThemeTechnicalSettings? Theme { get; init; }

    public void ApplyTo(HardwareSettings hardware)
    {
        ArgumentNullException.ThrowIfNull(hardware);

        hardware.UseMock = Controller.UseMock;
        hardware.PortName = Controller.Port;
        hardware.BaudRate = Controller.BaudRate;
        hardware.ReconnectIntervalSeconds = Controller.ReconnectIntervalSeconds;
        hardware.HandshakeTimeoutMilliseconds = Controller.HandshakeTimeoutMilliseconds;
        hardware.FrameTimeoutMilliseconds = Controller.FrameTimeoutMilliseconds;
        hardware.MaximumFrameLength = Controller.MaximumFrameLength;
        hardware.CashlessKeepAliveSeconds = Controller.CashlessKeepAliveSeconds;

        hardware.ServiceButtonInputIndex = Service.ButtonInput;
        hardware.ServiceButtonActiveHigh = Service.ButtonActiveHigh;
        hardware.StockSensorInputIndex = Hopper1.Enabled ? Hopper1.StockSensorInput : -1;
        hardware.StockLowWhenInputHigh = Hopper1.StockLowActiveHigh;
        hardware.Hopper1DispenseSensorInputIndex = Hopper1.Enabled ? Hopper1.DispenseSensorInput : -1;
        hardware.Hopper1DispenseSensorActiveHigh = Hopper1.DispensePulseActiveHigh;
        hardware.Hopper2StockSensorInputIndex = Hopper2.Enabled ? Hopper2.StockSensorInput : -1;
        hardware.Hopper2StockLowWhenInputHigh = Hopper2.StockLowActiveHigh;
        hardware.Hopper2DispenseSensorInputIndex = Hopper2.Enabled ? Hopper2.DispenseSensorInput : -1;
        hardware.Hopper2DispenseSensorActiveHigh = Hopper2.DispensePulseActiveHigh;
        hardware.InputPollMilliseconds = Polling.InputPollMilliseconds;
        hardware.StockPollSeconds = Polling.StockPollSeconds;
        hardware.DispenseMode = ResolveDispenseMode(Hopper1.Enabled, Hopper2.Enabled);
    }

    public void ApplyTo(ThemeSettings themes)
    {
        ArgumentNullException.ThrowIfNull(themes);
        if (Theme is null)
        {
            return;
        }

        themes.ExternalDlcEnabled = Theme.Enabled;
        themes.ExternalDlcPath = Theme.BuildPackagePath();
    }

    public static string ResolveDispenseMode(bool hopper1Enabled, bool hopper2Enabled) =>
        (hopper1Enabled, hopper2Enabled) switch
        {
            (true, true) => "DualHopperAuto",
            (true, false) => "Hopper1Auto",
            (false, true) => "Hopper2Auto",
            _ => "Unsupported",
        };

    public static (bool Hopper1Enabled, bool Hopper2Enabled) ResolveConfiguredHoppers(
        string? dispenseMode) =>
        dispenseMode?.Trim() switch
        {
            string mode when mode.Equals("DualHopperAuto", StringComparison.OrdinalIgnoreCase) => (true, true),
            string mode when mode.Equals("Hopper1Auto", StringComparison.OrdinalIgnoreCase) => (true, false),
            string mode when mode.Equals("Hopper2Auto", StringComparison.OrdinalIgnoreCase) => (false, true),
            _ => (false, false),
        };

    public IReadOnlyList<string> Validate() =>
        Validate(SerialPortNameValidator.CurrentPlatform);

    public IReadOnlyList<string> Validate(SerialPortPlatform serialPortPlatform)
    {
        var issues = new List<string>();

        if (Version != CurrentVersion)
        {
            issues.Add($"Поддерживается version = {CurrentVersion}, получено: {Version}.");
        }

        if (!Controller.UseMock
            && !SerialPortNameValidator.IsValid(Controller.Port, serialPortPlatform))
        {
            issues.Add(
                $"controller.port: {SerialPortNameValidator.GetExpectedFormatMessage(serialPortPlatform)}");
        }

        if (!HardwareSettingsValidator.AllowedBaudRates.Contains(Controller.BaudRate))
        {
            issues.Add("controller.baud_rate содержит неподдерживаемую скорость порта.");
        }

        ValidateRange(issues, "controller.reconnect_interval_seconds", Controller.ReconnectIntervalSeconds, 1, 60);
        ValidateRange(issues, "controller.handshake_timeout_ms", Controller.HandshakeTimeoutMilliseconds, 1000, 60000);
        ValidateRange(issues, "controller.frame_timeout_ms", Controller.FrameTimeoutMilliseconds, 10, 5000);
        ValidateRange(issues, "controller.max_frame_length", Controller.MaximumFrameLength, 2, 65536);
        ValidateRange(issues, "controller.cashless_keep_alive_seconds", Controller.CashlessKeepAliveSeconds, 30, 170);
        ValidateInput(issues, "hopper_1.stock_sensor_input", Hopper1.StockSensorInput);
        ValidateInput(issues, "hopper_1.dispense_sensor_input", Hopper1.DispenseSensorInput);
        ValidateInput(issues, "hopper_2.stock_sensor_input", Hopper2.StockSensorInput);
        ValidateInput(issues, "hopper_2.dispense_sensor_input", Hopper2.DispenseSensorInput);
        ValidateInput(issues, "service.button_input", Service.ButtonInput);
        ValidateRange(issues, "polling.input_poll_ms", Polling.InputPollMilliseconds, 50, 1000);
        ValidateRange(issues, "polling.stock_poll_seconds", Polling.StockPollSeconds, 1, 60);
        if (Theme is not null)
        {
            ValidateTheme(issues, Theme);
        }

        var assignedInputs = new Dictionary<int, string>();
        AddUniqueInput(issues, assignedInputs, Service.ButtonInput, "service.button_input");
        if (Hopper1.Enabled)
        {
            AddUniqueInput(issues, assignedInputs, Hopper1.StockSensorInput, "hopper_1.stock_sensor_input");
            AddUniqueInput(issues, assignedInputs, Hopper1.DispenseSensorInput, "hopper_1.dispense_sensor_input");
        }

        if (Hopper2.Enabled)
        {
            AddUniqueInput(issues, assignedInputs, Hopper2.StockSensorInput, "hopper_2.stock_sensor_input");
            AddUniqueInput(issues, assignedInputs, Hopper2.DispenseSensorInput, "hopper_2.dispense_sensor_input");
        }

        return issues;
    }

    private static void ValidateTheme(List<string> issues, ThemeTechnicalSettings theme)
    {
        string directory = theme.Directory.Trim();
        if (directory.Length == 0
            || directory.Length > ThemeSettingsValidator.MaximumDlcPathLength
            || directory.Any(char.IsControl))
        {
            issues.Add("theme.directory содержит недопустимый или слишком длинный путь.");
        }
        else
        {
            bool userPath = directory.StartsWith("user://", StringComparison.OrdinalIgnoreCase);
            if (directory.StartsWith("res://", StringComparison.OrdinalIgnoreCase)
                || (!userPath && !Path.IsPathFullyQualified(directory)))
            {
                issues.Add("theme.directory должен быть путём user:// или абсолютным локальным путём.");
            }

            string normalized = directory.Replace('\\', '/');
            if (normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
            {
                issues.Add("Переходы «..» в theme.directory запрещены.");
            }
        }

        string fileName = theme.FileName.Trim();
        if (fileName.Length == 0
            || fileName.Length > 128
            || fileName.Any(char.IsControl)
            || fileName is "." or ".."
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || fileName.IndexOfAny(new[] { '/', '\\', ':' }) >= 0)
        {
            issues.Add("theme.file_name содержит недопустимое имя файла.");
        }
        else if (Path.HasExtension(fileName))
        {
            issues.Add("theme.file_name задаётся без расширения .pck.");
        }

        if (directory.Length > 0
            && fileName.Length > 0
            && theme.BuildPackagePath().Length > ThemeSettingsValidator.MaximumDlcPathLength)
        {
            issues.Add($"Полный путь theme.directory + theme.file_name не должен превышать {ThemeSettingsValidator.MaximumDlcPathLength} символов.");
        }
    }

    private static void ValidateRange(List<string> issues, string name, int value, int minimum, int maximum)
    {
        if (value < minimum || value > maximum)
        {
            issues.Add($"{name} должен находиться в диапазоне {minimum}…{maximum}.");
        }
    }

    private static void ValidateInput(List<string> issues, string name, int value)
    {
        if (value is < -1 or > 25)
        {
            issues.Add($"{name} должен находиться в диапазоне -1…25.");
        }
    }

    private static void AddUniqueInput(
        List<string> issues,
        IDictionary<int, string> assignedInputs,
        int input,
        string name)
    {
        if (input < 0)
        {
            return;
        }

        if (assignedInputs.TryGetValue(input, out string? existing))
        {
            issues.Add($"IN{input} одновременно назначен параметрам {existing} и {name}.");
            return;
        }

        assignedInputs.Add(input, name);
    }
}

public sealed record ControllerTechnicalSettings(
    bool UseMock,
    string Port,
    int BaudRate,
    int ReconnectIntervalSeconds,
    int HandshakeTimeoutMilliseconds,
    int FrameTimeoutMilliseconds,
    int MaximumFrameLength,
    int CashlessKeepAliveSeconds);

public sealed record HopperTechnicalSettings(
    bool Enabled,
    int StockSensorInput,
    bool StockLowActiveHigh,
    int DispenseSensorInput,
    bool DispensePulseActiveHigh);

public sealed record ServiceTechnicalSettings(bool ButtonActiveHigh, int ButtonInput);

public sealed record PollingTechnicalSettings(int InputPollMilliseconds, int StockPollSeconds);

public sealed record ThemeTechnicalSettings(bool Enabled, string Directory, string FileName)
{
    public string BuildPackagePath()
    {
        string directory = Directory.Trim();
        string packageFileName = $"{FileName.Trim()}.pck";
        if (directory.StartsWith("user://", StringComparison.OrdinalIgnoreCase))
        {
            if (!directory.Equals("user://", StringComparison.OrdinalIgnoreCase))
            {
                directory = directory.TrimEnd('/', '\\');
            }

            string separator = directory.EndsWith('/') ? string.Empty : "/";
            return $"{directory}{separator}{packageFileName}";
        }

        return Path.Combine(directory, packageFileName);
    }
}

internal sealed class TechnicalSettingsDocument
{
    [JsonPropertyName("version")]
    public int? Version { get; set; }

    [JsonPropertyName("controller")]
    public ControllerTechnicalSettingsDocument? Controller { get; set; }

    [JsonPropertyName("hopper_1")]
    public HopperTechnicalSettingsDocument? Hopper1 { get; set; }

    [JsonPropertyName("hopper_2")]
    public HopperTechnicalSettingsDocument? Hopper2 { get; set; }

    [JsonPropertyName("service")]
    public ServiceTechnicalSettingsDocument? Service { get; set; }

    [JsonPropertyName("polling")]
    public PollingTechnicalSettingsDocument? Polling { get; set; }

    [JsonPropertyName("theme")]
    public ThemeTechnicalSettingsDocument? Theme { get; set; }
}

internal sealed class ThemeTechnicalSettingsDocument
{
    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; }

    [JsonPropertyName("directory")]
    public string? Directory { get; set; }

    [JsonPropertyName("file_name")]
    public string? FileName { get; set; }
}

internal sealed class ControllerTechnicalSettingsDocument
{
    [JsonPropertyName("use_mock")]
    public bool? UseMock { get; set; }

    [JsonPropertyName("port")]
    public string? Port { get; set; }

    [JsonPropertyName("baud_rate")]
    public int? BaudRate { get; set; }

    [JsonPropertyName("reconnect_interval_seconds")]
    public int? ReconnectIntervalSeconds { get; set; }

    [JsonPropertyName("handshake_timeout_ms")]
    public int? HandshakeTimeoutMilliseconds { get; set; }

    [JsonPropertyName("frame_timeout_ms")]
    public int? FrameTimeoutMilliseconds { get; set; }

    [JsonPropertyName("max_frame_length")]
    public int? MaximumFrameLength { get; set; }

    [JsonPropertyName("cashless_keep_alive_seconds")]
    public int? CashlessKeepAliveSeconds { get; set; }
}

internal sealed class HopperTechnicalSettingsDocument
{
    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; }

    [JsonPropertyName("stock_sensor_input")]
    public int? StockSensorInput { get; set; }

    [JsonPropertyName("stock_low_active_high")]
    public bool? StockLowActiveHigh { get; set; }

    [JsonPropertyName("dispense_sensor_input")]
    public int? DispenseSensorInput { get; set; }

    [JsonPropertyName("dispense_pulse_active_high")]
    public bool? DispensePulseActiveHigh { get; set; }
}

internal sealed class ServiceTechnicalSettingsDocument
{
    [JsonPropertyName("button_input")]
    public int? ButtonInput { get; set; }

    [JsonPropertyName("button_active_high")]
    public bool? ButtonActiveHigh { get; set; }
}

internal sealed class PollingTechnicalSettingsDocument
{
    [JsonPropertyName("input_poll_ms")]
    public int? InputPollMilliseconds { get; set; }

    [JsonPropertyName("stock_poll_seconds")]
    public int? StockPollSeconds { get; set; }
}
