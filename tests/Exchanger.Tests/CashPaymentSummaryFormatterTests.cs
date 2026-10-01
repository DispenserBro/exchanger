using Exchanger.Scenes.CashPaymentScreen;
using NUnit.Framework;

namespace Exchanger.Tests;

public sealed class CashPaymentSummaryFormatterTests
{
    [Test]
    public void Format_SeparatesZeroBalanceTokensAndBonus()
    {
        CashPaymentSummaryText summary = CashPaymentSummaryFormatter.Format(TestSettings.Create().Pricing, 0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summary.Balance, Is.EqualTo("0 РУБЛЕЙ"));
            Assert.That(summary.BaseTokens, Is.EqualTo("0 ЖЕТОНОВ"));
            Assert.That(summary.Bonus, Is.EqualTo("БЕЗ БОНУСА"));
        }
    }

    [TestCase(100, "100 РУБЛЕЙ", "10 ЖЕТОНОВ", "+ 2 ЖЕТОНА\nВ ПОДАРОК")]
    [TestCase(500, "500 РУБЛЕЙ", "50 ЖЕТОНОВ", "+ 15 ЖЕТОНОВ\nВ ПОДАРОК")]
    public void Format_UsesIndependentReferenceFields(
        int balanceRubles,
        string expectedBalance,
        string expectedBaseTokens,
        string expectedBonus)
    {
        CashPaymentSummaryText summary = CashPaymentSummaryFormatter.Format(
            TestSettings.Create().Pricing,
            balanceRubles);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summary.Balance, Is.EqualTo(expectedBalance));
            Assert.That(summary.BaseTokens, Is.EqualTo(expectedBaseTokens));
            Assert.That(summary.Bonus, Is.EqualTo(expectedBonus));
        }
    }
}
