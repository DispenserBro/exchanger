using Exchanger.Core.Configuration;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class ThemeSettingsValidatorTests
{
    [Test]
    public void Validate_DefaultSettings_IsValidEvenWhenPackageWillBeAddedLater()
    {
        ThemeValidationResult result = ThemeSettingsValidator.Validate(new ThemeSettings());

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_DisabledDlcAllowsRetainingInvalidPath()
    {
        var settings = new ThemeSettings
        {
            ExternalDlcEnabled = false,
            ExternalDlcPath = "https://example.com/theme.zip",
        };

        ThemeValidationResult result = ThemeSettingsValidator.Validate(settings);

        Assert.That(result.IsValid, Is.True);
    }

    [TestCase("")]
    [TestCase("relative/theme.pck")]
    [TestCase("res://themes/theme.pck")]
    [TestCase("user://themes/../theme.pck")]
    [TestCase("user://themes/theme.zip")]
    public void Validate_EnabledDlcRejectsUnsafeOrUnsupportedPath(string path)
    {
        var settings = new ThemeSettings { ExternalDlcPath = path };

        ThemeValidationResult result = ThemeSettingsValidator.Validate(settings);

        Assert.That(result.IsValid, Is.False);
    }

    [TestCase("user://theme_dlc/active_theme.pck")]
    [TestCase("C:\\Exchanger\\themes\\active_theme.PCK")]
    public void Validate_EnabledDlcAcceptsSupportedLocalPath(string path)
    {
        var settings = new ThemeSettings { ExternalDlcPath = path };

        ThemeValidationResult result = ThemeSettingsValidator.Validate(settings);

        Assert.That(result.IsValid, Is.True);
    }
}
