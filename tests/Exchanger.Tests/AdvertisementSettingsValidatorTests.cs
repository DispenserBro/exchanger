using Exchanger.Core.Configuration;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class AdvertisementSettingsValidatorTests
{
    [Test]
    public void NormalizeSelectedVideo_PreservesUnicodeExternalPathWithoutAliasOrCopy()
    {
        const string source = "C:\\Videos\\Записи экрана\\clip.mp4";

        string result = AdvertisementVideoLibrary.NormalizeSelectedVideo(source);

        Assert.That(result, Is.EqualTo("C:/Videos/Записи экрана/clip.mp4"));
        Assert.That(result, Does.Not.StartWith("user://"));
    }

    [Test]
    public void Validate_DefaultSettings_IsValid()
    {
        AdvertisementValidationResult result = AdvertisementSettingsValidator.Validate(new AdvertisementSettings());

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_DisabledAdvertisement_AllowsRetainingUnavailableMedia()
    {
        var settings = new AdvertisementSettings
        {
            Enabled = false,
            FallbackText = string.Empty,
            PosterPath = "missing.bmp",
            VideoPaths = ["https://example.com/ad.mp4"],
        };

        AdvertisementValidationResult result = AdvertisementSettingsValidator.Validate(
            settings,
            (_, _) => false);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_EnabledAdvertisementRequiresFallbackText()
    {
        var settings = new AdvertisementSettings { FallbackText = " " };

        AdvertisementValidationResult result = AdvertisementSettingsValidator.Validate(settings);

        Assert.That(result.Issues, Has.Some.Property("Field").EqualTo(AdvertisementValidationField.FallbackText));
    }

    [TestCase("relative/poster.png", AdvertisementAssetType.Poster)]
    [TestCase("https://example.com/ad.ogv", AdvertisementAssetType.Video)]
    [TestCase("user://ads/../poster.png", AdvertisementAssetType.Poster)]
    public void Validate_NonLocalOrTraversalPath_IsRejected(string path, AdvertisementAssetType type)
    {
        AdvertisementSettings settings = CreateWithAsset(path, type);

        AdvertisementValidationResult result = AdvertisementSettingsValidator.Validate(settings);

        Assert.That(result.IsValid, Is.False);
    }

    [TestCase("user://ads/poster.bmp", AdvertisementAssetType.Poster)]
    [TestCase("user://ads/video.avi", AdvertisementAssetType.Video)]
    public void Validate_UnsupportedExtension_IsRejected(string path, AdvertisementAssetType type)
    {
        AdvertisementSettings settings = CreateWithAsset(path, type);

        AdvertisementValidationResult result = AdvertisementSettingsValidator.Validate(settings);

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void Validate_MissingAssetIsReportedByInjectedProbe()
    {
        var settings = new AdvertisementSettings { PosterPath = "user://ads/poster.png" };

        AdvertisementValidationResult result = AdvertisementSettingsValidator.Validate(
            settings,
            (_, _) => false);

        Assert.That(result.Issues, Has.Some.Property("Field").EqualTo(AdvertisementValidationField.PosterPath));
    }

    [Test]
    public void Validate_DuplicateVideosReportsBothRows()
    {
        var settings = new AdvertisementSettings
        {
            VideoPaths = ["user://ads/clip.ogv", "USER://ADS/CLIP.OGV"],
        };

        AdvertisementValidationResult result = AdvertisementSettingsValidator.Validate(
            settings,
            (_, _) => true);

        Assert.That(result.Issues.Count(issue => issue.Field == AdvertisementValidationField.VideoPath), Is.EqualTo(2));
    }

    [Test]
    public void Validate_DuplicateVideosWithSlashVariantsReportsBothRows()
    {
        var settings = new AdvertisementSettings
        {
            VideoPaths = [@"C:\Exchanger\ads\clip.mp4", "C:/Exchanger/ads/clip.mp4"],
        };

        AdvertisementValidationResult result = AdvertisementSettingsValidator.Validate(
            settings,
            (_, _) => true);

        Assert.That(result.Issues.Count(issue => issue.Field == AdvertisementValidationField.VideoPath), Is.EqualTo(2));
    }

    [TestCase("C:\\Exchanger\\ads\\clip.ogv")]
    [TestCase("C:\\Exchanger\\ads\\clip.mp4")]
    [TestCase("C:\\Exchanger\\ads\\clip.mov")]
    [TestCase("C:\\Exchanger\\ads\\clip.m4v")]
    public void Validate_SupportedLocalAssetsAcceptedWhenProbeSucceeds(string videoPath)
    {
        var settings = new AdvertisementSettings
        {
            PosterPath = "user://ads/poster.webp",
            VideoPaths = [videoPath],
        };

        AdvertisementValidationResult result = AdvertisementSettingsValidator.Validate(
            settings,
            (_, _) => true);

        Assert.That(result.IsValid, Is.True);
    }

    private static AdvertisementSettings CreateWithAsset(string path, AdvertisementAssetType type)
    {
        return type == AdvertisementAssetType.Poster
            ? new AdvertisementSettings { PosterPath = path }
            : new AdvertisementSettings { VideoPaths = [path] };
    }
}
