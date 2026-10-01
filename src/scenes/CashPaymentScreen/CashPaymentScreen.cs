using Exchanger.Core.Configuration;
using Exchanger.Core.Session;
using Exchanger.Core.Theming;
using Godot;
using System;

namespace Exchanger.Scenes.CashPaymentScreen;

public partial class CashPaymentScreen : ThemeBindableScreen
{
    protected override string ThemeScreenKey => "cash_payment";

    private Button _backButton = null!;
    private Button _homeNavigationButton = null!;
    private Label _balanceLabel = null!;
    private Label _tokensLabel = null!;
    private Label _bonusLabel = null!;
    private Label _bannerTextLabel = null!;
    private Button _dispenseButton = null!;
    private Button _add10Button = null!;
    private Button _add50Button = null!;
    private Button _add100Button = null!;
    private Control _debugPanel = null!;
    private ThemeManager _themeManager = null!;
    private PricingSettings _pricing = new();
    private AnimatedBannerTextSettings _bannerTexts = new();
    private int _balanceRubles;
    private int? _maximumAvailableTokens;

    public event Action? BackRequested;

    public event Action? HomeRequested;

    public event Action<int>? MockCashRequested;

    public event Action? DispenseRequested;

    public override void _Ready()
    {
        _backButton = GetNode<Button>("SafeMargin/Content/TopBar/BackButton");
        _homeNavigationButton = GetNode<Button>("SafeMargin/Content/TopBar/HomeNavigationButton");
        _balanceLabel = GetNode<Label>("SafeMargin/Content/Summary/Margin/Values/Balance");
        _tokensLabel = GetNode<Label>("SafeMargin/Content/Summary/Margin/Values/Tokens");
        _bonusLabel = GetNode<Label>("SafeMargin/Content/Summary/Margin/Values/Bonus");
        _bannerTextLabel = UsesExternalThemeScene
            ? GetThemeBinding<Label>("cash_payment.content.banner_text")
            : GetNode<Label>("SafeMargin/Content/BannerText");
        _dispenseButton = GetNode<Button>("SafeMargin/Content/DispenseButton");
        _debugPanel = GetNode<Control>("SafeMargin/Content/DebugPanel");
        _add10Button = GetNode<Button>("SafeMargin/Content/DebugPanel/Add10Button");
        _add50Button = GetNode<Button>("SafeMargin/Content/DebugPanel/Add50Button");
        _add100Button = GetNode<Button>("SafeMargin/Content/DebugPanel/Add100Button");

        _themeManager = GetNode<ThemeManager>("/root/ThemeManager");
        _themeManager.ApplyTo(this);
        _themeManager.ThemeChanged += OnThemeChanged;
        _backButton.Pressed += OnBackPressed;
        _homeNavigationButton.Pressed += OnHomePressed;
        _dispenseButton.Pressed += OnDispensePressed;
        _add10Button.Pressed += OnAdd10Pressed;
        _add50Button.Pressed += OnAdd50Pressed;
        _add100Button.Pressed += OnAdd100Pressed;
        _debugPanel.Visible = OS.IsDebugBuild();
        UpdateSummary();
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
            _dispenseButton.Pressed -= OnDispensePressed;
            _add10Button.Pressed -= OnAdd10Pressed;
            _add50Button.Pressed -= OnAdd50Pressed;
            _add100Button.Pressed -= OnAdd100Pressed;
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
        SetBalance(0);
    }

    public void SetTokenPurchaseLimit(int? maximumAvailableTokens)
    {
        _maximumAvailableTokens = NormalizeTokenLimit(maximumAvailableTokens);
        if (IsNodeReady())
        {
            UpdateSummary();
        }
    }

    public void SetBalance(int balanceRubles)
    {
        _balanceRubles = Math.Max(0, balanceRubles);
        if (IsNodeReady())
        {
            UpdateSummary();
        }
    }

    public void SetDebugControlsVisible(bool visible)
    {
        if (IsNodeReady())
        {
            _debugPanel.Visible = visible && OS.IsDebugBuild();
        }
    }

    private void UpdateSummary()
    {
        CashPaymentSummaryText summary = CashPaymentSummaryFormatter.Format(_pricing, _balanceRubles);
        _balanceLabel.Text = summary.Balance;
        _tokensLabel.Text = summary.BaseTokens;
        _bonusLabel.Text = summary.Bonus;
        _bannerTextLabel.Text = _bannerTexts.CashPayment;
        _bannerTextLabel.Visible = true;
        bool hasMinimumAmount = _balanceRubles >= _pricing.TokenPriceRubles;
        bool withinInventory = PricingPolicy.IsWithinTokenLimit(
            _pricing,
            _balanceRubles,
            _maximumAvailableTokens);
        _dispenseButton.Disabled = !hasMinimumAmount || !withinInventory;
        if (hasMinimumAmount && !withinInventory)
        {
            _bonusLabel.Text = "НЕДОСТАТОЧНО ЖЕТОНОВ";
        }
    }

    private void OnBackPressed()
    {
        BackRequested?.Invoke();
    }

    private void OnHomePressed() => HomeRequested?.Invoke();

    private void OnDispensePressed() => DispenseRequested?.Invoke();

    private void OnAdd10Pressed() => MockCashRequested?.Invoke(10);

    private void OnAdd50Pressed() => MockCashRequested?.Invoke(50);

    private void OnAdd100Pressed() => MockCashRequested?.Invoke(100);

    private void OnThemeChanged(VendingThemeDefinition definition)
    {
        _themeManager.ApplyTo(this);
    }

    private static int? NormalizeTokenLimit(int? maximumAvailableTokens) =>
        maximumAvailableTokens is null ? null : Math.Max(0, maximumAvailableTokens.Value);
}
