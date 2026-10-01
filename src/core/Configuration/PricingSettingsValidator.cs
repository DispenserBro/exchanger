using System;
using System.Collections.Generic;
using System.Linq;

namespace Exchanger.Core.Configuration;

public enum PricingValidationField
{
    TokenPrice,
    MaximumCardAmount,
    CustomAmountStep,
    BonusRule,
    PaymentPreset,
}

public readonly record struct PricingValidationIssue(
    PricingValidationField Field,
    string Message,
    int? RuleIndex = null,
    int? PresetIndex = null);

public sealed class PricingValidationResult
{
    public PricingValidationResult(IReadOnlyList<PricingValidationIssue> issues)
    {
        Issues = issues ?? throw new ArgumentNullException(nameof(issues));
    }

    public IReadOnlyList<PricingValidationIssue> Issues { get; }

    public bool IsValid => Issues.Count == 0;
}

/// <summary>
/// Строгая проверка редактируемых тарифов до нормализации и записи.
/// В отличие от AppSettings.Normalize не исправляет пользовательский ввод молча.
/// </summary>
public static class PricingSettingsValidator
{
    public const int MinimumTokenPriceRubles = 1;
    public const int MaximumTokenPriceRubles = 100000;
    public const int MaximumBonusTokens = 100000;
    public const int MaximumCardAmountRubles = 1000000;

    public static PricingValidationResult Validate(PricingSettings pricing)
    {
        ArgumentNullException.ThrowIfNull(pricing);

        var issues = new List<PricingValidationIssue>();
        if (pricing.TokenPriceRubles < MinimumTokenPriceRubles
            || pricing.TokenPriceRubles > MaximumTokenPriceRubles)
        {
            issues.Add(new PricingValidationIssue(
                PricingValidationField.TokenPrice,
                $"Укажите цену от {MinimumTokenPriceRubles} до {MaximumTokenPriceRubles} ₽."));
        }

        if (pricing.TokenPriceRubles > pricing.MaxCardAmountRubles)
        {
            issues.Add(new PricingValidationIssue(
                PricingValidationField.TokenPrice,
                $"Цена жетона должна быть не больше {pricing.MaxCardAmountRubles} ₽."));
        }

        if (pricing.MaxCardAmountRubles < pricing.TokenPriceRubles
            || pricing.MaxCardAmountRubles > MaximumCardAmountRubles)
        {
            issues.Add(new PricingValidationIssue(
                PricingValidationField.MaximumCardAmount,
                $"Укажите максимальную сумму от {Math.Max(1, pricing.TokenPriceRubles)} до {MaximumCardAmountRubles} ₽."));
        }

        if (pricing.CustomAmountStepRubles < 1
            || pricing.CustomAmountStepRubles > pricing.MaxCardAmountRubles)
        {
            issues.Add(new PricingValidationIssue(
                PricingValidationField.CustomAmountStep,
                $"Укажите шаг от 1 до {Math.Max(1, pricing.MaxCardAmountRubles)} ₽."));
        }

        PaymentPreset[] presets = pricing.PaymentPresets ?? Array.Empty<PaymentPreset>();
        var duplicatePresetAmounts = presets
            .Where(preset => preset is not null)
            .GroupBy(preset => preset.AmountRubles)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();

        for (int index = 0; index < presets.Length; index++)
        {
            PaymentPreset? preset = presets[index];
            if (preset is null)
            {
                issues.Add(new PricingValidationIssue(
                    PricingValidationField.PaymentPreset,
                    "Укажите сумму.",
                    PresetIndex: index));
                continue;
            }

            if (preset.AmountRubles < pricing.TokenPriceRubles
                || preset.AmountRubles > pricing.MaxCardAmountRubles)
            {
                issues.Add(new PricingValidationIssue(
                    PricingValidationField.PaymentPreset,
                    $"Укажите сумму от {pricing.TokenPriceRubles} до {pricing.MaxCardAmountRubles} ₽.",
                    PresetIndex: index));
            }
            else if (pricing.CustomAmountStepRubles > 0
                     && preset.AmountRubles % pricing.CustomAmountStepRubles != 0)
            {
                issues.Add(new PricingValidationIssue(
                    PricingValidationField.PaymentPreset,
                    $"Сумма должна делиться на {pricing.CustomAmountStepRubles} ₽ без остатка.",
                    PresetIndex: index));
            }

            if (duplicatePresetAmounts.Contains(preset.AmountRubles))
            {
                issues.Add(new PricingValidationIssue(
                    PricingValidationField.PaymentPreset,
                    $"Сумма {preset.AmountRubles} ₽ уже добавлена.",
                    PresetIndex: index));
            }
        }

        BonusRule[] rules = pricing.BonusRules ?? Array.Empty<BonusRule>();
        var duplicateThresholds = rules
            .Where(rule => rule is not null)
            .GroupBy(rule => rule.MinimumAmountRubles)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();

        for (int index = 0; index < rules.Length; index++)
        {
            BonusRule? rule = rules[index];
            if (rule is null)
            {
                issues.Add(new PricingValidationIssue(
                    PricingValidationField.BonusRule,
                    "Заполните условия бонуса.",
                    index));
                continue;
            }

            if (rule.MinimumAmountRubles < pricing.TokenPriceRubles
                || rule.MinimumAmountRubles > pricing.MaxCardAmountRubles)
            {
                issues.Add(new PricingValidationIssue(
                    PricingValidationField.BonusRule,
                    $"Укажите сумму от {pricing.TokenPriceRubles} до {pricing.MaxCardAmountRubles} ₽.",
                    index));
            }

            if (rule.BonusTokens < 0 || rule.BonusTokens > MaximumBonusTokens)
            {
                issues.Add(new PricingValidationIssue(
                    PricingValidationField.BonusRule,
                    $"Укажите от 0 до {MaximumBonusTokens} подарочных жетонов.",
                    index));
            }

            if (duplicateThresholds.Contains(rule.MinimumAmountRubles))
            {
                issues.Add(new PricingValidationIssue(
                    PricingValidationField.BonusRule,
                    $"Бонус для суммы {rule.MinimumAmountRubles} ₽ уже добавлен.",
                    index));
            }
        }

        return new PricingValidationResult(issues);
    }
}
