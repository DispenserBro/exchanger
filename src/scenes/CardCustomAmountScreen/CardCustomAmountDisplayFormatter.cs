using Exchanger.Core.Configuration;
using Exchanger.Core.Session;
using System;
using System.Globalization;

namespace Exchanger.Scenes.CardCustomAmountScreen;

internal readonly record struct CardCustomAmountDisplayText(
    string InputAmount,
    string BaseTokens,
    string BonusTokens);

internal static class CardCustomAmountDisplayFormatter
{
    public static CardCustomAmountDisplayText Format(PricingSettings pricing, int amountRubles)
    {
        ArgumentNullException.ThrowIfNull(pricing);
        int safeAmount = Math.Max(0, amountRubles);
        TokenCalculation calculation = PricingPolicy.Calculate(pricing, safeAmount);

        return new CardCustomAmountDisplayText(
            safeAmount.ToString(CultureInfo.InvariantCulture),
            TokenCountFormatter.FormatUpper(calculation.BaseTokens),
            TokenCountFormatter.FormatGiftUpper(calculation.BonusTokens));
    }
}
