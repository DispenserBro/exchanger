using Exchanger.Core.Navigation;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Exchanger.Core.Theming;

public readonly record struct ThemeScreenDescriptor(
    ScreenId ScreenId,
    string BindingId,
    string Key,
    string ScenePath,
    string RootName);

public enum ThemeScreenSceneFailure
{
    None,
    MissingScene,
    WrongRootType,
    WrongRootName,
    MissingScreenBinding,
    MissingElementBinding,
    DuplicateElementBinding,
    InvalidElementBinding,
    UnexpectedElementBinding,
    MissingRequiredBinding,
    WrongBindingType,
    InvalidSelfContainedMetadata,
    MissingSelfContainedTheme,
    MissingSelfContainedBackground,
    MissingSelfContainedDecoration,
}

/// <summary>
/// Стабильный контракт имён полносценовой DLC-темы. Идентификаторы не зависят
/// от расположения узлов внутри тематической сцены.
/// </summary>
public static class ThemeSceneContract
{
    private readonly record struct RuntimeBindingRequirement(
        string Id,
        string Type,
        string FallbackPath,
        bool Required = true);

    internal readonly record struct FallbackBindingRequirement(
        string Id,
        string[] Types,
        string FallbackPath,
        bool Required);

    public const string ScreenBindingMetadata = "exchanger_screen_binding_id";
    public const string ElementBindingMetadata = "exchanger_binding_id";
    public const string SelfContainedVisualsMetadata = "exchanger_self_contained_visuals";
    public const string SelfContainedBackgroundNode = "ThemeBackground";
    public const string SelfContainedDecorationNode = "BackgroundDecoration";

    public static readonly IReadOnlyList<ThemeScreenDescriptor> Screens =
    [
        Describe(ScreenId.Home, "home", "Home"),
        Describe(ScreenId.CashPayment, "cash_payment", "CashPayment"),
        Describe(ScreenId.CardAmount, "card_amount", "CardAmount"),
        Describe(ScreenId.CardCustomAmount, "card_custom_amount", "CardCustomAmount"),
        Describe(ScreenId.CardTerminal, "card_terminal", "CardTerminal"),
        Describe(ScreenId.Success, "success", "Success"),
        Describe(ScreenId.Error, "error", "Error"),
        Describe(ScreenId.ServiceAccess, "service_access", "ServiceAccess"),
        Describe(ScreenId.Settings, "settings", "Settings"),
    ];

