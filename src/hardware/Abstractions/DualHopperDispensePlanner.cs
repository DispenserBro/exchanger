using System;

namespace Exchanger.Hardware.Abstractions;

public readonly record struct DualHopperDispensePlan(int Hopper1Count, int Hopper2Count)
{
    public int TotalCount => Hopper1Count + Hopper2Count;
}

public static class DualHopperDispensePlanner
{
    public static bool TryCreate(
        int requestedCount,
        int hopper1Inventory,
        int hopper2Inventory,
        out DualHopperDispensePlan plan)
    {
        plan = default;
        if (requestedCount <= 0)
        {
            return false;
        }

        int availableInHopper1 = Math.Max(0, hopper1Inventory);
        int availableInHopper2 = Math.Max(0, hopper2Inventory);
        if ((long)availableInHopper1 + availableInHopper2 < requestedCount)
        {
            return false;
        }

        int fromHopper1 = Math.Min(requestedCount, availableInHopper1);
        int fromHopper2 = requestedCount - fromHopper1;
        plan = new DualHopperDispensePlan(fromHopper1, fromHopper2);
        return true;
    }
}
