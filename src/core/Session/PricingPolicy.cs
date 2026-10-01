using Exchanger.Core.Configuration;
using System;
using System.Linq;

namespace Exchanger.Core.Session;

public readonly record struct TokenCalculation(int BaseTokens, int BonusTokens)
{
    public int TotalTokens => BaseTokens + BonusTokens;
}

/// <summary>
/// Чистые правила суммы и расчёта жетонов, не зависящие от Godot.
/// </summary>
public static class PricingPolicy
{
    public static bool IsAmountValid(PricingSettings pricing, int amountRubles)
    {
        ArgumentNullException.ThrowIfNull(pricing);
        int step = Math.Max(1, pricing.CustomAmountStepRubles);
        return amountRubles >= pricing.TokenPriceRubles
               && amountRubles <= pricing.MaxCardAmountRubles
               && amountRubles % step == 0;
    }

    public static TokenCalculation Calculate(PricingSettings pricing, int amountRubles)
    {
        ArgumentNullException.ThrowIfNull(pricing);
        int baseTokens = amountRubles / Math.Max(1, pricing.TokenPriceRubles);
        int bonusTokens = (pricing.BonusRules ?? Array.Empty<BonusRule>())
            .Where(rule => rule is not null && amountRubles >= rule.MinimumAmountRubles)
            .Select(rule => rule.BonusTokens)
            .DefaultIfEmpty(0)
            .Max();
        return new TokenCalculation(baseTokens, bonusTokens);
    }

    public static bool IsWithinTokenLimit(
        PricingSettings pricing,
        int amountRubles,
        int? maximumAvailableTokens)
    {
        if (maximumAvailableTokens is null)
        {
            return true;
        }

        return Calculate(pricing, amountRubles).TotalTokens
               <= Math.Max(0, maximumAvailableTokens.Value);
    }
}
