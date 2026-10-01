using Exchanger.Core.Configuration;
using Exchanger.Core.Theming;
using Godot;
using System;
using System.Collections.Generic;

namespace Exchanger.Scenes.ServiceAccessScreen;

public partial class ServiceAccessScreen : ThemeBindableScreen
{
    protected override string ThemeScreenKey => "service_access";

    private readonly List<Button> _digitButtons = new();
    private readonly List<Action> _digitHandlers = new();
    private Label _header = null!;
    private Label _description = null!;
    private Label _pinLabel = null!;
    private Label _pinDisplay = null!;
    private Label _status = null!;
    private Button _clearButton = null!;
    private Button _eraseButton = null!;
    private Button _enterButton = null!;
    private Button _cancelButton = null!;
    private Button _homeNavigationButton = null!;
    private ThemeManager _themeManager = null!;
    private ServiceAccessPolicy? _policy;
    private EntryMode _mode;
    private string _pin = string.Empty;
    private string _pendingPin = string.Empty;

    public event Action<ServiceAccessLevel>? Authorized;

    public event Action<string>? PinCreated;

    public event Action? Cancelled;

    public override void _Ready()
    {
        _header = GetNode<Label>("SafeMargin/Content/Header");
        _description = GetNode<Label>("SafeMargin/Content/Description");
        _pinLabel = GetNode<Label>("SafeMargin/Content/AccessPanel/Fields/PinLabel");
        _pinDisplay = GetNode<Label>("SafeMargin/Content/AccessPanel/Fields/PinDisplay");
        _status = GetNode<Label>("SafeMargin/Content/AccessPanel/Fields/Status");
        _clearButton = GetNode<Button>("SafeMargin/Content/AccessPanel/Fields/Keypad/Clear");
        _eraseButton = GetNode<Button>("SafeMargin/Content/AccessPanel/Fields/Keypad/Erase");
        _enterButton = GetNode<Button>("SafeMargin/Content/AccessPanel/Fields/EnterButton");
        _cancelButton = GetNode<Button>("SafeMargin/Content/CancelButton");
        _homeNavigationButton = GetNode<Button>("SafeMargin/Content/HomeNavigationButton");
        if (!UsesSelfContainedThemeScene)
        {
            ConfigureFallbackKeypadButton(_clearButton);
            ConfigureFallbackKeypadButton(_eraseButton);
        }

        for (int digit = 0; digit <= 9; digit++)
        {
            int captured = digit;
            Button button = GetNode<Button>($"SafeMargin/Content/AccessPanel/Fields/Keypad/Digit{digit}");
            if (!UsesSelfContainedThemeScene)
            {
                ConfigureFallbackKeypadButton(button);
            }

            Action handler = () => AppendDigit(captured);
            button.Pressed += handler;
            _digitButtons.Add(button);
            _digitHandlers.Add(handler);
        }

        _clearButton.Pressed += ClearPin;
        _eraseButton.Pressed += EraseDigit;
        _enterButton.Pressed += SubmitPin;
        _cancelButton.Pressed += OnCancelPressed;
        _homeNavigationButton.Pressed += OnCancelPressed;

        _themeManager = GetNode<ThemeManager>("/root/ThemeManager");
        _themeManager.ApplyTo(this);
        _themeManager.ThemeChanged += OnThemeChanged;
    }

    public override void _ExitTree()
    {
        for (int index = 0; index < _digitButtons.Count; index++)
        {
            _digitButtons[index].Pressed -= _digitHandlers[index];
        }

        if (GodotObject.IsInstanceValid(_clearButton))
        {
            _clearButton.Pressed -= ClearPin;
            _eraseButton.Pressed -= EraseDigit;
            _enterButton.Pressed -= SubmitPin;
            _cancelButton.Pressed -= OnCancelPressed;
            _homeNavigationButton.Pressed -= OnCancelPressed;
        }

        if (GodotObject.IsInstanceValid(_themeManager))
        {
            _themeManager.ThemeChanged -= OnThemeChanged;
        }
    }

    public void Begin(ServiceAccessPolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _mode = policy.IsAvailable ? EntryMode.Verify : EntryMode.Create;
        _pin = string.Empty;
        _pendingPin = string.Empty;
        ConfigureMode();
        RefreshPinDisplay();
    }

    public void ShowPersistenceError()
    {
        _mode = EntryMode.Create;
        _pin = string.Empty;
        _pendingPin = string.Empty;
        ConfigureMode();
        _status.Text = "Не удалось сохранить PIN-код. Попробуйте ещё раз.";
        RefreshPinDisplay();
    }