    // Защита от рассинхронизации host-кода и уже выпущенного JSON-контракта.
    // Эти элементы используются контроллерами напрямую и должны проверяться
    // до активации DLC, иначе несовместимая тема сорвёт запуск приложения.
    private static readonly IReadOnlyDictionary<ScreenId, RuntimeBindingRequirement[]> RuntimeBindings =
        new Dictionary<ScreenId, RuntimeBindingRequirement[]>
        {
            [ScreenId.Home] =
            [
                new(
                    "home.scroll.content.advertisement_panel",
                    "Control",
                    "SafeMargin/Scroll/Content/AdvertisementPanel"),
                new("home.scroll.content.stock_status.meter.empty", "Control", string.Empty, Required: false),
                new("home.scroll.content.stock_status.meter.low", "Control", string.Empty, Required: false),
                new("home.scroll.content.stock_status.meter.enough", "Control", string.Empty, Required: false),
                new("home.scroll.content.stock_status.meter.much", "Control", string.Empty, Required: false),
                new(
                    "home.scroll.content.stock_status.approximate_count",
                    "Label",
                    "SafeMargin/Scroll/Content/StockStatus/Margin/Content/AvailabilityRow/ApproximateCount"),
                new("home.decoration.custom_text_block", "CanvasItem", string.Empty, Required: false),
                new("home.decoration.right_character", "CanvasItem", string.Empty, Required: false),
            ],
            [ScreenId.CardAmount] =
            [
                new(
                    "card_amount.content.reward_panel.content.bonus",
                    "Label",
                    "SafeMargin/Content/RewardPanel/Content/Bonus"),
                new(
                    "card_amount.content.top_bar.home_navigation_button",
                    "Button",
                    "SafeMargin/Content/TopBar/HomeNavigationButton"),
            ],
            [ScreenId.CashPayment] =
            [
                new(
                    "cash_payment.content.banner_text",
                    "Label",
                    "SafeMargin/Content/BannerText"),
                new(
                    "cash_payment.content.top_bar.home_navigation_button",
                    "Button",
                    "SafeMargin/Content/TopBar/HomeNavigationButton"),
            ],
            [ScreenId.CardCustomAmount] =
            [
                new(
                    "card_custom_amount.content.top_bar.home_navigation_button",
                    "Button",
                    "SafeMargin/Content/TopBar/HomeNavigationButton"),
            ],
            [ScreenId.CardTerminal] =
            [
                new(
                    "card_terminal.content.top_bar.home_navigation_button",
                    "Button",
                    "SafeMargin/Content/TopBar/HomeNavigationButton"),
            ],
            [ScreenId.Success] =
            [
                new(
                    "success.content.home_navigation_button",
                    "Button",
                    "SafeMargin/Content/HomeNavigationButton"),
            ],
            [ScreenId.Error] =
            [
                new(
                    "error.content.home_navigation_button",
                    "Button",
                    "SafeMargin/Content/HomeNavigationButton"),
            ],
            [ScreenId.ServiceAccess] =
            [
                new(
                    "service_access.content.home_navigation_button",
                    "Button",
                    "SafeMargin/Content/HomeNavigationButton"),
            ],
            [ScreenId.Settings] =
            [
                new(
                    "settings.scroll.content.animated_banner_texts_panel",
                    "Control",
                    "SafeMargin/Scroll/Content/AnimatedBannerTextsPanel"),
                new(
                    "settings.scroll.content.music_panel",
                    "Control",
                    "SafeMargin/Scroll/Content/MusicPanel"),
                new(
                    "settings.scroll.content.sound_effects_panel",
                    "Control",
                    "SafeMargin/Scroll/Content/SoundEffectsPanel"),
                new(
                    "settings.scroll.content.menu_visibility_panel",
                    "Control",
                    "SafeMargin/Scroll/Content/MenuVisibilityPanel"),
                new(
                    "settings.scroll.content.menu_visibility_panel.margin.content.show_stock_status",
                    "CheckButton",
                    "SafeMargin/Scroll/Content/MenuVisibilityPanel/Margin/Content/ShowStockStatus"),
                new(
                    "settings.scroll.content.menu_visibility_panel.margin.content.show_right_character",
                    "CheckButton",
                    "SafeMargin/Scroll/Content/MenuVisibilityPanel/Margin/Content/ShowRightCharacter"),
                new(
                    "settings.scroll.content.menu_visibility_panel.margin.content.show_promotion_block",
                    "CheckButton",
                    "SafeMargin/Scroll/Content/MenuVisibilityPanel/Margin/Content/ShowPromotionBlock"),
                new(
                    "settings.scroll.content.menu_visibility_panel.margin.content.show_interactive_pet",
                    "CheckButton",
                    "SafeMargin/Scroll/Content/MenuVisibilityPanel/Margin/Content/ShowInteractivePet"),
                new(
                    "settings.scroll.content.advertisement_poster_panel.margin.content.select_button",
                    "Button",
                    "SafeMargin/Scroll/Content/AdvertisementPosterPanel/Margin/Content/SelectButton"),
                new(
                    "settings.scroll.content.service_inventory_panel.margin.content.hopper1_label",
                    "Label",
                    "SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Hopper1Label"),
                new(
                    "settings.scroll.content.service_inventory_panel.margin.content.description",
                    "Label",
                    "SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Description"),
                new(
                    "settings.scroll.content.service_inventory_panel.margin.content.inventory_enabled",
                    "CheckButton",
                    "SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/InventoryAccountingEnabled"),
                new(
                    "settings.scroll.content.service_inventory_panel.margin.content.purchase_limit_enabled",
                    "CheckButton",
                    "SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/PurchaseLimitEnabled"),
                new(
                    "settings.scroll.content.service_inventory_panel.margin.content.hopper1.recount_button",
                    "Button",
                    "SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Hopper1/RecountButton"),
                new(
                    "settings.scroll.content.service_inventory_panel.margin.content.hopper1.manual_add_button",
                    "Button",
                    "SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Hopper1/ManualAddButton"),
                new(
                    "settings.scroll.content.service_inventory_panel.margin.content.hopper2_label",
                    "Label",
                    "SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Hopper2Label"),
                new(
                    "settings.scroll.content.service_inventory_panel.margin.content.hopper2.recount_button",
                    "Button",
                    "SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Hopper2/RecountButton"),
                new(
                    "settings.scroll.content.service_inventory_panel.margin.content.hopper2.manual_add_button",
                    "Button",
                    "SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Hopper2/ManualAddButton"),
                new(
                    "settings.scroll.content.footer_clearance",
                    "Control",
                    "SafeMargin/Scroll/Content/FooterClearance"),
                new(
                    "settings.footer",
                    "Control",
                    "Footer"),
                new(
                    "settings.footer.save_and_exit_button",
                    "Button",
                    "Footer/SaveAndExitButton"),
            ],
        };

