using Exchanger.Core.Configuration;
using Exchanger.Core.Session;
using System;

namespace Exchanger.Scenes.CashPaymentScreen;

internal readonly record struct CashPaymentSummaryText(
    string Balance,
    string BaseTokens,
    string Bonus);

internal static class CashPaymentSummaryFormatter
{
    public static CashPaymentSummaryText Format(PricingSettings pricing, int balanceRubles)
    {
        ArgumentNullException.ThrowIfNull(pricing);
        int safeBalance = Math.Max(0, balanceRubles);
        TokenCalculation calculation = PricingPolicy.Calculate(pricing, safeBalance);

        return new CashPaymentSummaryText(
            $"{safeBalance} РУБЛЕЙ",
            TokenCountFormatter.FormatUpper(calculation.BaseTokens),
            TokenCountFormatter.FormatGiftUpper(calculation.BonusTokens));
    }
}
