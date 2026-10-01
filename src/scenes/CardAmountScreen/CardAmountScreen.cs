using Exchanger.Core.Configuration;
using Exchanger.Core.Session;
using Exchanger.Core.Theming;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Exchanger.Scenes.CardAmountScreen;

public partial class CardAmountScreen : ThemeBindableScreen
{
    protected override string ThemeScreenKey => "card_amount";
    private const int MaximumThemePresetSlots = 16;

    private Button _backButton = null!;
    private Button _homeNavigationButton = null!;
    private Button _otherAmountButton = null!;
    private GridContainer _presetGrid = null!;
    private Label _selectionLabel = null!;
    private Label _rewardLabel = null!;
    private Label _rewardBonusLabel = null!;
    private Button _confirmButton = null!;
    private ThemeManager _themeManager = null!;
    private readonly List<(Button Button, Action Handler)> _themePresetSlots = new();
    private PricingSettings _pricing = new();
    private AnimatedBannerTextSettings _bannerTexts = new();
    private int? _selectedAmount;
    private int? _maximumAvailableTokens;

    public event Action? BackRequested;

    public event Action? HomeRequested;

    public event Action? OtherAmountRequested;

    public event Action<int>? AmountSelected;

    public override void _Ready()
    {
        _backButton = GetNode<Button>("SafeMargin/Content/TopBar/BackButton");
        _homeNavigationButton = GetNode<Button>("SafeMargin/Content/TopBar/HomeNavigationButton");
        _presetGrid = GetNode<GridContainer>("SafeMargin/Content/AmountPanel/Margin/Content/PresetGrid");
        _otherAmountButton = GetNode<Button>("SafeMargin/Content/AmountPanel/Margin/Content/OtherAmountButton");
        _selectionLabel = GetNode<Label>("SafeMargin/Content/SelectionLabel");
        _rewardLabel = GetNode<Label>("SafeMargin/Content/RewardPanel/Content/Value");
        _rewardBonusLabel = UsesSelfContainedThemeScene
            ? GetThemeBinding<Label>("card_amount.content.reward_panel.content.bonus")
            : GetNode<Label>("SafeMargin/Content/RewardPanel/Content/Bonus");
        _confirmButton = GetNode<Button>("SafeMargin/Content/ConfirmButton");

        _themeManager = GetNode<ThemeManager>("/root/ThemeManager");
        _themeManager.ApplyTo(this);
        _themeManager.ThemeChanged += OnThemeChanged;

        _backButton.Pressed += OnBackPressed;
        _homeNavigationButton.Pressed += OnHomePressed;
        _otherAmountButton.Pressed += OnOtherAmountPressed;
        _confirmButton.Pressed += OnConfirmPressed;
        if (UsesSelfContainedThemeScene)
        {
            BindThemePresetSlots();
        }

        RebuildPresets();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_themeManager))
        {
            _themeManager.ThemeChanged -= OnThemeChanged;
        }

        if (GodotObject.IsInstanceValid(_backButton))
        {
            _backButton.Pressed -= OnBackPressed;
            _homeNavigationButton.Pressed -= OnHomePressed;
            _otherAmountButton.Pressed -= OnOtherAmountPressed;
            _confirmButton.Pressed -= OnConfirmPressed;
        }

        foreach ((Button button, Action handler) in _themePresetSlots)
        {
            if (GodotObject.IsInstanceValid(button))
            {
                button.Pressed -= handler;
            }
        }

        _themePresetSlots.Clear();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && @event.IsActionPressed("kiosk_back"))
        {
            OnBackPressed();
            GetViewport().SetInputAsHandled();
        }
    }

    public void Configure(
        PricingSettings pricing,
        int? maximumAvailableTokens = null,
        AnimatedBannerTextSettings? bannerTexts = null)
    {
        _pricing = pricing ?? throw new ArgumentNullException(nameof(pricing));
        _maximumAvailableTokens = NormalizeTokenLimit(maximumAvailableTokens);
        _bannerTexts = bannerTexts ?? new AnimatedBannerTextSettings();
        if (IsNodeReady())
        {
            RebuildPresets();
        }
    }

    public void SetTokenPurchaseLimit(int? maximumAvailableTokens)
    {
        _maximumAvailableTokens = NormalizeTokenLimit(maximumAvailableTokens);
        if (IsNodeReady())
        {
            RebuildPresets();
        }
    }

    private void RebuildPresets()
    {
        if (!IsNodeReady())
        {
            return;
        }

        if (UsesSelfContainedThemeScene)
        {
            UpdateThemePresetSlots();
        }
        else
        {
            RebuildFallbackPresetButtons();
        }

        _selectedAmount = null;
        _rewardLabel.Text = "ВЫБЕРИТЕ СУММУ";
        _rewardBonusLabel.Text = string.Empty;
        _selectionLabel.Text = _bannerTexts.CardAmount;
        _confirmButton.Disabled = true;

    }

    private void BindThemePresetSlots()
    {
        for (int index = 0; index < MaximumThemePresetSlots; index++)
        {
            int slotIndex = index;
            Button button = GetThemeBinding<Button>(
                $"card_amount.content.amount_panel.margin.content.preset_grid.slot_{index:00}");
            Action handler = () => OnThemePresetPressed(slotIndex);
            button.Pressed += handler;
            _themePresetSlots.Add((button, handler));
        }
    }

    private void UpdateThemePresetSlots()
    {
        PaymentPreset[] presets = _pricing.PaymentPresets ?? Array.Empty<PaymentPreset>();
        for (int index = 0; index < _themePresetSlots.Count; index++)
        {
            Button button = _themePresetSlots[index].Button;
            bool hasPreset = index < presets.Length;
            button.Visible = hasPreset;
            if (hasPreset)
            {
                button.Text = $"{presets[index].AmountRubles} РУБ.";
                button.Disabled = !CanPurchaseAmount(presets[index].AmountRubles);
            }
        }
    }

    private void OnThemePresetPressed(int index)
    {
        PaymentPreset[] presets = _pricing.PaymentPresets ?? Array.Empty<PaymentPreset>();
        if (index >= presets.Length)
        {
            return;
        }

        int amount = presets[index].AmountRubles;
        int tokens = amount / Math.Max(1, _pricing.TokenPriceRubles);
        OnAmountPressed(amount, tokens);
    }

    private void RebuildFallbackPresetButtons()
    {
        foreach (Node child in _presetGrid.GetChildren())
        {
            _presetGrid.RemoveChild(child);
            child.QueueFree();
        }

        foreach (PaymentPreset preset in _pricing.PaymentPresets ?? Array.Empty<PaymentPreset>())
        {
            int amount = preset.AmountRubles;
            int tokens = amount / Math.Max(1, _pricing.TokenPriceRubles);
            var button = new Button
            {
                Text = $"{amount} РУБ.",
                Disabled = !CanPurchaseAmount(amount),
                CustomMinimumSize = new Vector2(292f, 68f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                ThemeTypeVariation = ThemeSemanticTypes.CompactButton,
            };
            button.Pressed += () => OnAmountPressed(amount, tokens);
            _presetGrid.AddChild(button);
        }
    }

    private void OnAmountPressed(int amount, int tokens)
    {
        if (!CanPurchaseAmount(amount))
        {
            _selectedAmount = null;
            _rewardLabel.Text = "НЕДОСТАТОЧНО ЖЕТОНОВ";
            _rewardBonusLabel.Text = string.Empty;
            _selectionLabel.Text = "ВЫБЕРИТЕ МЕНЬШУЮ СУММУ";
            _confirmButton.Disabled = true;
            return;
        }

        int bonus = _pricing.BonusRules
            .Where(rule => amount >= rule.MinimumAmountRubles)
            .Select(rule => rule.BonusTokens)
            .DefaultIfEmpty(0)
            .Max();
        _selectedAmount = amount;
        _rewardLabel.Text = TokenCountFormatter.FormatUpper(tokens);
        _rewardBonusLabel.Text = TokenCountFormatter.FormatGiftUpper(bonus);
        _selectionLabel.Text = $"ВЫБРАНО: {amount} ₽";
        _confirmButton.Disabled = false;
    }

    private void OnConfirmPressed()
    {
        if (_selectedAmount is int amount)
        {
            AmountSelected?.Invoke(amount);
        }
    }

    private void OnOtherAmountPressed()
    {
        OtherAmountRequested?.Invoke();
    }

    private void OnBackPressed()
    {
        BackRequested?.Invoke();
    }

    private void OnHomePressed() => HomeRequested?.Invoke();

    private void OnThemeChanged(VendingThemeDefinition definition)
    {
        _themeManager.ApplyTo(this);
    }

    private bool CanPurchaseAmount(int amountRubles) =>
        PricingPolicy.IsWithinTokenLimit(_pricing, amountRubles, _maximumAvailableTokens);

    private static int? NormalizeTokenLimit(int? maximumAvailableTokens) =>
        maximumAvailableTokens is null ? null : Math.Max(0, maximumAvailableTokens.Value);
}
