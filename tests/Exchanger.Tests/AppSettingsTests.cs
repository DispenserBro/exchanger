using Exchanger.Core.Audio;
using Exchanger.Core.Configuration;
using NUnit.Framework;
using System.Text.Json;

namespace Exchanger.Tests;

[TestFixture]
public sealed class AppSettingsTests
{
    [Test]
    public void DefaultConfiguration_DeserializesAndNormalizes()
    {
        string root = ProjectPaths.FindRoot();
        string json = File.ReadAllText(Path.Combine(root, "config", "default_settings.json"));
        AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        Assert.That(settings, Is.Not.Null);
        settings!.Normalize();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.ConfigurationVersion, Is.EqualTo(AppSettings.CurrentConfigurationVersion));
            Assert.That(settings.Window.DebugWindowWidth, Is.EqualTo(720));
            Assert.That(settings.Window.DebugWindowHeight, Is.EqualTo(1280));
            Assert.That(settings.Pricing.PaymentPresets, Is.Not.Empty);
            Assert.That(settings.Advertisement.Muted, Is.True);
            Assert.That(settings.Audio.MasterVolumePercent, Is.EqualTo(100));
            Assert.That(settings.Audio.Muted, Is.False);
            Assert.That(settings.Audio.MusicVolumePercent, Is.EqualTo(100));
            Assert.That(settings.Audio.SoundEffectsVolumePercent, Is.EqualTo(100));
            Assert.That(settings.Audio.AdvertisementVolumePercent, Is.EqualTo(100));
            Assert.That(settings.Payments.CashEnabled, Is.True);
            Assert.That(settings.Payments.CardEnabled, Is.True);
            Assert.That(settings.MenuVisibility.ShowStockStatus, Is.True);
            Assert.That(settings.MenuVisibility.ShowCustomText, Is.True);
            Assert.That(settings.MenuVisibility.ShowPromotionBlock, Is.True);
            Assert.That(settings.MenuVisibility.ShowInteractivePet, Is.True);
            Assert.That(settings.TokenInventory.Enabled, Is.True);
            Assert.That(settings.TokenInventory.PurchaseLimitEnabled, Is.True);
            Assert.That(settings.Branding.ShortText, Is.EqualTo("ПОЛУЧИ СВОИ ЖЕТОНЫ!"));
            Assert.That(settings.Hardware.BaudRate, Is.EqualTo(115200));
            Assert.That(settings.Hardware.FrameTimeoutMilliseconds, Is.EqualTo(100));
            Assert.That(settings.Hardware.HandshakeTimeoutMilliseconds, Is.EqualTo(2000));
            Assert.That(settings.Hardware.ControllerAesMode, Is.EqualTo("ECB"));
            Assert.That(settings.Hardware.DispenseMode, Is.EqualTo("Unsupported"));
            Assert.That(settings.Hardware.ServiceButtonInputIndex, Is.EqualTo(8));
            Assert.That(settings.Hardware.ServiceButtonActiveHigh, Is.False);
            Assert.That(settings.Hardware.StockSensorInputIndex, Is.EqualTo(1));
            Assert.That(settings.Hardware.StockLowWhenInputHigh, Is.False);
            Assert.That(settings.Hardware.Hopper2StockSensorInputIndex, Is.EqualTo(4));
            Assert.That(settings.Hardware.Hopper2StockLowWhenInputHigh, Is.False);
            Assert.That(settings.Hardware.Hopper1DispenseSensorInputIndex, Is.EqualTo(24));
            Assert.That(settings.Hardware.Hopper1DispenseSensorActiveHigh, Is.False);
            Assert.That(settings.Hardware.Hopper2DispenseSensorInputIndex, Is.EqualTo(25));
            Assert.That(settings.Hardware.Hopper2DispenseSensorActiveHigh, Is.False);
            Assert.That(settings.Hardware.InputPollMilliseconds, Is.EqualTo(100));
            Assert.That(settings.Hardware.StockPollSeconds, Is.EqualTo(5));
            Assert.That(settings.Themes.ExternalDlcEnabled, Is.True);
            Assert.That(settings.Themes.ExternalDlcPath, Is.EqualTo("user://theme_dlc/active_theme.pck"));
            Assert.That(settings.Security.ServicePinEnabled, Is.False);
            Assert.That(settings.Security.ServicePinSaltBase64, Is.Empty);
            Assert.That(settings.Security.ServicePinHashBase64, Is.Empty);
            Assert.That(settings.Security.ServicePinIterations, Is.EqualTo(ServiceAccessPolicy.DefaultPbkdf2Iterations));
        }
    }

    [Test]
    public void MenuVisibility_LegacyJsonKeyStillControlsCustomText()
    {
        MenuVisibilitySettings? visibility = JsonSerializer.Deserialize<MenuVisibilitySettings>(
            "{\"ShowRightCharacter\":false}",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(visibility, Is.Not.Null);
        Assert.That(visibility!.ShowCustomText, Is.False);
        Assert.That(JsonSerializer.Serialize(visibility), Does.Contain("\"ShowRightCharacter\""));
    }

    [Test]
    public void Normalize_ClampsUnsafeValuesAndCleansAdvertisementPaths()
    {
        var settings = new AppSettings
        {
            Timeouts = new TimeoutSettings
            {
                SessionIdleSeconds = 1,
                CashPaymentSeconds = 1,
                CashlessPaymentSeconds = 9999,
                SuccessSeconds = 1,
            },
            Pricing = new PricingSettings
            {
                TokenPriceRubles = 0,
                MaxCardAmountRubles = 0,
                CustomAmountStepRubles = 0,
            },
            Hardware = new HardwareSettings
            {
                HandshakeTimeoutMilliseconds = 0,
                ServiceButtonInputIndex = 100,
                StockSensorInputIndex = -100,
                Hopper2StockSensorInputIndex = -100,
                Hopper1DispenseSensorInputIndex = 100,
                Hopper2DispenseSensorInputIndex = 100,
                InputPollMilliseconds = 1,
                MockStockLevel = "NotARealLevel",
            },
            Advertisement = new AdvertisementSettings
            {
                PosterPath = "  res://poster.png  ",
                VideoPaths = [" res://ad.ogv ", "res://ad.ogv", "C:\\Ads\\clip.mp4", "C:/Ads/clip.mp4", "  "],
            },
            Audio = new AudioSettings
            {
                MasterVolumePercent = -10,
                MusicVolumePercent = -20,
                SoundEffectsVolumePercent = 250,
                AdvertisementVolumePercent = 250,
            },
            Security = new SecuritySettings
            {
                ServicePinEnabled = true,
                ServicePinSaltBase64 = "broken-salt",
                ServicePinHashBase64 = "broken-hash",
                ServicePinIterations = 1,
            },
        };

        settings.Normalize();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.Timeouts.SessionIdleSeconds, Is.EqualTo(15));
            Assert.That(settings.Timeouts.CashPaymentSeconds, Is.EqualTo(15));
            Assert.That(settings.Timeouts.CashlessPaymentSeconds, Is.EqualTo(600));
            Assert.That(settings.Timeouts.SuccessSeconds, Is.EqualTo(3));
            Assert.That(settings.Pricing.TokenPriceRubles, Is.EqualTo(1));
            Assert.That(settings.Pricing.CustomAmountStepRubles, Is.EqualTo(1));
            Assert.That(settings.Hardware.MockStockLevel, Is.EqualTo("Enough"));
            Assert.That(settings.Hardware.HandshakeTimeoutMilliseconds, Is.EqualTo(1000));
            Assert.That(settings.Hardware.ServiceButtonInputIndex, Is.EqualTo(25));
            Assert.That(settings.Hardware.StockSensorInputIndex, Is.EqualTo(-1));
            Assert.That(settings.Hardware.Hopper2StockSensorInputIndex, Is.EqualTo(-1));
            Assert.That(settings.Hardware.Hopper1DispenseSensorInputIndex, Is.EqualTo(25));
            Assert.That(settings.Hardware.Hopper2DispenseSensorInputIndex, Is.EqualTo(25));
            Assert.That(settings.Hardware.InputPollMilliseconds, Is.EqualTo(50));
            Assert.That(settings.Advertisement.PosterPath, Is.EqualTo("res://poster.png"));
            Assert.That(settings.Advertisement.VideoPaths, Is.EqualTo(new[] { "res://ad.ogv", "C:/Ads/clip.mp4" }));
            Assert.That(settings.Audio.MasterVolumePercent, Is.Zero);
            Assert.That(settings.Audio.MusicVolumePercent, Is.Zero);
            Assert.That(settings.Audio.SoundEffectsVolumePercent, Is.EqualTo(100));
            Assert.That(settings.Audio.AdvertisementVolumePercent, Is.EqualTo(100));
            Assert.That(settings.Themes.ExternalDlcPath, Is.EqualTo("user://theme_dlc/active_theme.pck"));
            Assert.That(settings.Security.ServicePinSaltBase64, Is.Empty);
            Assert.That(settings.Security.ServicePinHashBase64, Is.Empty);
            Assert.That(settings.Security.ServicePinIterations, Is.EqualTo(ServiceAccessPolicy.DefaultPbkdf2Iterations));
        }
    }

    [Test]
    public void Normalize_ReplacesLegacyAdvertisementFallbackTextWithPromoTerm()
    {
        var settings = new AppSettings
        {
            Advertisement = new AdvertisementSettings
            {
                FallbackText = "РЕКЛАМНЫЙ БЛОК",
            },
        };

        settings.Normalize();

        Assert.That(settings.Advertisement.FallbackText, Is.EqualTo("ПРОМО-БЛОК"));
    }

    [TestCase(100, 0.0f)]
    [TestCase(50, -30.0f)]
    [TestCase(3, -58.2f)]
    [TestCase(0, AudioSettingsApplier.MinimumDecibels)]
    [TestCase(-25, AudioSettingsApplier.MinimumDecibels)]
    [TestCase(250, 0.0f)]
    public void AudioVolume_ConvertsPercentToDecibels(int percent, float expected)
    {
        Assert.That(AudioSettingsApplier.PercentToDecibels(percent), Is.EqualTo(expected).Within(0.001f));
    }
}

internal static class ProjectPaths
{
    public static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "project.godot")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Не найден корень проекта Exchanger.");
    }
}
