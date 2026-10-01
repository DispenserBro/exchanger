using Exchanger.Core.Configuration;
using Exchanger.Core.Inventory;
using Exchanger.Core.Logging;
using Exchanger.Core.Theming;
using Exchanger.Hardware.Abstractions;
using Exchanger.UI.TokenStockStatus;
using Godot;
using System;
using StockStatusControl = Exchanger.UI.TokenStockStatus.TokenStockStatus;
using AdvertisementPlayerControl = Exchanger.UI.AdvertisementPlayer.AdvertisementPlayer;
using AdvertisementPresenter = Exchanger.UI.AdvertisementPlayer.AdvertisementPresenter;
using ConnectionStatusControl = Exchanger.UI.ConnectionStatusIndicator.ConnectionStatusIndicator;

namespace Exchanger.Scenes.HomeScreen;

public partial class HomeScreen : ThemeBindableScreen
{
    protected override string ThemeScreenKey => "home";

    private Label _brandLabel = null!;
    private Label _speechTextLabel = null!;
    private Label _priceLabel = null!;
    private AdvertisementPlayerControl? _fallbackAdvertisementPlayer;
    private AdvertisementPresenter? _themeAdvertisementPresenter;
    private Control _advertisementPanel = null!;
    private Label _supportLabel = null!;
    private StockStatusControl? _fallbackStockStatus;
    private Control _stockStatusHost = null!;
    private CanvasItem? _customTextBlock;
    private CanvasItem? _rightCharacter;
    private ConnectionStatusControl? _fallbackConnectionStatus;
    private Label? _themeStockState;
    private Label? _themeStockApproximateCount;
    private Label _stockApproximateCount = null!;
    private Control? _themeStockEmpty;
    private Control? _themeStockLow;
    private Control? _themeStockEnough;
    private Control? _themeStockMuch;
    private Control? _themeConnectionStatus;
    private Label? _themeConnectionLabel;
    private Button _cashButton = null!;
    private Button _cardButton = null!;
    private ThemeManager _themeManager = null!;
    private AppSettings? _settings;
    private TokenAvailabilityLevel _availabilityLevel = TokenAvailabilityLevel.Unknown;
    private int _tokenInventoryCount;
    private MachineConnectionState _connectionState = MachineConnectionState.Disconnected;

    public event Action? CashRequested;

    public event Action? CardRequested;

