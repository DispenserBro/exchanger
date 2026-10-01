using Exchanger.Core.Inventory;
using Exchanger.Hardware.Abstractions;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class TokenInventoryPolicyTests
{
    [TestCase(0, "нет жетонов")]
    [TestCase(1, "<100")]
    [TestCase(99, "<100")]
    [TestCase(100, "~100")]
    [TestCase(199, ">100")]
    [TestCase(1000, "~1000")]
    [TestCase(1001, ">1000")]
    [TestCase(1500, "~1500")]
    [TestCase(1999, ">1500")]
    public void ApproximateCount_UsesRequiredBuckets(int count, string expected)
    {
        Assert.That(TokenInventoryPolicy.FormatApproximateCount(count), Is.EqualTo(expected));
    }

    [TestCase(0, "0 жетонов")]
    [TestCase(1, "1 жетон")]
    [TestCase(2, "2 жетона")]
    [TestCase(5, "5 жетонов")]
    [TestCase(11, "11 жетонов")]
    [TestCase(21, "21 жетон")]
    [TestCase(124, "124 жетона")]
    public void ExactCount_UsesReadableRussianForm(int count, string expected)
    {
        Assert.That(TokenInventoryPolicy.FormatExactCount(count), Is.EqualTo(expected));
    }

    [TestCase(1, 1, "ХОППЕР 1 — ОСТАТОК: 1 ЖЕТОН")]
    [TestCase(2, 24, "ХОППЕР 2 — ОСТАТОК: 24 ЖЕТОНА")]
    [TestCase(2, 111, "ХОППЕР 2 — ОСТАТОК: 111 ЖЕТОНОВ")]
    public void HopperExactCount_IdentifiesHopperAndUsesReadableRussianForm(
        int hopperNumber,
        int count,
        string expected)
    {
        Assert.That(TokenInventoryPolicy.FormatHopperExactCount(hopperNumber, count), Is.EqualTo(expected));
    }

    [TestCase(0, MachineStockLevel.Enough, MachineStockLevel.Enough, TokenAvailabilityLevel.Empty)]
    [TestCase(50, MachineStockLevel.Enough, MachineStockLevel.Low, TokenAvailabilityLevel.Low)]
    [TestCase(500, MachineStockLevel.Low, MachineStockLevel.Low, TokenAvailabilityLevel.Low)]
    [TestCase(100, MachineStockLevel.Low, MachineStockLevel.Unknown, TokenAvailabilityLevel.Enough)]
    [TestCase(500, MachineStockLevel.Enough, MachineStockLevel.Low, TokenAvailabilityLevel.Enough)]
    [TestCase(1, MachineStockLevel.Enough, MachineStockLevel.Enough, TokenAvailabilityLevel.Much)]
    public void Availability_CombinesAccountedCountAndBothSensors(
        int count,
        MachineStockLevel hopper1,
        MachineStockLevel hopper2,
        TokenAvailabilityLevel expected)
    {
        Assert.That(
            TokenInventoryPolicy.ResolveAvailability(count, hopper1, hopper2),
            Is.EqualTo(expected));
    }

    [TestCase(MachineStockLevel.Enough, MachineStockLevel.Low, true, true, TokenAvailabilityLevel.Enough)]
    [TestCase(MachineStockLevel.Low, MachineStockLevel.Unknown, true, true, TokenAvailabilityLevel.Low)]
    [TestCase(MachineStockLevel.Unknown, MachineStockLevel.Enough, true, false, TokenAvailabilityLevel.Unknown)]
    [TestCase(MachineStockLevel.Unknown, MachineStockLevel.Enough, false, true, TokenAvailabilityLevel.Enough)]
    [TestCase(MachineStockLevel.Enough, MachineStockLevel.Enough, false, false, TokenAvailabilityLevel.Unknown)]
    public void AvailabilityWithoutInventory_UsesOnlyConfiguredHopperSensors(
        MachineStockLevel hopper1,
        MachineStockLevel hopper2,
        bool hopper1Enabled,
        bool hopper2Enabled,
        TokenAvailabilityLevel expected)
    {
        Assert.That(
            TokenInventoryPolicy.ResolveWithoutInventory(
                hopper1,
                hopper2,
                hopper1Enabled,
                hopper2Enabled),
            Is.EqualTo(expected));
    }
}