    internal static IReadOnlyList<FallbackBindingRequirement> GetFallbackBindings(
        ScreenId screenId,
        ThemeScreenBindingContract bindingContract)
    {
        ArgumentNullException.ThrowIfNull(bindingContract);
        var result = new List<FallbackBindingRequirement>(bindingContract.Bindings.Length + 12);
        result.AddRange(bindingContract.Bindings.Select(binding => new FallbackBindingRequirement(
            binding.Id,
            binding.Types,
            binding.FallbackPath,
            binding.Required)));

        if (RuntimeBindings.TryGetValue(screenId, out RuntimeBindingRequirement[]? runtimeBindings))
        {
            result.AddRange(runtimeBindings.Select(binding => new FallbackBindingRequirement(
                binding.Id,
                [binding.Type],
                binding.FallbackPath,
                binding.Required)));
        }

        return result;
    }

    public static bool TryGetByBindingId(string bindingId, out ThemeScreenDescriptor descriptor)
    {
        foreach (ThemeScreenDescriptor candidate in Screens)
        {
            if (candidate.BindingId.Equals(bindingId, StringComparison.Ordinal))
            {
                descriptor = candidate;
                return true;
            }
        }

        descriptor = default;
        return false;
    }

    public static ThemeScreenSceneFailure ValidateScene(
        PackedScene? packedScene,
        ThemeScreenDescriptor descriptor,
        ThemeScreenBindingContract? bindingContract = null)
        => ValidateScene(packedScene, descriptor, bindingContract, out _);

