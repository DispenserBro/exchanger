using Exchanger.Core.Theming;
using Godot;
using System;

namespace Exchanger.Scenes.ErrorScreen;

public partial class ErrorScreen : ThemeBindableScreen
{
    protected override string ThemeScreenKey => "error";

    private Label _titleLabel = null!;
    private Label _messageLabel = null!;
    private Label _countdownLabel = null!;
    private Button _homeButton = null!;
    private Button _homeNavigationButton = null!;
    private ThemeManager _themeManager = null!;

    public event Action? HomeRequested;

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("SafeMargin/Content/ErrorPanel/Margin/Values/Title");
        _messageLabel = GetNode<Label>("SafeMargin/Content/ErrorPanel/Margin/Values/Body/Message");
        _countdownLabel = GetNode<Label>("SafeMargin/Content/Countdown");
        _homeButton = GetNode<Button>("SafeMargin/Content/HomeButton");
        _homeNavigationButton = GetNode<Button>("SafeMargin/Content/HomeNavigationButton");

        _themeManager = GetNode<ThemeManager>("/root/ThemeManager");
        _themeManager.ApplyTo(this);
        _themeManager.ThemeChanged += OnThemeChanged;
        _homeButton.Pressed += OnHomePressed;
        _homeNavigationButton.Pressed += OnHomePressed;
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_themeManager))
        {
            _themeManager.ThemeChanged -= OnThemeChanged;
        }

        if (GodotObject.IsInstanceValid(_homeButton))
        {
            _homeButton.Pressed -= OnHomePressed;
            _homeNavigationButton.Pressed -= OnHomePressed;
        }
    }

    public void ShowError(string title, string message)
    {
        _titleLabel.Text = title;
        _messageLabel.Text = message;
    }

    public void SetCountdown(int seconds)
    {
        _countdownLabel.Text = seconds > 0
            ? $"Возврат на главный экран через {seconds} сек."
            : string.Empty;
    }

    private void OnHomePressed() => HomeRequested?.Invoke();

    private void OnThemeChanged(VendingThemeDefinition definition)
    {
        _themeManager.ApplyTo(this);
    }
}
