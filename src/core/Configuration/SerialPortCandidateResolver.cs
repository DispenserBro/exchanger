using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Exchanger.Core.Configuration;

/// <summary>
/// Превращает технический префикс автоматического поиска в упорядоченный список
/// уже обнаруженных последовательных устройств. Сам порт не открывает.
/// </summary>
public static partial class SerialPortCandidateResolver
{
    /// <summary>
    /// Windows: <c>COM</c>. Linux: <c>ttyUSB</c> или <c>/dev/ttyUSB</c>.
    /// Полные имена остаются точной настройкой одного устройства.
    /// </summary>
    public static bool IsAutomaticSearchPrefix(string? configuredPort, SerialPortPlatform platform)
    {
        string normalized = configuredPort?.Trim() ?? string.Empty;
        return platform switch
        {
            SerialPortPlatform.Windows => normalized.Equals("COM", StringComparison.OrdinalIgnoreCase),
            SerialPortPlatform.Linux => normalized.Equals("ttyUSB", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("/dev/ttyUSB", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    /// <summary>
    /// Канонизирует короткую Linux-запись <c>ttyUSBN</c> в путь устройства
    /// <c>/dev/ttyUSBN</c>, который ожидает System.IO.Ports.
    /// </summary>
    public static string NormalizePortName(string? configuredPort, SerialPortPlatform platform)
    {
        string normalized = configuredPort?.Trim() ?? string.Empty;
        if (platform == SerialPortPlatform.Linux
            && LinuxTtyUsbCandidateRegex().Match(normalized) is { Success: true } match
            && long.TryParse(match.Groups["index"].Value, out long index))
        {
            return $"/dev/ttyUSB{index}";
        }

        return normalized;
    }
    /// <summary>
    /// Для обычной настройки возвращает один заданный порт. Для префикса поиска
    /// отбирает только подходящий тип устройств и сортирует их по номеру с нуля.
    /// </summary>
    public static IReadOnlyList<string> ResolveCandidates(
        string? configuredPort,
        SerialPortPlatform platform,
        IEnumerable<string>? availablePortNames)
    {
        string normalized = configuredPort?.Trim() ?? string.Empty;
        if (!IsAutomaticSearchPrefix(normalized, platform))
        {
            return normalized.Length == 0 ? Array.Empty<string>() : new[] { NormalizePortName(normalized, platform) };
        }

        return platform switch
        {
            SerialPortPlatform.Windows => ResolveWindowsCandidates(availablePortNames),
            SerialPortPlatform.Linux => ResolveLinuxTtyUsbCandidates(availablePortNames),
            _ => Array.Empty<string>(),
        };
    }

    private static IReadOnlyList<string> ResolveWindowsCandidates(IEnumerable<string>? availablePortNames) =>
        Resolve(availablePortNames, WindowsCandidateRegex(), candidate => $"COM{candidate.Index}");

    private static IReadOnlyList<string> ResolveLinuxTtyUsbCandidates(IEnumerable<string>? availablePortNames) =>
        Resolve(availablePortNames, LinuxTtyUsbCandidateRegex(), candidate => $"/dev/ttyUSB{candidate.Index}");

    private static IReadOnlyList<string> Resolve(
        IEnumerable<string>? availablePortNames,
        Regex pattern,
        Func<PortCandidate, string> format)
    {
        if (availablePortNames is null)
        {
            return Array.Empty<string>();
        }

        return availablePortNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => pattern.Match(name.Trim()))
            .Where(match => match.Success && long.TryParse(match.Groups["index"].Value, out _))
            .Select(match => new PortCandidate(long.Parse(match.Groups["index"].Value)))
            .Distinct()
            .OrderBy(candidate => candidate.Index)
            .Select(format)
            .ToArray();
    }

    [GeneratedRegex("^COM(?<index>[0-9]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WindowsCandidateRegex();

    [GeneratedRegex("^(?:/dev/)?ttyUSB(?<index>[0-9]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LinuxTtyUsbCandidateRegex();

    private readonly record struct PortCandidate(long Index);
}
