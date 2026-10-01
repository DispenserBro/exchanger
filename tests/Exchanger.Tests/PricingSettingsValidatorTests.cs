using Exchanger.Core.Configuration;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class PricingSettingsValidatorTests
{
    [Test]
    public void Validate_DefaultPricing_IsValid()
    {
        PricingValidationResult result = PricingSettingsValidator.Validate(TestSettings.Create().Pricing);

        Assert.That(result.IsValid, Is.True);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(100001)]
    public void Validate_InvalidTokenPrice_ReportsTokenPrice(int price)
    {
        PricingSettings pricing = TestSettings.Create().Pricing;
        pricing.TokenPriceRubles = price;

        PricingValidationResult result = PricingSettingsValidator.Validate(pricing);

        Assert.That(result.Issues, Has.Some.Property("Field").EqualTo(PricingValidationField.TokenPrice));
    }

    [Test]
    public void Validate_DuplicateThresholds_ReportsBothRows()
    {
        PricingSettings pricing = TestSettings.Create().Pricing;
        pricing.BonusRules =
        [
            new BonusRule { MinimumAmountRubles = 100, BonusTokens = 2 },
            new BonusRule { MinimumAmountRubles = 100, BonusTokens = 5 },
        ];

        PricingValidationResult result = PricingSettingsValidator.Validate(pricing);

        Assert.That(
            result.Issues.Count(issue => issue.Field == PricingValidationField.BonusRule),
            Is.EqualTo(2));
    }

    [Test]
    public void Validate_ThresholdOutsideRange_ReportsSpecificRow()
    {
        PricingSettings pricing = TestSettings.Create().Pricing;
        pricing.BonusRules = [new BonusRule { MinimumAmountRubles = 5, BonusTokens = 1 }];

        PricingValidationResult result = PricingSettingsValidator.Validate(pricing);

        Assert.That(result.Issues, Has.Some.Property("RuleIndex").EqualTo(0));
    }

    [Test]
    public void Validate_NegativeBonus_RejectsConfiguration()
    {
        PricingSettings pricing = TestSettings.Create().Pricing;
        pricing.BonusRules = [new BonusRule { MinimumAmountRubles = 100, BonusTokens = -1 }];

        PricingValidationResult result = PricingSettingsValidator.Validate(pricing);

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void Validate_DuplicateCardPresets_ReportsBothRows()
    {
        PricingSettings pricing = TestSettings.Create().Pricing;
        pricing.PaymentPresets =
        [
            new PaymentPreset { AmountRubles = 100 },
            new PaymentPreset { AmountRubles = 100 },
        ];

        PricingValidationResult result = PricingSettingsValidator.Validate(pricing);

        Assert.That(
            result.Issues.Count(issue => issue.Field == PricingValidationField.PaymentPreset),
            Is.EqualTo(2));
    }

    [Test]
    public void Validate_CardPresetMustRespectConfiguredStep()
    {
        PricingSettings pricing = TestSettings.Create().Pricing;
        pricing.CustomAmountStepRubles = 25;
        pricing.PaymentPresets = [new PaymentPreset { AmountRubles = 110 }];

        PricingValidationResult result = PricingSettingsValidator.Validate(pricing);

        Assert.That(result.Issues, Has.Some.Property("PresetIndex").EqualTo(0));
    }

    [Test]
    public void Validate_MaximumAndStepOutsideRange_AreRejected()
    {
        PricingSettings pricing = TestSettings.Create().Pricing;
        pricing.MaxCardAmountRubles = 5;
        pricing.CustomAmountStepRubles = 0;

        PricingValidationResult result = PricingSettingsValidator.Validate(pricing);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Issues, Has.Some.Property("Field").EqualTo(PricingValidationField.MaximumCardAmount));
            Assert.That(result.Issues, Has.Some.Property("Field").EqualTo(PricingValidationField.CustomAmountStep));
        }
    }
}
