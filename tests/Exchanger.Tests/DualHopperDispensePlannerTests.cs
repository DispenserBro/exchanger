using Exchanger.Hardware.Abstractions;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class DualHopperDispensePlannerTests
{
    [TestCase(10, 20, 30, 10, 0)]
    [TestCase(10, 10, 30, 10, 0)]
    [TestCase(10, 6, 30, 6, 4)]
    [TestCase(10, 0, 30, 0, 10)]
    [TestCase(10, -5, 30, 0, 10)]
    public void TryCreate_UsesFirstHopperBeforeSecond(
        int requested,
        int hopper1Inventory,
        int hopper2Inventory,
        int expectedFromHopper1,
        int expectedFromHopper2)
    {
        bool created = DualHopperDispensePlanner.TryCreate(
            requested,
            hopper1Inventory,
            hopper2Inventory,
            out DualHopperDispensePlan plan);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created, Is.True);
            Assert.That(plan.Hopper1Count, Is.EqualTo(expectedFromHopper1));
            Assert.That(plan.Hopper2Count, Is.EqualTo(expectedFromHopper2));
            Assert.That(plan.TotalCount, Is.EqualTo(requested));
        }
    }

    [TestCase(0, 10, 10)]
    [TestCase(-1, 10, 10)]
    [TestCase(10, 3, 6)]
    [TestCase(10, -1, 9)]
    public void TryCreate_RejectsInvalidOrInsufficientInventory(
        int requested,
        int hopper1Inventory,
        int hopper2Inventory)
    {
        bool created = DualHopperDispensePlanner.TryCreate(
            requested,
            hopper1Inventory,
            hopper2Inventory,
            out DualHopperDispensePlan plan);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created, Is.False);
            Assert.That(plan, Is.EqualTo(default(DualHopperDispensePlan)));
        }
    }
}
