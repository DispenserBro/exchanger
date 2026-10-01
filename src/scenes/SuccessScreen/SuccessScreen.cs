using Exchanger.Core.Session;
using Exchanger.Core.Theming;
using Godot;
using System;

namespace Exchanger.Scenes.SuccessScreen;

public partial class SuccessScreen : ThemeBindableScreen
{
    protected override string ThemeScreenKey => "success";

    private Label _titleLabel = null!;
    private Label _messageLabel = null!;
    private Label _countdownLabel = null!;
    private Button _homeButton = null!;
    private Button _homeNavigationButton = null!;
    private ThemeManager _themeManager = null!;

    public event Action? HomeRequested;

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("SafeMargin/Content/ResultPanel/Margin/Values/Title");
        _messageLabel = GetNode<Label>("SafeMargin/Content/ResultPanel/Margin/Values/Body/Message");
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

    public void ShowDispensing(int totalTokens)
    {
        _titleLabel.Text = "ВЫДАЧА ЖЕТОНОВ";
        _messageLabel.Text = $"Подождите, выдаём {TokenCountFormatter.Format(totalTokens)}…";
        _countdownLabel.Text = "Не закрывайте лоток выдачи";
        _homeButton.Visible = false;
        _homeNavigationButton.Disabled = true;
    }

    public void ShowDispenseProgress(int confirmedTokens, int totalTokens)
    {
        int safeTotal = Math.Max(1, totalTokens);
        int safeConfirmed = Math.Clamp(confirmedTokens, 0, safeTotal);
        _messageLabel.Text =
            $"Выдано: {TokenCountFormatter.Format(safeConfirmed)} из {TokenCountFormatter.Format(safeTotal)}…";
    }

    public void ShowCompleted(int baseTokens, int bonusTokens)
    {
        int total = baseTokens + bonusTokens;
        _titleLabel.Text = "ВЫДАЧА ЖЕТОНОВ";
        _messageLabel.Text = bonusTokens > 0
            ? $"ЗАБЕРИТЕ {TokenCountFormatter.FormatUpper(total)}\nИЗ НИХ {TokenCountFormatter.FormatUpper(bonusTokens)} В ПОДАРОК!"
            : $"ЗАБЕРИТЕ {TokenCountFormatter.FormatUpper(total)}\nСПАСИБО ЗА ПОКУПКУ!";
        _homeButton.Visible = true;
        _homeNavigationButton.Disabled = false;
    }

    public void SetCountdown(int seconds)
    {
        if (_homeButton.Visible)
        {
            _countdownLabel.Text = seconds > 0
                ? $"Возврат на главный экран через {seconds} сек."
                : string.Empty;
        }
    }

    private void OnHomePressed() => HomeRequested?.Invoke();

    private void OnThemeChanged(VendingThemeDefinition definition)
    {
        _themeManager.ApplyTo(this);
    }
}
