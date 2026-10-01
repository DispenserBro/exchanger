using Exchanger.Scenes.CardCustomAmountScreen;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class CardCustomAmountDisplayFormatterTests
{
    [Test]
    public void Format_SeparatesZeroInputFromTokenSummary()
    {
        CardCustomAmountDisplayText result = CardCustomAmountDisplayFormatter.Format(
            TestSettings.Create().Pricing,
            0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.InputAmount, Is.EqualTo("0"));
            Assert.That(result.BaseTokens, Is.EqualTo("0 ЖЕТОНОВ"));
            Assert.That(result.BonusTokens, Is.EqualTo("БЕЗ БОНУСА"));
        }
    }

    [TestCase(100, "100", "10 ЖЕТОНОВ", "+ 2 ЖЕТОНА\nВ ПОДАРОК")]
    [TestCase(500, "500", "50 ЖЕТОНОВ", "+ 15 ЖЕТОНОВ\nВ ПОДАРОК")]
    public void Format_UsesInputFieldAndTokenOnlySummary(
        int amount,
        string expectedInput,
        string expectedBaseTokens,
        string expectedBonusTokens)
    {
        CardCustomAmountDisplayText result = CardCustomAmountDisplayFormatter.Format(
            TestSettings.Create().Pricing,
            amount);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.InputAmount, Is.EqualTo(expectedInput));
            Assert.That(result.BaseTokens, Is.EqualTo(expectedBaseTokens));
            Assert.That(result.BonusTokens, Is.EqualTo(expectedBonusTokens));
        }
    }
}
