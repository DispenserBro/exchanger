using System;
using System.Collections.Generic;

namespace Exchanger.Core.Configuration;

public enum HardwareValidationField
{
    PortName,
    BaudRate,
    ReconnectIntervalSeconds,
}

public readonly record struct HardwareValidationIssue(HardwareValidationField Field, string Message);

public sealed class HardwareValidationResult
{
    public HardwareValidationResult(IReadOnlyList<HardwareValidationIssue> issues) => Issues = issues;

    public IReadOnlyList<HardwareValidationIssue> Issues { get; }

    public bool IsValid => Issues.Count == 0;
}

/// <summary>Проверяет только разрешённую UI-поверхность соединения, не затрагивая AES и wire-параметры.</summary>
public static class HardwareSettingsValidator
{
    public static readonly int[] AllowedBaudRates = [9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600];

    public static HardwareValidationResult Validate(HardwareSettings settings) =>
        Validate(settings, SerialPortNameValidator.CurrentPlatform);

    public static HardwareValidationResult Validate(
        HardwareSettings settings,
        SerialPortPlatform serialPortPlatform)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var issues = new List<HardwareValidationIssue>();

        string portName = settings.PortName?.Trim() ?? string.Empty;
        if (!settings.UseMock && !SerialPortNameValidator.IsValid(portName, serialPortPlatform))
        {
            issues.Add(new HardwareValidationIssue(
                HardwareValidationField.PortName,
                SerialPortNameValidator.GetExpectedFormatMessage(serialPortPlatform)));
        }

        if (Array.IndexOf(AllowedBaudRates, settings.BaudRate) < 0)
        {
            issues.Add(new HardwareValidationIssue(
                HardwareValidationField.BaudRate,
                "Выберите поддерживаемую скорость соединения."));
        }

        if (settings.ReconnectIntervalSeconds is < 1 or > 60)
        {
            issues.Add(new HardwareValidationIssue(
                HardwareValidationField.ReconnectIntervalSeconds,
                "Повторное подключение должно выполняться через 1–60 секунд."));
        }

        return new HardwareValidationResult(issues);
    }
}
