using Exchanger.Core.Configuration;
using Exchanger.Core.Session;
using Exchanger.Core.Theming;
using Godot;
using System;
using System.Collections.Generic;

namespace Exchanger.Scenes.CardCustomAmountScreen;

public partial class CardCustomAmountScreen : ThemeBindableScreen
{
    protected override string ThemeScreenKey => "card_custom_amount";

    private readonly List<(Button Button, Action Handler)> _digitHandlers = new();
    private Button _backButton = null!;
    private Button _homeNavigationButton = null!;
    private Button _clearButton = null!;
    private Button _backspaceButton = null!;
    private Button _confirmButton = null!;
    private Label _baseTokensLabel = null!;
    private Label _bonusTokensLabel = null!;
    private Label _inputAmountLabel = null!;
    private Label _hintLabel = null!;
    private ThemeManager _themeManager = null!;
    private PricingSettings _pricing = new();
    private AnimatedBannerTextSettings _bannerTexts = new();
    private int? _maximumAvailableTokens;
    private string _digits = string.Empty;

    public event Action? BackRequested;

    public event Action? HomeRequested;

    public event Action<int>? AmountConfirmed;

    public override void _Ready()
    {
        _backButton = GetNode<Button>("SafeMargin/Content/TopBar/BackButton");
        _homeNavigationButton = GetNode<Button>("SafeMargin/Content/TopBar/HomeNavigationButton");
        _baseTokensLabel = GetNode<Label>("SafeMargin/Content/AmountPanel/Margin/Values/Amount");
        _bonusTokensLabel = GetNode<Label>("SafeMargin/Content/AmountPanel/Margin/Values/Tokens");
        _inputAmountLabel = GetNode<Label>("SafeMargin/Content/KeypadPanel/Content/InputValue");
        _hintLabel = GetNode<Label>("SafeMargin/Content/Hint");
        _clearButton = GetNode<Button>("SafeMargin/Content/KeypadPanel/Content/Keypad/ClearButton");
        _backspaceButton = GetNode<Button>("SafeMargin/Content/KeypadPanel/Content/Keypad/BackspaceButton");
        _confirmButton = GetNode<Button>("SafeMargin/Content/ConfirmButton");
        if (!UsesSelfContainedThemeScene)
        {
            ConfigureFallbackKeypadButton(_clearButton);
            ConfigureFallbackKeypadButton(_backspaceButton);
        }

        _themeManager = GetNode<ThemeManager>("/root/ThemeManager");
        _themeManager.ApplyTo(this);
        _themeManager.ThemeChanged += OnThemeChanged;

        _backButton.Pressed += OnBackPressed;
        _homeNavigationButton.Pressed += OnHomePressed;
        _clearButton.Pressed += Backspace;
        _backspaceButton.Pressed += Backspace;
        _confirmButton.Pressed += Confirm;

        for (int digit = 0; digit <= 9; digit++)
        {
            int capturedDigit = digit;
            Button button = GetNode<Button>($"SafeMargin/Content/KeypadPanel/Content/Keypad/Digit{digit}");
            if (!UsesSelfContainedThemeScene)
            {
                ConfigureFallbackKeypadButton(button);
            }

            Action handler = () => AppendDigit(capturedDigit);
            button.Pressed += handler;
            _digitHandlers.Add((button, handler));
        }

        ResetInput();
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
            _clearButton.Pressed -= Backspace;
            _backspaceButton.Pressed -= Backspace;
            _confirmButton.Pressed -= Confirm;
        }

        foreach ((Button button, Action handler) in _digitHandlers)
        {
            if (GodotObject.IsInstanceValid(button))
            {
                button.Pressed -= handler;
            }
        }
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
            ResetInput();
        }
    }

    public void SetTokenPurchaseLimit(int? maximumAvailableTokens)
    {
        _maximumAvailableTokens = NormalizeTokenLimit(maximumAvailableTokens);
        if (IsNodeReady())
        {
            UpdateDisplay();
        }
    }

    public void ResetInput()
    {
        _digits = string.Empty;
        UpdateDisplay();
    }

    private void AppendDigit(int digit)
    {
        string candidate = _digits + digit;
        if (!int.TryParse(candidate, out int amount) || amount > _pricing.MaxCardAmountRubles)
        {
            _hintLabel.Text = $"Максимальная сумма: {_pricing.MaxCardAmountRubles} ₽";
            return;
        }

        _digits = amount == 0 ? string.Empty : amount.ToString();
        UpdateDisplay();
    }

    private void Backspace()
    {
        if (_digits.Length > 0)
        {
            _digits = _digits[..^1];
        }

        UpdateDisplay();
    }

    private void Confirm()
    {
        if (TryGetValidAmount(out int amount))
        {
            AmountConfirmed?.Invoke(amount);
        }
    }

    private void UpdateDisplay()
    {
        int amount = int.TryParse(_digits, out int parsed) ? parsed : 0;
        CardCustomAmountDisplayText display = CardCustomAmountDisplayFormatter.Format(_pricing, amount);
        _inputAmountLabel.Text = display.InputAmount;
        _baseTokensLabel.Text = display.BaseTokens;
        _bonusTokensLabel.Text = display.BonusTokens;

        bool valid = TryGetValidAmount(out _);
        _confirmButton.Disabled = !valid;
        bool pricingValid = PricingPolicy.IsAmountValid(_pricing, amount);
        _hintLabel.Visible = true;
        _hintLabel.Text = string.IsNullOrEmpty(_digits)
            ? _bannerTexts.CardCustomAmount
            : valid
            ? "Сумма готова к подтверждению"
            : pricingValid && !PricingPolicy.IsWithinTokenLimit(_pricing, amount, _maximumAvailableTokens)
                ? $"Доступно не более {Math.Max(0, _maximumAvailableTokens ?? 0)} жетонов"
                : $"От {_pricing.TokenPriceRubles} до {_pricing.MaxCardAmountRubles} ₽, шаг {_pricing.CustomAmountStepRubles} ₽";
    }

    private bool TryGetValidAmount(out int amount)
    {
        amount = int.TryParse(_digits, out int parsed) ? parsed : 0;
        return PricingPolicy.IsAmountValid(_pricing, amount)
               && PricingPolicy.IsWithinTokenLimit(_pricing, amount, _maximumAvailableTokens);
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

    private static void ConfigureFallbackKeypadButton(Button button)
    {
        button.CustomMinimumSize = new Vector2(145f, 82f);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.ThemeTypeVariation = ThemeSemanticTypes.CompactButton;
    }

    private static int? NormalizeTokenLimit(int? maximumAvailableTokens) =>
        maximumAvailableTokens is null ? null : Math.Max(0, maximumAvailableTokens.Value);
}
