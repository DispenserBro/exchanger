using Exchanger.Core.Configuration;
using NUnit.Framework;
using System.Linq;

namespace Exchanger.Tests;

[TestFixture]
public sealed class BrandingSettingsValidatorTests
{
    [Test]
    public void Validate_DefaultBranding_IsValid()
    {
        BrandingValidationResult result = BrandingSettingsValidator.Validate(new BrandingSettings());

        Assert.That(result.IsValid, Is.True);
    }

    [TestCase(BrandingValidationField.ApplicationName)]
    [TestCase(BrandingValidationField.ShortText)]
    [TestCase(BrandingValidationField.SupportPhone)]
    public void Validate_EmptyRequiredField_IsRejected(BrandingValidationField field)
    {
        var settings = new BrandingSettings();
        SetField(settings, field, "   ");

        BrandingValidationResult result = BrandingSettingsValidator.Validate(settings);

        Assert.That(result.Issues, Has.Some.Property("Field").EqualTo(field));
    }

    [TestCase(BrandingValidationField.ApplicationName, BrandingSettingsValidator.MaximumApplicationNameLength)]
    [TestCase(BrandingValidationField.ShortText, BrandingSettingsValidator.MaximumShortTextLength)]
    [TestCase(BrandingValidationField.SupportPhone, BrandingSettingsValidator.MaximumSupportPhoneLength)]
    public void Validate_OverMaximumLength_IsRejected(BrandingValidationField field, int maximumLength)
    {
        var settings = new BrandingSettings();
        SetField(settings, field, new string('А', maximumLength + 1));

        BrandingValidationResult result = BrandingSettingsValidator.Validate(settings);

        Assert.That(result.Issues, Has.Some.Property("Field").EqualTo(field));
    }

    [Test]
    public void Validate_ControlCharacter_IsRejected()
    {
        var settings = new BrandingSettings { ShortText = "Первая строка\nВторая строка" };

        BrandingValidationResult result = BrandingSettingsValidator.Validate(settings);

        Assert.That(result.Issues, Has.Some.Property("Field").EqualTo(BrandingValidationField.ShortText));
    }

    [Test]
    public void Validate_EmptyShortText_UsesOwnerFacingCaption()
    {
        var settings = new BrandingSettings { ShortText = "" };

        BrandingValidationResult result = BrandingSettingsValidator.Validate(settings);
        string message = result.Issues.Single(issue => issue.Field == BrandingValidationField.ShortText).Message;

        Assert.That(message, Does.Contain("Свой текст").And.Not.Contain("Короткий текст"));
    }

    [Test]
    public void Normalize_BrandingCollapsesWhitespaceAndLimitsLength()
    {
        var settings = new AppSettings
        {
            Branding = new BrandingSettings
            {
                ApplicationName = "  НОВЫЙ\nАВТОМАТ  ",
                ShortText = new string('С', BrandingSettingsValidator.MaximumShortTextLength + 10),
                SupportPhone = "  +7   900  000-00-00  ",
            },
        };

        settings.Normalize();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.Branding.ApplicationName, Is.EqualTo("НОВЫЙ АВТОМАТ"));
            Assert.That(settings.Branding.ShortText.Length, Is.EqualTo(BrandingSettingsValidator.MaximumShortTextLength));
            Assert.That(settings.Branding.SupportPhone, Is.EqualTo("+7 900 000-00-00"));
        }
    }

    private static void SetField(BrandingSettings settings, BrandingValidationField field, string value)
    {
        switch (field)
        {
            case BrandingValidationField.ApplicationName:
                settings.ApplicationName = value;
                break;
            case BrandingValidationField.ShortText:
                settings.ShortText = value;
                break;
            case BrandingValidationField.SupportPhone:
                settings.SupportPhone = value;
                break;
        }
    }
}
