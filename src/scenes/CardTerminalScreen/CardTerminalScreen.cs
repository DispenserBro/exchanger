using Exchanger.Core.Session;
using Exchanger.Core.Configuration;
using Exchanger.Core.Theming;
using Godot;
using System;

namespace Exchanger.Scenes.CardTerminalScreen;

public partial class CardTerminalScreen : ThemeBindableScreen
{
    protected override string ThemeScreenKey => "card_terminal";

    private Button _backButton = null!;
    private Button _homeNavigationButton = null!;
    private Button _cancelButton = null!;
    private Button _approveButton = null!;
    private Button _declineButton = null!;
    private Button _dispenseFailureButton = null!;
    private Button _partialDispenseButton = null!;
    private Button _connectionLossButton = null!;
    private Label _amountLabel = null!;
    private Label _tokensLabel = null!;
    private Label _countdownLabel = null!;
    private Control _debugPanel = null!;
    private ThemeManager _themeManager = null!;
    private AnimatedBannerTextSettings _bannerTexts = new();

    public event Action? BackRequested;

    public event Action? HomeRequested;

    public event Action? CancelRequested;

    public event Action? DebugApproveRequested;

    public event Action? DebugDeclineRequested;

    public event Action? DebugDispenseFailureRequested;

    public event Action? DebugPartialDispenseRequested;

    public event Action? DebugConnectionLossRequested;

    public override void _Ready()
    {
        _backButton = GetNode<Button>("SafeMargin/Content/TopBar/BackButton");
        _homeNavigationButton = GetNode<Button>("SafeMargin/Content/TopBar/HomeNavigationButton");
        _cancelButton = GetNode<Button>("SafeMargin/Content/CancelButton");
        _amountLabel = GetNode<Label>("SafeMargin/Content/Amount");
        _tokensLabel = GetNode<Label>("SafeMargin/Content/Tokens");
        _countdownLabel = GetNode<Label>("SafeMargin/Content/Countdown");
        _debugPanel = GetNode<Control>("SafeMargin/Content/DebugPanel");
        _approveButton = GetNode<Button>("SafeMargin/Content/DebugPanel/ApproveButton");
        _declineButton = GetNode<Button>("SafeMargin/Content/DebugPanel/DeclineButton");
        _dispenseFailureButton = GetNode<Button>("SafeMargin/Content/DebugPanel/DispenseFailureButton");
        _partialDispenseButton = GetNode<Button>("SafeMargin/Content/DebugPanel/PartialDispenseButton");
        _connectionLossButton = GetNode<Button>("SafeMargin/Content/DebugPanel/ConnectionLossButton");

        _themeManager = GetNode<ThemeManager>("/root/ThemeManager");
        _themeManager.ApplyTo(this);
        _themeManager.ThemeChanged += OnThemeChanged;

        _backButton.Pressed += OnBackPressed;
        _homeNavigationButton.Pressed += OnHomePressed;
        _cancelButton.Pressed += OnCancelPressed;
        _approveButton.Pressed += OnApprovePressed;
        _declineButton.Pressed += OnDeclinePressed;
        _dispenseFailureButton.Pressed += OnDispenseFailurePressed;
        _partialDispenseButton.Pressed += OnPartialDispensePressed;
        _connectionLossButton.Pressed += OnConnectionLossPressed;
        _debugPanel.Visible = OS.IsDebugBuild();
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
        }

        if (GodotObject.IsInstanceValid(_cancelButton))
        {
            _cancelButton.Pressed -= OnCancelPressed;
            _approveButton.Pressed -= OnApprovePressed;
            _declineButton.Pressed -= OnDeclinePressed;
            _dispenseFailureButton.Pressed -= OnDispenseFailurePressed;
            _partialDispenseButton.Pressed -= OnPartialDispensePressed;
            _connectionLossButton.Pressed -= OnConnectionLossPressed;
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

    public void ShowPayment(int amountRubles, int baseTokens, int bonusTokens)
    {
        int totalTokens = baseTokens + bonusTokens;
        _amountLabel.Text = $"К ОПЛАТЕ: {amountRubles} ₽  •  {TokenCountFormatter.FormatUpper(totalTokens)}";
        _tokensLabel.Text = string.Empty;
    }

    public void Configure(AnimatedBannerTextSettings? bannerTexts)
    {
        _bannerTexts = bannerTexts ?? new AnimatedBannerTextSettings();
    }

    public void SetCountdown(int seconds)
    {
        _countdownLabel.Text = seconds > 0
            ? _bannerTexts.CardTerminalCountdownTemplate.Replace("{seconds}", seconds.ToString(), StringComparison.OrdinalIgnoreCase)
            : string.Empty;
    }

    public void SetDebugControlsVisible(bool visible)
    {
        if (IsNodeReady())
        {
            _debugPanel.Visible = visible && OS.IsDebugBuild();
        }
    }

    private void OnBackPressed() => BackRequested?.Invoke();

    private void OnHomePressed() => HomeRequested?.Invoke();

    private void OnCancelPressed() => CancelRequested?.Invoke();

    private void OnApprovePressed() => DebugApproveRequested?.Invoke();

    private void OnDeclinePressed() => DebugDeclineRequested?.Invoke();

    private void OnDispenseFailurePressed() => DebugDispenseFailureRequested?.Invoke();

    private void OnPartialDispensePressed() => DebugPartialDispenseRequested?.Invoke();

    private void OnConnectionLossPressed() => DebugConnectionLossRequested?.Invoke();

    private void OnThemeChanged(VendingThemeDefinition definition)
    {
        _themeManager.ApplyTo(this);
    }
}
