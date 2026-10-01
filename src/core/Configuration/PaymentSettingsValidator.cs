using System;
using System.Collections.Generic;

namespace Exchanger.Core.Configuration;

public enum PaymentValidationField
{
    PaymentMethods,
    SessionIdleTimeout,
    CashPaymentTimeout,
    CashlessPaymentTimeout,
    SuccessTimeout,
}

public readonly record struct PaymentValidationIssue(PaymentValidationField Field, string Message);

public sealed class PaymentValidationResult
{
    public PaymentValidationResult(IReadOnlyList<PaymentValidationIssue> issues)
    {
        Issues = issues ?? throw new ArgumentNullException(nameof(issues));
    }

    public IReadOnlyList<PaymentValidationIssue> Issues { get; }

    public bool IsValid => Issues.Count == 0;
}

/// <summary>Проверяет операторские настройки способов оплаты и таймаутов до записи.</summary>
public static class PaymentSettingsValidator
{
    public static PaymentValidationResult Validate(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var issues = new List<PaymentValidationIssue>();
        if (!settings.Payments.CashEnabled && !settings.Payments.CardEnabled)
        {
            issues.Add(new PaymentValidationIssue(
                PaymentValidationField.PaymentMethods,
                "Оставьте хотя бы один способ оплаты."));
        }

        ValidateRange(
            settings.Timeouts.SessionIdleSeconds,
            15,
            600,
            PaymentValidationField.SessionIdleTimeout,
            "Время до возврата назад",
            issues);
        ValidateRange(
            settings.Timeouts.CashPaymentSeconds,
            15,
            600,
            PaymentValidationField.CashPaymentTimeout,
            "Время оплаты наличными",
            issues);
        ValidateRange(
            settings.Timeouts.CashlessPaymentSeconds,
            30,
            600,
            PaymentValidationField.CashlessPaymentTimeout,
            "Время безналичной оплаты",
            issues);
        ValidateRange(
            settings.Timeouts.SuccessSeconds,
            3,
            60,
            PaymentValidationField.SuccessTimeout,
            "Время показа результата",
            issues);

        return new PaymentValidationResult(issues);
    }

    private static void ValidateRange(
        int value,
        int minimum,
        int maximum,
        PaymentValidationField field,
        string caption,
        ICollection<PaymentValidationIssue> issues)
    {
        if (value < minimum || value > maximum)
        {
            issues.Add(new PaymentValidationIssue(
                field,
                $"{caption} — от {minimum} до {maximum} секунд."));
        }
    }
}
