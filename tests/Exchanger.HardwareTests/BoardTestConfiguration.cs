using Exchanger.Hardware.Abstractions;
using Exchanger.Hardware.PeripheryController;
using NUnit.Framework;

namespace Exchanger.HardwareTests;

internal sealed record BoardTestConfiguration(
    string PortName,
    int BaudRate,
    TimeSpan AuthorizationTimeout,
    TimeSpan ResponseTimeout,
    TimeSpan InteractionTimeout,
    int ReconnectCycles,
    int StockSensorInputIndex,
    bool StockLowWhenInputHigh)
{
    public const string MasterEnableVariable = "EXCHANGER_BOARD_TESTS";
    public const string PortVariable = "EXCHANGER_BOARD_PORT";

    public static BoardTestConfiguration RequireSafeTests()
    {
        if (!"1".Equals(Environment.GetEnvironmentVariable(MasterEnableVariable), StringComparison.Ordinal))
        {
            Assert.Ignore($"Аппаратные тесты отключены. Установите {MasterEnableVariable}=1 и задайте {PortVariable}.");
        }

        string portName = Environment.GetEnvironmentVariable(PortVariable)?.Trim() ?? string.Empty;
        Assert.That(portName, Is.Not.Empty, $"Не задана переменная {PortVariable} (например, COM5).");

        return new BoardTestConfiguration(
            portName,
            ReadInt("EXCHANGER_BOARD_BAUD", 115200, 1200, 2_000_000),
            TimeSpan.FromSeconds(ReadInt("EXCHANGER_BOARD_AUTH_TIMEOUT_SECONDS", 10, 3, 60)),
            TimeSpan.FromSeconds(ReadInt("EXCHANGER_BOARD_RESPONSE_TIMEOUT_SECONDS", 5, 1, 60)),
            TimeSpan.FromSeconds(ReadInt("EXCHANGER_BOARD_INTERACTION_TIMEOUT_SECONDS", 60, 10, 600)),
            ReadInt("EXCHANGER_BOARD_RECONNECT_CYCLES", 3, 1, 20),
            ReadInt("EXCHANGER_BOARD_STOCK_INPUT", PeripheryControllerInputs.Hopper2StockLow, -1, 25),
            ReadBool("EXCHANGER_BOARD_STOCK_LOW_WHEN_HIGH", false));
    }

    public static void RequireConfirmation(string variable, string expectedValue, string description)
    {
        string actual = Environment.GetEnvironmentVariable(variable)?.Trim() ?? string.Empty;
        if (!actual.Equals(expectedValue, StringComparison.Ordinal))
        {
            Assert.Ignore($"{description} Для запуска явно установите {variable}={expectedValue}.");
        }
    }

    public static int RequirePositiveInt(string variable, int maximum)
    {
        string raw = Environment.GetEnvironmentVariable(variable)?.Trim() ?? string.Empty;
        Assert.That(int.TryParse(raw, out int value), Is.True, $"Переменная {variable} должна содержать целое число.");
        Assert.That(value, Is.InRange(1, maximum), $"Переменная {variable} должна быть в диапазоне 1..{maximum}.");
        return value;
    }

    public static int RequireOptionalPositiveInt(string variable, int maximum, string skippedCapability)
    {
        string raw = Environment.GetEnvironmentVariable(variable)?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(raw))
        {
            Assert.Ignore($"{skippedCapability} не проверяется: переменная {variable} не задана.");
        }

        return RequirePositiveInt(variable, maximum);
    }

    public static MachineStockLevel RequireExpectedStockLevel()
    {
        string raw = Environment.GetEnvironmentVariable("EXCHANGER_BOARD_EXPECT_STOCK")?.Trim() ?? string.Empty;
        Assert.That(
            Enum.TryParse(raw, ignoreCase: true, out MachineStockLevel level)
            && level is MachineStockLevel.Enough or MachineStockLevel.Low,
            Is.True,
            "EXCHANGER_BOARD_EXPECT_STOCK должен быть Enough или Low.");
        return level;
    }

    public static MachineStockLevel RequireOptionalExpectedStockLevel()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EXCHANGER_BOARD_EXPECT_STOCK")))
        {
            Assert.Ignore("Датчик запаса не проверяется: EXCHANGER_BOARD_EXPECT_STOCK не задан.");
        }

        return RequireExpectedStockLevel();
    }

    private static int ReadInt(string variable, int fallback, int minimum, int maximum)
    {
        string? raw = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        Assert.That(int.TryParse(raw, out int value), Is.True, $"Переменная {variable} должна содержать целое число.");
        Assert.That(value, Is.InRange(minimum, maximum), $"Переменная {variable} вне диапазона {minimum}..{maximum}.");
        return value;
    }

    private static bool ReadBool(string variable, bool fallback)
    {
        string? raw = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        Assert.That(bool.TryParse(raw, out bool value), Is.True, $"Переменная {variable} должна быть true или false.");
        return value;
    }
}
