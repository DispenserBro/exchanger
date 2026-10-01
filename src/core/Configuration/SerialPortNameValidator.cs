using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Exchanger.Core.Configuration;

public enum SerialPortPlatform
{
    Unsupported,
    Windows,
    Linux,
}

/// <summary>Проверяет имя последовательного порта по правилам текущей desktop-платформы.</summary>
public static partial class SerialPortNameValidator
{
    private const string LinuxByIdPrefix = "/dev/serial/by-id/";
    private const int MaximumLinuxByIdNameLength = 255;

    public static SerialPortPlatform CurrentPlatform =>
        OperatingSystem.IsWindows()
            ? SerialPortPlatform.Windows
            : OperatingSystem.IsLinux()
                ? SerialPortPlatform.Linux
                : SerialPortPlatform.Unsupported;

    public static bool IsValid(string? portName) => IsValid(portName, CurrentPlatform);

    public static bool IsValid(string? portName, SerialPortPlatform platform)
    {
        string normalized = portName?.Trim() ?? string.Empty;
        return platform switch
        {
            SerialPortPlatform.Windows => normalized.Equals("COM", StringComparison.OrdinalIgnoreCase)
                || WindowsComPortRegex().IsMatch(normalized),
            SerialPortPlatform.Linux => IsValidLinuxPort(normalized),
            _ => false,
        };
    }

    public static string GetExpectedFormatMessage(SerialPortPlatform platform) =>
        platform switch
        {
            SerialPortPlatform.Windows => "Укажите COM1…COM256 либо COM для автоматического поиска.",
            SerialPortPlatform.Linux =>
                "Укажите последовательный порт в формате /dev/ttyUSB*, /dev/ttyACM* "
                + "или /dev/serial/by-id/*.",
            _ => "Последовательные порты поддерживаются только в Windows и Linux.",
        };

    private static bool IsValidLinuxPort(string portName)
    {
        if (portName.Equals("ttyUSB", StringComparison.OrdinalIgnoreCase)
            || portName.Equals("/dev/ttyUSB", StringComparison.OrdinalIgnoreCase)
            || LinuxTtyPortRegex().IsMatch(portName)
            || LinuxShortTtyUsbPortRegex().IsMatch(portName))
        {
            return true;
        }

        if (!portName.StartsWith(LinuxByIdPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string deviceId = portName[LinuxByIdPrefix.Length..];
        return deviceId.Length is > 0 and <= MaximumLinuxByIdNameLength
               && deviceId is not "." and not ".."
               && !deviceId.Any(character =>
                   character is '/' or '\\' || char.IsControl(character));
    }
    [GeneratedRegex(
        "^COM(?:[1-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-6])$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WindowsComPortRegex();

    [GeneratedRegex("^ttyUSB[0-9]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LinuxShortTtyUsbPortRegex();

    [GeneratedRegex("^/dev/tty(?:USB|ACM)[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex LinuxTtyPortRegex();
}