    private void ConfigureMode()
    {
        switch (_mode)
        {
            case EntryMode.Create:
                _header.Text = "СОЗДАНИЕ PIN";
                _description.Text = "Придумайте PIN-код для защиты настроек.";
                _pinLabel.Text = "НОВЫЙ PIN";
                _status.Text = $"Введите от {ServiceAccessPolicy.MinimumPinLength} до {ServiceAccessPolicy.MaximumPinLength} цифр.";
                _enterButton.Text = "ПРОДОЛЖИТЬ";
                break;
            case EntryMode.Confirm:
                _header.Text = "ПОДТВЕРЖДЕНИЕ PIN";
                _description.Text = "Введите новый PIN-код ещё раз.";
                _pinLabel.Text = "ПОВТОРИТЕ PIN";
                _status.Text = "Введите тот же PIN ещё раз.";
                _enterButton.Text = "СОХРАНИТЬ И ВОЙТИ";
                break;
            default:
                _header.Text = "ВХОД В НАСТРОЙКИ";
                _description.Text = "Введите PIN-код, чтобы открыть настройки.";
                _pinLabel.Text = "PIN-КОД";
                _status.Text = "После пяти ошибок вход блокируется на 30 секунд.";
                _enterButton.Text = "ВОЙТИ";
                break;
        }

        _enterButton.Disabled = false;
    }

    private void AppendDigit(int digit)
    {
        if (_pin.Length >= ServiceAccessPolicy.MaximumPinLength || _policy is null)
        {
            return;
        }

        _pin += digit.ToString();
        RefreshPinDisplay();
    }

    private void ClearPin()
    {
        _pin = string.Empty;
        RefreshPinDisplay();
    }

    private void EraseDigit()
    {
        if (_pin.Length > 0)
        {
            _pin = _pin[..^1];
        }

        RefreshPinDisplay();
    }

    private void SubmitPin()
    {
        if (_policy is null)
        {
            return;
        }

        if (_mode == EntryMode.Create)
        {
            if (!ServiceAccessPolicy.IsValidPin(_pin))
            {
                _status.Text = $"PIN должен содержать от {ServiceAccessPolicy.MinimumPinLength} до {ServiceAccessPolicy.MaximumPinLength} цифр.";
                return;
            }

            _pendingPin = _pin;
            _pin = string.Empty;
            _mode = EntryMode.Confirm;
            ConfigureMode();
            RefreshPinDisplay();
            return;
        }

        if (_mode == EntryMode.Confirm)
        {
            if (!string.Equals(_pin, _pendingPin, StringComparison.Ordinal))
            {
                _pin = string.Empty;
                _pendingPin = string.Empty;
                _mode = EntryMode.Create;
                ConfigureMode();
                _status.Text = "PIN не совпали. Задайте новый PIN ещё раз.";
                RefreshPinDisplay();
                return;
            }

            string createdPin = _pin;
            _pin = string.Empty;
            _pendingPin = string.Empty;
            RefreshPinDisplay();
            PinCreated?.Invoke(createdPin);
            return;
        }

        ServiceAccessResult result = _policy.Verify(_pin);
        _pin = string.Empty;
        RefreshPinDisplay();

        switch (result.Status)
        {
            case ServiceAccessStatus.Authorized:
                _status.Text = "Настройки открыты.";
                Authorized?.Invoke(result.Level);
                break;
            case ServiceAccessStatus.InvalidPin:
                _status.Text = $"Неверный PIN. Осталось попыток: {result.RemainingAttempts}.";
                break;
            case ServiceAccessStatus.Locked:
                _status.Text = $"Вход временно заблокирован. Повторите через {result.LockSeconds} сек.";
                break;
            default:
                _status.Text = "Не удалось проверить PIN-код. Задайте новый PIN-код.";
                break;
        }
    }

    private void RefreshPinDisplay() => _pinDisplay.Text = _pin.Length == 0
        ? "—"
        : new string('●', _pin.Length);

    private void OnCancelPressed()
    {
        _pendingPin = string.Empty;
        ClearPin();
        Cancelled?.Invoke();
    }

    private void OnThemeChanged(VendingThemeDefinition definition) => _themeManager.ApplyTo(this);

    private static void ConfigureFallbackKeypadButton(Button button)
    {
        button.CustomMinimumSize = new Vector2(170f, 76f);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.ThemeTypeVariation = ThemeSemanticTypes.CompactButton;
    }

    private enum EntryMode
    {
        Verify,
        Create,
        Confirm,
    }
}