    public static ThemeScreenSceneFailure ValidateScene(
        PackedScene? packedScene,
        ThemeScreenDescriptor descriptor,
        ThemeScreenBindingContract? bindingContract,
        out bool selfContainedVisuals)
    {
        selfContainedVisuals = false;
        if (packedScene is null)
        {
            return ThemeScreenSceneFailure.MissingScene;
        }

        Node root = packedScene.Instantiate();
        try
        {
            if (root is not Control)
            {
                return ThemeScreenSceneFailure.WrongRootType;
            }

            if (!root.Name.ToString().Equals(descriptor.RootName, StringComparison.Ordinal))
            {
                return ThemeScreenSceneFailure.WrongRootName;
            }

            if (!TryReadMetadata(root, ScreenBindingMetadata, out string screenBinding)
                || !screenBinding.Equals(descriptor.BindingId, StringComparison.Ordinal))
            {
                return ThemeScreenSceneFailure.MissingScreenBinding;
            }

            if (!TryReadOptionalBooleanMetadata(
                    root,
                    SelfContainedVisualsMetadata,
                    out selfContainedVisuals))
            {
                return ThemeScreenSceneFailure.InvalidSelfContainedMetadata;
            }

            if (selfContainedVisuals)
            {
                var themedRoot = (Control)root;
                if (themedRoot.Theme is null)
                {
                    return ThemeScreenSceneFailure.MissingSelfContainedTheme;
                }

                TextureRect? background = root.GetNodeOrNull<TextureRect>(SelfContainedBackgroundNode);
                if (background?.Texture is null)
                {
                    return ThemeScreenSceneFailure.MissingSelfContainedBackground;
                }

                CanvasItem? decoration = root.GetNodeOrNull<CanvasItem>(SelfContainedDecorationNode);
                if (decoration is null)
                {
                    return ThemeScreenSceneFailure.MissingSelfContainedDecoration;
                }
            }

            var elementBindings = new Dictionary<string, Node>(StringComparer.Ordinal);
            foreach (Node node in EnumerateTree(root))
            {
                if (!TryReadMetadata(node, ElementBindingMetadata, out string elementBinding))
                {
                    continue;
                }

                if (!IsValidElementBinding(descriptor.Key, elementBinding))
                {
                    return ThemeScreenSceneFailure.InvalidElementBinding;
                }

                if (!elementBindings.TryAdd(elementBinding, node))
                {
                    return ThemeScreenSceneFailure.DuplicateElementBinding;
                }
            }

            if (elementBindings.Count == 0 && bindingContract is null)
            {
                return ThemeScreenSceneFailure.MissingElementBinding;
            }

            if (bindingContract is not null)
            {
                var expectedBindings = new HashSet<string>(
                    bindingContract.Bindings.Select(binding => binding.Id),
                    StringComparer.Ordinal);
                if (RuntimeBindings.TryGetValue(
                        descriptor.ScreenId,
                        out RuntimeBindingRequirement[]? supplementalBindings))
                {
                    expectedBindings.UnionWith(
                        supplementalBindings.Select(requirement => requirement.Id));
                }

                if (elementBindings.Keys.Any(binding => !expectedBindings.Contains(binding)))
                {
                    return ThemeScreenSceneFailure.UnexpectedElementBinding;
                }

                foreach (ThemeBindingRequirement requirement in bindingContract.Bindings)
                {
                    if (!elementBindings.TryGetValue(requirement.Id, out Node? node))
                    {
                        continue;
                    }

                    if (!requirement.Types.Any(type => node.IsClass(type)))
                    {
                        return ThemeScreenSceneFailure.WrongBindingType;
                    }
                }
            }

            if (RuntimeBindings.TryGetValue(descriptor.ScreenId, out RuntimeBindingRequirement[]? runtimeBindings))
            {
                foreach (RuntimeBindingRequirement requirement in runtimeBindings)
                {
                    if (!elementBindings.TryGetValue(requirement.Id, out Node? node))
                    {
                        if (requirement.Required && string.IsNullOrWhiteSpace(requirement.FallbackPath))
                        {
                            return ThemeScreenSceneFailure.MissingRequiredBinding;
                        }

                        continue;
                    }

                    if (!node.IsClass(requirement.Type))
                    {
                        return ThemeScreenSceneFailure.WrongBindingType;
                    }
                }
            }

            return ThemeScreenSceneFailure.None;
        }
        finally
        {
            root.Free();
        }
    }

    private static ThemeScreenDescriptor Describe(ScreenId id, string key, string pascalName)
    {
        return new ThemeScreenDescriptor(
            id,
            $"screen.{key}",
            key,
            $"{ThemeDlcManifestValidator.ResourceRoot}scenes/screen_{key}.tscn",
            $"ThemeScreen_{pascalName}");
    }

    internal static bool IsValidElementBinding(string screenKey, string binding)
    {
        if (binding.Length is < 3 or > 240
            || !binding.StartsWith(screenKey + ".", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (char character in binding)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_'))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool IsCompatibleVisualComposition(
        bool selfContainedVisuals,
        string backgroundTexturePath,
        string backgroundDecorationPath)
    {
        return !selfContainedVisuals
            || (string.IsNullOrWhiteSpace(backgroundTexturePath)
                && string.IsNullOrWhiteSpace(backgroundDecorationPath));
    }

    private static bool TryReadMetadata(Node node, string key, out string value)
    {
        value = string.Empty;
        if (!node.HasMeta(key))
        {
            return false;
        }

        value = node.GetMeta(key).AsString().Trim();
        return value.Length > 0;
    }

    private static bool TryReadOptionalBooleanMetadata(Node node, string key, out bool value)
    {
        value = false;
        if (!node.HasMeta(key))
        {
            return true;
        }

        Variant metadata = node.GetMeta(key);
        if (metadata.VariantType != Variant.Type.Bool)
        {
            return false;
        }

        value = metadata.AsBool();
        return true;
    }

    private static IEnumerable<Node> EnumerateTree(Node root)
    {
        yield return root;
        foreach (Node child in root.GetChildren())
        {
            foreach (Node descendant in EnumerateTree(child))
            {
                yield return descendant;
            }
        }
    }
}
