using Exchanger.Core.Theming;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class ThemeDlcManifestTests
{
    [Test]
    public void ValidManifest_IsAccepted()
    {
        ThemeDlcManifest manifest = CreateValidManifest();

        ThemeDlcManifestFailure failure = ThemeDlcManifestValidator.Validate(manifest);

        Assert.That(failure, Is.EqualTo(ThemeDlcManifestFailure.None));
    }

    [Test]
    public void FullSceneManifestV2_WithCanonicalSceneMap_IsAccepted()
    {
        ThemeDlcManifest manifest = CreateValidManifest(ThemeDlcManifestValidator.SupportedFormatVersion);
        foreach (ThemeScreenDescriptor descriptor in ThemeSceneContract.Screens)
        {
            manifest.ScreenScenes.Add(descriptor.BindingId, descriptor.ScenePath);
        }

        Assert.That(
            ThemeDlcManifestValidator.Validate(manifest),
            Is.EqualTo(ThemeDlcManifestFailure.None));
    }

    [Test]
    public void FullSceneManifestV2_RequiresEveryCanonicalScene()
    {
        ThemeDlcManifest manifest = CreateValidManifest(ThemeDlcManifestValidator.SupportedFormatVersion);

        Assert.That(
            ThemeDlcManifestValidator.Validate(manifest),
            Is.EqualTo(ThemeDlcManifestFailure.InvalidScreenScene));
    }

    [Test]
    public void LegacyManifest_WithScreenScene_IsRejectedBeforeRuntimeBinding()
    {
        ThemeDlcManifest manifest = CreateValidManifest();
        ThemeScreenDescriptor home = ThemeSceneContract.Screens[0];
        manifest.ScreenScenes.Add(home.BindingId, home.ScenePath);

        Assert.That(
            ThemeDlcManifestValidator.Validate(manifest),
            Is.EqualTo(ThemeDlcManifestFailure.InvalidScreenScene));
    }

    [Test]
    public void ResourceOutsideDlcNamespace_IsRejected()
    {
        ThemeDlcManifest manifest = CreateValidManifest();
        manifest.UiThemePath = "res://themes/default/ui_theme.tres";

        ThemeDlcManifestFailure failure = ThemeDlcManifestValidator.Validate(manifest);

        Assert.That(failure, Is.EqualTo(ThemeDlcManifestFailure.InvalidUiThemePath));
    }

    [Test]
    public void ParentTraversal_IsRejected()
    {
        ThemeDlcManifest manifest = CreateValidManifest();
        manifest.BackgroundDecorationPath = "res://exchanger_theme_dlc/../src/scenes/App/App.tscn";

        ThemeDlcManifestFailure failure = ThemeDlcManifestValidator.Validate(manifest);

        Assert.That(failure, Is.EqualTo(ThemeDlcManifestFailure.InvalidBackgroundDecorationPath));
    }

    [Test]
    public void VisualSlotInsideDlcNamespace_IsAccepted()
    {
        ThemeDlcManifest manifest = CreateValidManifest();
        manifest.VisualSlots["home.top"] = "res://exchanger_theme_dlc/visuals/home_top.tscn";

        Assert.That(ThemeDlcManifestValidator.Validate(manifest), Is.EqualTo(ThemeDlcManifestFailure.None));
    }

    [Test]
    public void InteractivePetSceneInsideDlcNamespace_IsAccepted()
    {
        ThemeDlcManifest manifest = CreateValidManifest();
        manifest.InteractivePetScene = "res://exchanger_theme_dlc/components/interactive_pet.tscn";

        Assert.That(ThemeDlcManifestValidator.Validate(manifest), Is.EqualTo(ThemeDlcManifestFailure.None));
    }

    [TestCase("res://themes/sample/components/interactive_pet.tscn")]
    [TestCase("res://exchanger_theme_dlc/../interactive_pet.tscn")]
    [TestCase("res://exchanger_theme_dlc/components/interactive_pet.png")]
    public void InvalidInteractivePetScenePath_IsRejected(string path)
    {
        ThemeDlcManifest manifest = CreateValidManifest();
        manifest.InteractivePetScene = path;

        Assert.That(
            ThemeDlcManifestValidator.Validate(manifest),
            Is.EqualTo(ThemeDlcManifestFailure.InvalidInteractivePetScenePath));
    }

    [TestCase("../home")]
    [TestCase("home/pet")]
    [TestCase("")]
    public void InvalidVisualSlotId_IsRejected(string slotId)
    {
        ThemeDlcManifest manifest = CreateValidManifest();
        manifest.VisualSlots[slotId] = "res://exchanger_theme_dlc/visuals/home_top.tscn";

        Assert.That(ThemeDlcManifestValidator.Validate(manifest), Is.EqualTo(ThemeDlcManifestFailure.InvalidVisualSlot));
    }

    [TestCase(0f)]
    [TestCase(4.1f)]
    public void InvalidBackgroundDecorationScale_IsRejected(float scale)
    {
        ThemeDlcManifest manifest = CreateValidManifest();
        manifest.BackgroundDecorationScale = scale;

        Assert.That(
            ThemeDlcManifestValidator.Validate(manifest),
            Is.EqualTo(ThemeDlcManifestFailure.InvalidBackgroundDecorationScale));
    }

    [TestCase(0, ThemeDlcManifestFailure.UnsupportedVersion)]
    [TestCase(3, ThemeDlcManifestFailure.UnsupportedVersion)]
    public void UnsupportedFormatVersion_IsRejected(int version, ThemeDlcManifestFailure expected)
    {
        ThemeDlcManifest manifest = CreateValidManifest();
        manifest.FormatVersion = version;

        Assert.That(ThemeDlcManifestValidator.Validate(manifest), Is.EqualTo(expected));
    }

    [TestCase("#061229", ThemeDlcManifestFailure.None)]
    [TestCase("061229FF", ThemeDlcManifestFailure.None)]
    [TestCase("#12345", ThemeDlcManifestFailure.InvalidFallbackColor)]
    [TestCase("not-a-color", ThemeDlcManifestFailure.InvalidFallbackColor)]
    public void FallbackColor_IsStrictlyValidated(string color, ThemeDlcManifestFailure expected)
    {
        ThemeDlcManifest manifest = CreateValidManifest();
        manifest.FallbackColor = color;

        Assert.That(ThemeDlcManifestValidator.Validate(manifest), Is.EqualTo(expected));
    }

    private static ThemeDlcManifest CreateValidManifest(
        int formatVersion = ThemeDlcManifestValidator.LegacyFormatVersion)
    {
        return new ThemeDlcManifest
        {
            Format = ThemeDlcManifestValidator.ExpectedFormat,
            FormatVersion = formatVersion,
            ThemeId = "sample-theme",
            DisplayName = "Тестовая тема",
            UiThemePath = "res://exchanger_theme_dlc/ui_theme.tres",
            BackgroundTexturePath = "res://exchanger_theme_dlc/background.png",
            BackgroundDecorationPath = "res://exchanger_theme_dlc/background_decoration.tscn",
            FallbackColor = "#061229",
        };
    }
}
