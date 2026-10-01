using Exchanger.Core.Session;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class PricingPolicyTests
{
    [TestCase(0, false)]
    [TestCase(9, false)]
    [TestCase(10, true)]
    [TestCase(15, false)]
    [TestCase(100, true)]
    [TestCase(5000, true)]
    [TestCase(5010, false)]
    public void IsAmountValid_RespectsRangeAndStep(int amountRubles, bool expected)
    {
        bool actual = PricingPolicy.IsAmountValid(TestSettings.Create().Pricing, amountRubles);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase(50, 5, 0)]
    [TestCase(100, 10, 2)]
    [TestCase(499, 49, 2)]
    [TestCase(500, 50, 15)]
    public void Calculate_UsesHighestApplicableBonus(int amountRubles, int baseTokens, int bonusTokens)
    {
        TokenCalculation result = PricingPolicy.Calculate(TestSettings.Create().Pricing, amountRubles);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.BaseTokens, Is.EqualTo(baseTokens));
            Assert.That(result.BonusTokens, Is.EqualTo(bonusTokens));
            Assert.That(result.TotalTokens, Is.EqualTo(baseTokens + bonusTokens));
        }
    }

    [TestCase(100, 11, false)]
    [TestCase(100, 12, true)]
    [TestCase(500, 64, false)]
    [TestCase(500, 65, true)]
    public void IsWithinTokenLimit_IncludesBonusTokens(
        int amountRubles,
        int maximumAvailableTokens,
        bool expected)
    {
        bool actual = PricingPolicy.IsWithinTokenLimit(
            TestSettings.Create().Pricing,
            amountRubles,
            maximumAvailableTokens);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void IsWithinTokenLimit_NullLimitDisablesInventoryRestriction()
    {
        Assert.That(
            PricingPolicy.IsWithinTokenLimit(TestSettings.Create().Pricing, 5000, null),
            Is.True);
    }
}
