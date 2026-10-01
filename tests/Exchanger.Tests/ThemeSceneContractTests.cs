using Exchanger.Core.Navigation;
using Exchanger.Core.Theming;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class ThemeSceneContractTests
{
    [TestCase(
        "home",
        "SafeMargin/Scroll/Content/PaymentPanel/Margin/Content/Buttons/CashButton",
        "home.scroll.content.payment_panel.margin.content.buttons.cash_button")]
    [TestCase(
        "settings",
        "SafeMargin/Scroll/Content/SectionTabs/AdvertisementButton",
        "settings.scroll.content.section_tabs.advertisement_button")]
    [TestCase(
        "card_custom_amount",
        "SafeMargin/Content/KeypadPanel/Content/Keypad/Digit7",
        "card_custom_amount.content.keypad_panel.content.keypad.digit7")]
    [TestCase(
        "cash_payment",
        "SafeMargin/Content/Summary/Margin/Values/Balance",
        "cash_payment.content.summary.margin.values.balance")]
    [TestCase(
        "cash_payment",
        "SafeMargin/Content/Summary/Margin/Values/Tokens",
        "cash_payment.content.summary.margin.values.tokens")]
    [TestCase(
        "cash_payment",
        "SafeMargin/Content/Summary/Margin/Values/Bonus",
        "cash_payment.content.summary.margin.values.bonus")]
    [TestCase(
        "card_custom_amount",
        "SafeMargin/Content/KeypadPanel/Content/InputValue",
        "card_custom_amount.content.keypad_panel.content.input_value")]
    [TestCase(
        "card_terminal",
        "SafeMargin/Content/TopBar/BackButton",
        "card_terminal.content.top_bar.back_button")]
    [TestCase(
        "settings",
        "SafeMargin/Scroll/Content/TimeoutsPanel/Margin/Content/Grid/CashlessPayment",
        "settings.scroll.content.timeouts_panel.margin.content.grid.cashless_payment")]
    [TestCase(
        "settings",
        "SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Hopper1Label",
        "settings.scroll.content.service_inventory_panel.margin.content.hopper1_label")]
    [TestCase(
        "settings",
        "SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/InventoryAccountingEnabled",
        "settings.scroll.content.service_inventory_panel.margin.content.inventory_accounting_enabled")]
    [TestCase(
        "settings",
        "SafeMargin/Scroll/Content/MenuVisibilityPanel/Margin/Content/ShowPromotionBlock",
        "settings.scroll.content.menu_visibility_panel.margin.content.show_promotion_block")]
    [TestCase(
        "settings",
        "SafeMargin/Scroll/Content/MenuVisibilityPanel/Margin/Content/ShowInteractivePet",
        "settings.scroll.content.menu_visibility_panel.margin.content.show_interactive_pet")]
    [TestCase(
        "settings",
        "SafeMargin/Scroll/Content/AdvertisementPosterPanel/Margin/Content/SelectButton",
        "settings.scroll.content.advertisement_poster_panel.margin.content.select_button")]
    [TestCase(
        "settings",
        "Footer/SaveAndExitButton",
        "settings.footer.save_and_exit_button")]
    [TestCase(
        "home",
        "SafeMargin/Scroll/Content/AdvertisementPanel",
        "home.scroll.content.advertisement_panel")]
    [TestCase(
        "cash_payment",
        "SafeMargin/Content/TopBar/HomeNavigationButton",
        "cash_payment.content.top_bar.home_navigation_button")]
    public void BindingId_IsStableAndIndependentFromThemeHierarchy(
        string screenKey,
        string fallbackPath,
        string expected)
    {
        Assert.That(
            ThemeBindingResolver.FromFallbackPath(screenKey, fallbackPath),
            Is.EqualTo(expected));
    }

    [Test]
    public void ScreenCatalog_ContainsEveryApplicationScreenExactlyOnce()
    {
        ScreenId[] expected = Enum.GetValues<ScreenId>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ThemeSceneContract.Screens.Select(item => item.ScreenId), Is.EquivalentTo(expected));
            Assert.That(ThemeSceneContract.Screens.Select(item => item.BindingId), Is.Unique);
            Assert.That(ThemeSceneContract.Screens.Select(item => item.ScenePath), Is.Unique);
            Assert.That(ThemeSceneContract.Screens.Select(item => item.RootName), Is.Unique);
        }
    }

    [Test]
    public void ScreenCatalog_UsesCanonicalNamesAndPaths()
    {
        foreach (ThemeScreenDescriptor descriptor in ThemeSceneContract.Screens)
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(descriptor.BindingId, Is.EqualTo($"screen.{descriptor.Key}"));
                Assert.That(
                    descriptor.ScenePath,
                    Is.EqualTo($"res://exchanger_theme_dlc/scenes/screen_{descriptor.Key}.tscn"));
                Assert.That(descriptor.RootName, Does.StartWith("ThemeScreen_"));
            }
        }
    }

    [Test]
    public void BindingContractIdentity_IsPinnedByHost()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                ThemeBindingContract.ResourcePath,
                Is.EqualTo("res://exchanger_theme_dlc/contracts/screen_bindings.v2.json"));
            Assert.That(ThemeBindingContract.RequiredBindingCount, Is.EqualTo(481));
            Assert.That(ThemeBindingContract.ExpectedSha256, Has.Length.EqualTo(64));
            Assert.That(
                ThemeBindingContract.ExpectedSha256,
                Does.Match("^[0-9A-F]{64}$"));
        }
    }

    [Test]
    public void SupplementalInventoryToggle_UsesStableRuntimeBindingAndBuiltInFallback()
    {
        var contract = new ThemeScreenBindingContract { Bindings = [] };

        ThemeSceneContract.FallbackBindingRequirement requirement = ThemeSceneContract
            .GetFallbackBindings(ScreenId.Settings, contract)
            .Single(item => item.Id ==
                "settings.scroll.content.service_inventory_panel.margin.content.inventory_enabled");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(requirement.Required, Is.True);
            Assert.That(requirement.Types, Does.Contain("CheckButton"));
            Assert.That(
                requirement.FallbackPath,
                Is.EqualTo(
                    "SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/InventoryAccountingEnabled"));
        }
    }

    [Test]
    public void SupplementalInventoryDescription_UsesStableRuntimeBindingAndBuiltInFallback()
    {
        var contract = new ThemeScreenBindingContract { Bindings = [] };

        ThemeSceneContract.FallbackBindingRequirement requirement = ThemeSceneContract
            .GetFallbackBindings(ScreenId.Settings, contract)
            .Single(item => item.Id ==
                "settings.scroll.content.service_inventory_panel.margin.content.description");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(requirement.Required, Is.True);
            Assert.That(requirement.Types, Does.Contain("Label"));
            Assert.That(
                requirement.FallbackPath,
                Is.EqualTo(
                    "SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Description"));
        }
    }

    [Test]
    public void SupplementalPurchaseLimitToggle_UsesStableRuntimeBindingAndBuiltInFallback()
    {
        var contract = new ThemeScreenBindingContract { Bindings = [] };

        ThemeSceneContract.FallbackBindingRequirement requirement = ThemeSceneContract
            .GetFallbackBindings(ScreenId.Settings, contract)
            .Single(item => item.Id ==
                "settings.scroll.content.service_inventory_panel.margin.content.purchase_limit_enabled");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(requirement.Required, Is.True);
            Assert.That(requirement.Types, Does.Contain("CheckButton"));
            Assert.That(
                requirement.FallbackPath,
                Is.EqualTo(
                    "SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/PurchaseLimitEnabled"));
        }
    }

    [Test]
    public void SupplementalCardAmountBonus_UsesStableRuntimeBindingAndBuiltInFallback()
    {
        var contract = new ThemeScreenBindingContract { Bindings = [] };

        ThemeSceneContract.FallbackBindingRequirement requirement = ThemeSceneContract
            .GetFallbackBindings(ScreenId.CardAmount, contract)
            .Single(item => item.Id == "card_amount.content.reward_panel.content.bonus");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(requirement.Required, Is.True);
            Assert.That(requirement.Types, Does.Contain("Label"));
            Assert.That(
                requirement.FallbackPath,
                Is.EqualTo("SafeMargin/Content/RewardPanel/Content/Bonus"));
        }
    }

    [TestCase(
        ScreenId.CashPayment,
        "cash_payment.content.top_bar.home_navigation_button",
        "SafeMargin/Content/TopBar/HomeNavigationButton")]
    [TestCase(
        ScreenId.CardAmount,
        "card_amount.content.top_bar.home_navigation_button",
        "SafeMargin/Content/TopBar/HomeNavigationButton")]
    [TestCase(
        ScreenId.CardCustomAmount,
        "card_custom_amount.content.top_bar.home_navigation_button",
        "SafeMargin/Content/TopBar/HomeNavigationButton")]
    [TestCase(
        ScreenId.CardTerminal,
        "card_terminal.content.top_bar.home_navigation_button",
        "SafeMargin/Content/TopBar/HomeNavigationButton")]
    [TestCase(
        ScreenId.Success,
        "success.content.home_navigation_button",
        "SafeMargin/Content/HomeNavigationButton")]
    [TestCase(
        ScreenId.Error,
        "error.content.home_navigation_button",
        "SafeMargin/Content/HomeNavigationButton")]
    [TestCase(
        ScreenId.ServiceAccess,
        "service_access.content.home_navigation_button",
        "SafeMargin/Content/HomeNavigationButton")]
    public void SupplementalHomeNavigation_UsesStableRuntimeBindingAndBuiltInFallback(
        ScreenId screenId,
        string bindingId,
        string fallbackPath)
    {
        var contract = new ThemeScreenBindingContract { Bindings = [] };

        ThemeSceneContract.FallbackBindingRequirement requirement = ThemeSceneContract
            .GetFallbackBindings(screenId, contract)
            .Single(item => item.Id == bindingId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(requirement.Required, Is.True);
            Assert.That(requirement.Types, Does.Contain("Button"));
            Assert.That(requirement.FallbackPath, Is.EqualTo(fallbackPath));
        }
    }

    [Test]
    public void OptionalCustomTextBlock_UsesThemeOwnedGroupBinding()
    {
        var contract = new ThemeScreenBindingContract { Bindings = [] };

        ThemeSceneContract.FallbackBindingRequirement requirement = ThemeSceneContract
            .GetFallbackBindings(ScreenId.Home, contract)
            .Single(item => item.Id == "home.decoration.custom_text_block");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(requirement.Required, Is.False);
            Assert.That(requirement.Types, Does.Contain("CanvasItem"));
            Assert.That(requirement.FallbackPath, Is.Empty);
        }
    }

    [TestCase("home", "home.content.cash_button", true)]
    [TestCase("home", "settings.content.cash_button", false)]
    [TestCase("home", "home.content/cash_button", false)]
    [TestCase("home", "home.content-cash_button", false)]
    public void ElementBindingId_UsesOnlyItsScreenNamespace(
        string screenKey,
        string bindingId,
        bool expected)
    {
        Assert.That(ThemeSceneContract.IsValidElementBinding(screenKey, bindingId), Is.EqualTo(expected));
    }

    [Test]
    public void SelfContainedVisualContract_UsesCanonicalMetadataAndNodes()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                ThemeSceneContract.SelfContainedVisualsMetadata,
                Is.EqualTo("exchanger_self_contained_visuals"));
            Assert.That(ThemeSceneContract.SelfContainedBackgroundNode, Is.EqualTo("ThemeBackground"));
            Assert.That(ThemeSceneContract.SelfContainedDecorationNode, Is.EqualTo("BackgroundDecoration"));
        }
    }

    [TestCase(false, "res://exchanger_theme_dlc/background.png", "res://exchanger_theme_dlc/background_decoration.tscn", true)]
    [TestCase(false, "", "", true)]
    [TestCase(true, "", "", true)]
    [TestCase(true, "res://exchanger_theme_dlc/background.png", "", false)]
    [TestCase(true, "", "res://exchanger_theme_dlc/background_decoration.tscn", false)]
    public void SelfContainedVisualContract_DoesNotAllowDuplicateGlobalComposition(
        bool selfContained,
        string backgroundPath,
        string decorationPath,
        bool expected)
    {
        Assert.That(
            ThemeSceneContract.IsCompatibleVisualComposition(
                selfContained,
                backgroundPath,
                decorationPath),
            Is.EqualTo(expected));
    }
}
