using Exchanger.Core.Configuration;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class PaymentSettingsValidatorTests
{
    [Test]
    public void Validate_DefaultSettings_IsValid()
    {
        PaymentValidationResult result = PaymentSettingsValidator.Validate(TestSettings.Create());

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_BothPaymentMethodsDisabled_IsRejected()
    {
        AppSettings settings = TestSettings.Create();
        settings.Payments.CashEnabled = false;
        settings.Payments.CardEnabled = false;

        PaymentValidationResult result = PaymentSettingsValidator.Validate(settings);

        Assert.That(result.Issues, Has.Some.Property("Field").EqualTo(PaymentValidationField.PaymentMethods));
    }

    [TestCase(14, 45, 120, 10, PaymentValidationField.SessionIdleTimeout)]
    [TestCase(45, 14, 120, 10, PaymentValidationField.CashPaymentTimeout)]
    [TestCase(45, 45, 29, 10, PaymentValidationField.CashlessPaymentTimeout)]
    [TestCase(45, 45, 120, 61, PaymentValidationField.SuccessTimeout)]
    public void Validate_TimeoutOutsideSafeRange_IsRejected(
        int idle,
        int cashPayment,
        int cashlessPayment,
        int success,
        PaymentValidationField expectedField)
    {
        AppSettings settings = TestSettings.Create();
        settings.Timeouts.SessionIdleSeconds = idle;
        settings.Timeouts.CashPaymentSeconds = cashPayment;
        settings.Timeouts.CashlessPaymentSeconds = cashlessPayment;
        settings.Timeouts.SuccessSeconds = success;

        PaymentValidationResult result = PaymentSettingsValidator.Validate(settings);

        Assert.That(result.Issues, Has.Some.Property("Field").EqualTo(expectedField));
    }
}