    public override void _Ready()
    {
        _brandLabel = GetNode<Label>("SafeMargin/Scroll/Content/Footer/Margin/Content/Brand");
        _speechTextLabel = GetNode<Label>("SafeMargin/Scroll/Content/SpeechText");
        _priceLabel = GetNode<Label>("SafeMargin/Scroll/Content/PricePanel/Margin/Content/Price");
        _cashButton = GetNode<Button>("SafeMargin/Scroll/Content/PaymentPanel/Margin/Content/Buttons/CashButton");
        _cardButton = GetNode<Button>("SafeMargin/Scroll/Content/PaymentPanel/Margin/Content/Buttons/CardButton");
        _supportLabel = GetNode<Label>("SafeMargin/Scroll/Content/Footer/Margin/Content/Support");
        _stockStatusHost = GetNode<Control>("SafeMargin/Scroll/Content/StockStatus");
        _advertisementPanel = GetNode<Control>("SafeMargin/Scroll/Content/AdvertisementPanel");

        if (UsesSelfContainedThemeScene)
        {
            BindThemeRuntimeVisuals();
            _stockApproximateCount = _themeStockApproximateCount!;
            _customTextBlock = GetOptionalThemeBinding<CanvasItem>("home.decoration.custom_text_block");
            _rightCharacter = GetOptionalThemeBinding<CanvasItem>("home.decoration.right_character");
        }
        else
        {
            _fallbackStockStatus = GetNode<StockStatusControl>("SafeMargin/Scroll/Content/StockStatus");
            _fallbackConnectionStatus = GetNode<ConnectionStatusControl>(
                "SafeMargin/Scroll/Content/Footer/Margin/Content/ConnectionStatus");
            _fallbackAdvertisementPlayer = GetNode<AdvertisementPlayerControl>(
                "SafeMargin/Scroll/Content/AdvertisementPanel/Layer/Content");
            _stockApproximateCount = GetNode<Label>(
                "SafeMargin/Scroll/Content/StockStatus/Margin/Content/AvailabilityRow/ApproximateCount");
        }

        _themeManager = GetNode<ThemeManager>("/root/ThemeManager");
        _themeManager.ApplyTo(this);
        _themeManager.ThemeChanged += OnThemeChanged;

        _cashButton.Pressed += OnCashPressed;
        _cardButton.Pressed += OnCardPressed;
        _fallbackConnectionStatus?.Configure(showReadyState: false);

        ApplySettings();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_themeManager))
        {
            _themeManager.ThemeChanged -= OnThemeChanged;
        }

        if (GodotObject.IsInstanceValid(_cashButton))
        {
            _cashButton.Pressed -= OnCashPressed;
            _cardButton.Pressed -= OnCardPressed;
        }

        _themeAdvertisementPresenter?.Dispose();
        _themeAdvertisementPresenter = null;
    }

    public void Configure(AppSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        if (IsNodeReady())
        {
            ApplySettings();
        }
    }

    public void SetStockLevel(MachineStockLevel stockLevel) =>
        SetTokenAvailability(stockLevel switch
        {
            MachineStockLevel.Enough => TokenAvailabilityLevel.Much,
            MachineStockLevel.Low => TokenAvailabilityLevel.Low,
            _ => TokenAvailabilityLevel.Unknown,
        }, _tokenInventoryCount);

    public void SetTokenAvailability(TokenAvailabilityLevel level) =>
        SetTokenAvailability(level, _tokenInventoryCount);

    public void SetTokenAvailability(TokenAvailabilityLevel level, int totalCount)
    {
        _availabilityLevel = level;
        _tokenInventoryCount = Math.Max(0, totalCount);
        if (!IsNodeReady())
        {
            return;
        }

        TokenStockState state = level switch
        {
            TokenAvailabilityLevel.Empty => TokenStockState.Empty,
            TokenAvailabilityLevel.Low => TokenStockState.Low,
            TokenAvailabilityLevel.Enough => TokenStockState.Enough,
            TokenAvailabilityLevel.Much => TokenStockState.Much,
            _ => TokenStockState.Unknown,
        };
        if (_themeStockState is not null)
        {
            _themeStockState.Text = TokenInventoryPolicy.FormatAvailability(level);
            _themeStockApproximateCount!.Text =
                TokenInventoryPolicy.FormatApproximateCount(_tokenInventoryCount);
            SetThemeStockVisuals(state);
        }
        else
        {
            _fallbackStockStatus!.SetLevel(
                state,
                TokenInventoryPolicy.FormatApproximateCount(_tokenInventoryCount));
        }

        ApplyPaymentAvailability();
    }

    public void SetConnectionState(MachineConnectionState state)
    {
        if (!IsNodeReady())
        {
            return;
        }

        _connectionState = state;
        if (_themeConnectionLabel is not null && _themeConnectionStatus is not null)
        {
            _themeConnectionStatus.Visible = state != MachineConnectionState.Ready;
            _themeConnectionLabel.Text = state switch
            {
                MachineConnectionState.Authenticating => "СЕРВИС: АВТОРИЗАЦИЯ КОНТРОЛЛЕРА…",
                MachineConnectionState.Ready => "СЕРВИС: КОНТРОЛЛЕР ПОДКЛЮЧЁН",
                MachineConnectionState.Faulted => "СЕРВИС: КОНТРОЛЛЕР НЕДОСТУПЕН",
                _ => "СЕРВИС: НЕТ СВЯЗИ С КОНТРОЛЛЕРОМ",
            };
        }
        else
        {
            _fallbackConnectionStatus!.SetState(state);
        }

        ApplyPaymentAvailability();
    }

    private void ApplySettings()
    {
        if (_settings is null || !IsNodeReady())
        {
            return;
        }

        _brandLabel.Text = _settings.Branding.ApplicationName.Replace(" ", "\n", StringComparison.Ordinal);
        _speechTextLabel.Text = _settings.Branding.ShortText;
        _speechTextLabel.Visible = _settings.MenuVisibility.ShowCustomText;
        _priceLabel.Text = $"{_settings.Pricing.TokenPriceRubles} РУБ.";
        _stockStatusHost.Visible = _settings.MenuVisibility.ShowStockStatus;
        _stockApproximateCount.Visible = _settings.TokenInventory.Enabled;
        _advertisementPanel.Visible = _settings.MenuVisibility.ShowPromotionBlock;
        if (GodotObject.IsInstanceValid(_customTextBlock))
        {
            _customTextBlock!.Visible = _settings.MenuVisibility.ShowCustomText;
        }
        else if (GodotObject.IsInstanceValid(_rightCharacter))
        {
            // Совместимость со старыми DLC, в которых ещё нет общего optional binding.
            _rightCharacter!.Visible = _settings.MenuVisibility.ShowCustomText;
        }

        SetTokenAvailability(_availabilityLevel, _tokenInventoryCount);

        if (_themeAdvertisementPresenter is not null)
        {
            _themeAdvertisementPresenter.Configure(_settings.Advertisement);
        }
        else
        {
            _fallbackAdvertisementPlayer!.Configure(_settings.Advertisement);
        }
        _supportLabel.Text = $"ТЕХПОДДЕРЖКА\n{_settings.Branding.SupportPhone}";

        ApplyPaymentAvailability();
    }

    private void ApplyPaymentAvailability()
    {
        if (_settings is null || !IsNodeReady())
        {
            return;
        }

        bool mayHaveStock = _connectionState == MachineConnectionState.Ready
            && _availabilityLevel is TokenAvailabilityLevel.Low
                or TokenAvailabilityLevel.Enough
                or TokenAvailabilityLevel.Much;
        _cashButton.Disabled = !mayHaveStock || !_settings.Payments.CashEnabled;
        _cardButton.Disabled = !mayHaveStock || !_settings.Payments.CardEnabled;
    }

    private void OnCashPressed()
    {
        AppLogger.Info("UI", "Выбрана оплата наличными.");
        CashRequested?.Invoke();
    }

    private void OnCardPressed()
    {
        AppLogger.Info("UI", "Выбрана оплата картой.");
        CardRequested?.Invoke();
    }

    private void OnThemeChanged(VendingThemeDefinition definition)
    {
        _themeManager.ApplyTo(this);
    }

    private void SetThemeStockVisuals(TokenStockState state)
    {
        if (_themeStockEmpty is null
            || _themeStockLow is null
            || _themeStockEnough is null
            || _themeStockMuch is null)
        {
            return;
        }

        _themeStockEmpty.Visible = state == TokenStockState.Empty;
        _themeStockLow.Visible = state == TokenStockState.Low;
        _themeStockEnough.Visible = state == TokenStockState.Enough;
        _themeStockMuch.Visible = state == TokenStockState.Much;
    }

    private void BindThemeRuntimeVisuals()
    {
        _themeStockState = GetThemeBinding<Label>("home.scroll.content.stock_status.state");
        _themeStockApproximateCount = GetThemeBinding<Label>(
            "home.scroll.content.stock_status.approximate_count");
        _themeStockEmpty = GetOptionalThemeBinding<Control>("home.scroll.content.stock_status.meter.empty");
        _themeStockLow = GetOptionalThemeBinding<Control>("home.scroll.content.stock_status.meter.low");
        _themeStockEnough = GetOptionalThemeBinding<Control>("home.scroll.content.stock_status.meter.enough");
        _themeStockMuch = GetOptionalThemeBinding<Control>("home.scroll.content.stock_status.meter.much");
        _themeConnectionStatus = GetNode<Control>(
            "SafeMargin/Scroll/Content/Footer/Margin/Content/ConnectionStatus");
        _themeConnectionLabel = GetThemeBinding<Label>(
            "home.scroll.content.footer.margin.content.connection_status.label");

        Control advertisementHost = GetNode<Control>(
            "SafeMargin/Scroll/Content/AdvertisementPanel/Layer/Content");
        _themeAdvertisementPresenter = new AdvertisementPresenter(
            advertisementHost,
            GetThemeBinding<VideoStreamPlayer>(
                "home.scroll.content.advertisement_panel.layer.content.video_player"),
            GetThemeBinding<TextureRect>(
                "home.scroll.content.advertisement_panel.layer.content.poster"),
            GetThemeBinding<Label>(
                "home.scroll.content.advertisement_panel.layer.content.fallback_label"));
    }
}
