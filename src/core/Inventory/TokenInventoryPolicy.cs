using Exchanger.Core.Session;
using Exchanger.Hardware.Abstractions;
using System;

namespace Exchanger.Core.Inventory;

public enum TokenHopper
{
    Hopper1 = 1,
    Hopper2 = 2,
}

public enum TokenAvailabilityLevel
{
    Unknown,
    Empty,
    Low,
    Enough,
    Much,
}

/// <summary>Единые правила показа общего запаса двух хопперов.</summary>
public static class TokenInventoryPolicy
{
    public static TokenAvailabilityLevel ResolveAvailability(
        int totalCount,
        MachineStockLevel hopper1,
        MachineStockLevel hopper2)
    {
        if (totalCount <= 0)
        {
            return TokenAvailabilityLevel.Empty;
        }

        bool bothEnough = hopper1 == MachineStockLevel.Enough
                          && hopper2 == MachineStockLevel.Enough;
        if (bothEnough)
        {
            return TokenAvailabilityLevel.Much;
        }

        bool bothLow = hopper1 == MachineStockLevel.Low
                       && hopper2 == MachineStockLevel.Low;
        if (totalCount < 100 || bothLow)
        {
            return TokenAvailabilityLevel.Low;
        }

        if (totalCount >= 100
            || hopper1 == MachineStockLevel.Enough
            || hopper2 == MachineStockLevel.Enough)
        {
            return TokenAvailabilityLevel.Enough;
        }

        return TokenAvailabilityLevel.Low;
    }

    public static TokenAvailabilityLevel ResolveWithoutInventory(
        MachineStockLevel hopper1,
        MachineStockLevel hopper2,
        bool hopper1Enabled,
        bool hopper2Enabled)
    {
        if (!hopper1Enabled && !hopper2Enabled)
        {
            return TokenAvailabilityLevel.Unknown;
        }

        bool anyEnough = hopper1Enabled && hopper1 == MachineStockLevel.Enough
                         || hopper2Enabled && hopper2 == MachineStockLevel.Enough;
        if (anyEnough)
        {
            return TokenAvailabilityLevel.Enough;
        }

        bool anyLow = hopper1Enabled && hopper1 == MachineStockLevel.Low
                      || hopper2Enabled && hopper2 == MachineStockLevel.Low;
        return anyLow ? TokenAvailabilityLevel.Low : TokenAvailabilityLevel.Unknown;
    }

    public static string FormatApproximateCount(int totalCount)
    {
        int count = Math.Max(0, totalCount);
        if (count == 0)
        {
            return "нет жетонов";
        }

        if (count < 100)
        {
            return "<100";
        }

        int step = count > 1000 ? 500 : 100;
        int lowerBound = count / step * step;
        return count % step == 0
            ? $"~{count}"
            : $">{lowerBound}";
    }

    public static string FormatExactCount(int totalCount)
        => TokenCountFormatter.Format(totalCount);

    public static string FormatHopperExactCount(int hopperNumber, int count)
    {
        if (hopperNumber is not 1 and not 2)
        {
            throw new ArgumentOutOfRangeException(nameof(hopperNumber));
        }

        return $"ХОППЕР {hopperNumber} — ОСТАТОК: {TokenCountFormatter.FormatUpper(count)}";
    }

    public static string FormatAvailability(TokenAvailabilityLevel level) => level switch
    {
        TokenAvailabilityLevel.Empty => "НЕТ ЖЕТОНОВ",
        TokenAvailabilityLevel.Low => "МАЛО",
        TokenAvailabilityLevel.Enough => "ДОСТАТОЧНО",
        TokenAvailabilityLevel.Much => "МНОГО",
        _ => "НЕТ ДАННЫХ",
    };
}
