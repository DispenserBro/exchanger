using Exchanger.Core.Navigation;
using Exchanger.Core.Theming;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class ThemeBindingFallbackComposerTests
{
    [TestCase(ScreenId.Home)]
    [TestCase(ScreenId.CashPayment)]
    [TestCase(ScreenId.CardAmount)]
    [TestCase(ScreenId.CardCustomAmount)]
    [TestCase(ScreenId.CardTerminal)]
    [TestCase(ScreenId.Success)]
    [TestCase(ScreenId.Error)]
    [TestCase(ScreenId.ServiceAccess)]
    [TestCase(ScreenId.Settings)]
    public void RequiredRuntimeBindings_HaveBuiltInFallbackPaths(ScreenId screenId)
    {
        var contract = new ThemeScreenBindingContract { Bindings = [] };

        IReadOnlyList<ThemeSceneContract.FallbackBindingRequirement> requirements =
            ThemeSceneContract.GetFallbackBindings(screenId, contract);

        Assert.That(
            requirements
                .Where(requirement => requirement.Required)
                .All(requirement => !string.IsNullOrWhiteSpace(requirement.FallbackPath)),
            Is.True);
    }

    [Test]
    public void SelectTransplantRoots_WhenPanelAndChildrenAreMissing_MovesPanelOnly()
    {
        ThemeSceneContract.FallbackBindingRequirement panel = Requirement(
            "settings.scroll.content.menu_visibility_panel",
            "SafeMargin/Scroll/Content/MenuVisibilityPanel");
        ThemeSceneContract.FallbackBindingRequirement stockToggle = Requirement(
            "settings.scroll.content.menu_visibility_panel.margin.content.show_stock_status",
            "SafeMargin/Scroll/Content/MenuVisibilityPanel/Margin/Content/ShowStockStatus");
        ThemeSceneContract.FallbackBindingRequirement promotionToggle = Requirement(
            "settings.scroll.content.menu_visibility_panel.margin.content.show_promotion_block",
            "SafeMargin/Scroll/Content/MenuVisibilityPanel/Margin/Content/ShowPromotionBlock");

        ThemeSceneContract.FallbackBindingRequirement[] roots =
            ThemeBindingFallbackComposer.SelectTransplantRoots(
                [panel, stockToggle, promotionToggle]);

        Assert.That(roots.Select(requirement => requirement.Id), Is.EqualTo([panel.Id]));
    }

    [Test]
    public void SelectTransplantRoots_WhenIndependentElementsAreMissing_MovesBoth()
    {
        ThemeSceneContract.FallbackBindingRequirement advertisement = Requirement(
            "home.scroll.content.advertisement_panel",
            "SafeMargin/Scroll/Content/AdvertisementPanel");
        ThemeSceneContract.FallbackBindingRequirement count = Requirement(
            "home.scroll.content.stock_status.approximate_count",
            "SafeMargin/Scroll/Content/StockStatus/Margin/Content/AvailabilityRow/ApproximateCount");

        ThemeSceneContract.FallbackBindingRequirement[] roots =
            ThemeBindingFallbackComposer.SelectTransplantRoots([advertisement, count]);

        Assert.That(
            roots.Select(requirement => requirement.Id),
            Is.EquivalentTo([advertisement.Id, count.Id]));
    }

    private static ThemeSceneContract.FallbackBindingRequirement Requirement(
        string id,
        string fallbackPath) =>
        new(id, ["Control"], fallbackPath, Required: true);
}
