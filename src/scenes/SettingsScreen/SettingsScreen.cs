using Exchanger.Core.Audio;
using Exchanger.Core.Configuration;
using Exchanger.Core.Inventory;
using Exchanger.Core.Logging;
using Exchanger.Core.Session;
using Exchanger.Core.Theming;
using Exchanger.UI.OnScreenKeyboard;
using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Exchanger.Scenes.SettingsScreen;

/// <summary>
/// Редактор операторских настроек с отложенным автоматическим сохранением.
/// Некорректный промежуточный ввод не записывается и остаётся подсвеченным.
/// </summary>
public partial class SettingsScreen : ThemeBindableScreen
{
    protected override string ThemeScreenKey => "settings";

    private const double AutoSaveDelaySeconds = 0.5;
    private const float TestToneMixRate = 44100.0f;
    private const float TestToneDurationSeconds = 0.28f;
    private const float TestToneFrequency = 660.0f;
    private const int MaximumEditableBonusRules = 16;
    private const int MaximumEditablePaymentPresets = 16;
    private const float ScrollDragDeadzonePixels = 12.0f;

    private Button _generalSectionButton = null!;
    private Button _audioSectionButton = null!;
    private Button _advertisementSectionButton = null!;
    private Button _appearanceSectionButton = null!;
    private Button _pricingSectionButton = null!;
    private Button _paymentSectionButton = null!;
    private Button _serviceSectionButton = null!;
    private Button _equipmentSectionButton = null!;
    private Button _diagnosticsSectionButton = null!;
    private Control _brandingPanel = null!;
    private Control _brandingPreviewPanel = null!;
    private Control _animatedBannerTextsPanel = null!;
    private Control _securityPanel = null!;
    private Control _masterPanel = null!;
    private Control _musicVolumePanel = null!;
    private Control _soundEffectsVolumePanel = null!;
    private Control _advertisementVolumePanel = null!;
    private Control _testPanel = null!;
    private Control _advertisementSettingsPanel = null!;
    private Control _advertisementPosterPanel = null!;
    private Control _advertisementPlaylistPanel = null!;
    private Control _advertisementPreviewPanel = null!;
    private Control _appearanceThemePanel = null!;
    private Control _appearanceEffectsPanel = null!;
    private Control _appearanceNoticePanel = null!;
    private Control _pricePanel = null!;
    private Control _bonusPanel = null!;
    private Control _previewPanel = null!;
    private Control _paymentMethodsPanel = null!;
    private Control _cardOptionsPanel = null!;
    private Control _timeoutsPanel = null!;
    private Control _equipmentConnectionPanel = null!;
    private Control _equipmentWindowPanel = null!;
    private Control _diagnosticsStatusPanel = null!;
    private Control _diagnosticsLoggingPanel = null!;
    private Control _diagnosticsTestPanel = null!;
    private Control _serviceInventoryPanel = null!;
    private Control _menuVisibilityPanel = null!;
    private Label _serviceInventoryDescription = null!;
    private CheckButton _inventoryAccountingEnabled = null!;
    private CheckButton _purchaseLimitEnabled = null!;
    private Label _serviceInventoryCount = null!;
    private Label _serviceHopper1Label = null!;
    private Button _serviceHopper1RecountButton = null!;
    private Button _serviceHopper1ManualAddButton = null!;
    private Label _serviceHopper2Label = null!;
    private Button _serviceHopper2RecountButton = null!;
    private Button _serviceHopper2ManualAddButton = null!;
    private Label _serviceInventoryStatus = null!;
    private CheckButton _showStockStatus = null!;
    private CheckButton _showCustomText = null!;
    private CheckButton _showPromotionBlock = null!;
    private CheckButton _showInteractivePet = null!;
    private Control _serviceDialogOverlay = null!;
    private Label _serviceDialogTitle = null!;
    private Label _serviceDialogDescription = null!;
    private SpinBox _serviceDialogAmount = null!;
    private Button _serviceDialogCancelButton = null!;
    private Button _serviceDialogConfirmButton = null!;
    private HSlider _masterVolumeSlider = null!;
    private Label _masterVolumeValue = null!;
    private CheckButton _muteButton = null!;
    private HSlider _musicVolumeSlider = null!;
    private Label _musicVolumeValue = null!;
    private HSlider _soundEffectsVolumeSlider = null!;
    private Label _soundEffectsVolumeValue = null!;
    private HSlider _advertisementVolumeSlider = null!;
    private Label _advertisementVolumeValue = null!;
    private Button _testSoundButton = null!;
    private Label _statusLabel = null!;
    private SpinBox _tokenPrice = null!;
    private Label _tokenPriceError = null!;
    private VBoxContainer _bonusRows = null!;
    private Button _addBonusRuleButton = null!;
    private Label _bonusError = null!;
    private Label _pricingPreview = null!;
    private CheckButton _cashEnabled = null!;
    private CheckButton _cardEnabled = null!;
    private Label _paymentMethodsError = null!;
    private SpinBox _maximumCardAmount = null!;
    private SpinBox _customAmountStep = null!;
    private Label _cardLimitsError = null!;
    private VBoxContainer _paymentPresetRows = null!;
    private Button _addPaymentPresetButton = null!;
    private Label _paymentPresetsError = null!;
    private SpinBox _sessionIdleTimeout = null!;
    private SpinBox _cashPaymentTimeout = null!;
    private SpinBox _cashlessPaymentTimeout = null!;
    private SpinBox _successTimeout = null!;
    private Label _timeoutsError = null!;
    private LineEdit _applicationName = null!;
    private LineEdit _shortText = null!;
    private LineEdit _supportPhone = null!;
    private Label _applicationNameError = null!;
    private Label _shortTextError = null!;
    private Label _supportPhoneError = null!;
    private Label _applicationNamePreview = null!;
    private Label _shortTextPreview = null!;
    private Label _supportPhonePreview = null!;
    private LineEdit _cashPaymentBannerText = null!;
    private LineEdit _cardAmountBannerText = null!;
    private LineEdit _cardCustomAmountBannerText = null!;
    private LineEdit _cardTerminalCountdownBannerText = null!;
    private CheckButton _servicePinEnabled = null!;
    private Button _resetServicePinButton = null!;
    private Label _servicePinStatus = null!;
    private CheckButton _advertisementEnabled = null!;
    private CheckButton _advertisementMuted = null!;
    private LineEdit _advertisementFallbackText = null!;
    private Label _advertisementFallbackError = null!;
    private LineEdit _advertisementPosterPath = null!;
    private Button _selectAdvertisementPosterButton = null!;
    private Label _advertisementPosterError = null!;
    private VBoxContainer _advertisementVideoRows = null!;
    private Button _addAdvertisementVideoButton = null!;
    private Label _advertisementPlaylistError = null!;
    private Label _advertisementSummary = null!;
    private CheckButton _externalDlcEnabled = null!;
    private LineEdit _externalDlcPath = null!;
    private Label _externalDlcPathError = null!;
    private Label _externalDlcPackageStatus = null!;
    private CheckButton _reducedEffects = null!;
    private Label _currentThemeStatus = null!;
    private CheckButton _useMock = null!;
    private LineEdit _portName = null!;
    private Label _portError = null!;
    private OptionButton _baudRate = null!;
    private SpinBox _reconnectSeconds = null!;
    private Label _connectionError = null!;
    private CheckButton _fullscreen = null!;
    private CheckButton _borderless = null!;
    private CheckButton _debugWindow = null!;
    private OptionButton _targetFps = null!;
    private Label _runtimeSummary = null!;
    private Label _recentErrors = null!;
    private OptionButton _minimumLogLevel = null!;
    private SpinBox _maxLogFileSize = null!;
    private SpinBox _logRetentionDays = null!;
    private Label _loggingError = null!;
    private Button _testDispenseButton = null!;
    private Label _testDispenseStatus = null!;
    private Control _footer = null!;
    private Button _saveAndExitButton = null!;
    private Control.MouseFilterEnum _footerMouseFilter;
    private Control.MouseFilterEnum _saveAndExitMouseFilter;
    private ScrollContainer _scroll = null!;
    private Timer _autoSaveTimer = null!;
    private AudioStreamPlayer _testTonePlayer = null!;
    private ThemeManager _themeManager = null!;
    private readonly List<Label> _bonusRuleErrorLabels = new();
    private readonly List<Label> _paymentPresetErrorLabels = new();
    private readonly List<Label> _advertisementVideoErrorLabels = new();
    private readonly List<ThemeBonusSlot> _themeBonusSlots = new();
    private readonly List<ThemePaymentPresetSlot> _themePaymentPresetSlots = new();
    private readonly List<ThemeAdvertisementVideoSlot> _themeAdvertisementVideoSlots = new();
    private Label? _themeBonusEmptyState;
    private Label? _themePaymentPresetEmptyState;
    private Label? _themeAdvertisementVideoEmptyState;
    private SettingsService? _settingsService;
    private AppSettings? _workingCopy;
    private ServiceAccessLevel _accessLevel = ServiceAccessLevel.Operator;
    private Func<string>? _runtimeSummaryProvider;
    private Func<IReadOnlyList<SafeDiagnosticEntry>>? _recentErrorsProvider;
    private bool _serviceTestAvailable;
    private bool _serviceTestInProgress;
    private bool _autoSaveEnabled;
    private bool _autoSaveInProgress;
    private ulong _testDispenseConfirmationExpiresAt;
    private bool _updatingControls;
    private bool _hopper1Configured = true;
    private bool _hopper2Configured = true;
    private bool _hopper1TokenRecountAvailable;
    private bool _hopper2TokenRecountAvailable;
    private bool _tokenRecountInProgress;
    private int _activeServiceHopper = 1;
    private int _tokenInventoryCount;
    private ServiceDialogMode _serviceDialogMode;
    private OnScreenKeyboardController? _onScreenKeyboard;
    private FileDialog _advertisementMediaFileDialog = null!;
    private AdvertisementAssetType? _advertisementMediaSelectionType;
    private int _activeScrollTouchIndex = -1;
    private bool _mouseScrollPressed;
    private bool _scrollDragActive;
    private float _scrollDragStartY;
    private int _scrollDragStartOffset;

    public event Action<AppSettings, SettingsChangeSummary, ServiceAccessLevel>? Saved;

    public event Action? SaveAndExitRequested;

    public event Action? TestDispenseRequested;

    public event Action<int>? TokenRecountRequested;

    public event Action<int, int>? ManualTokenAdditionConfirmed;

    public override void _Ready()
    {
        _generalSectionButton = GetNode<Button>("SafeMargin/Scroll/Content/SectionTabs/GeneralButton");
        _audioSectionButton = GetNode<Button>("SafeMargin/Scroll/Content/SectionTabs/AudioButton");
        _advertisementSectionButton = GetNode<Button>("SafeMargin/Scroll/Content/SectionTabs/AdvertisementButton");
        _appearanceSectionButton = GetNode<Button>("SafeMargin/Scroll/Content/SectionTabs/AppearanceButton");
        _pricingSectionButton = GetNode<Button>("SafeMargin/Scroll/Content/SectionTabs/PricingButton");
        _paymentSectionButton = GetNode<Button>("SafeMargin/Scroll/Content/SectionTabs/PaymentButton");
        _serviceSectionButton = GetNode<Button>("SafeMargin/Scroll/Content/SectionTabs/ServiceButton");
        _equipmentSectionButton = GetNode<Button>("SafeMargin/Scroll/Content/SectionTabs/EquipmentButton");
        _diagnosticsSectionButton = GetNode<Button>("SafeMargin/Scroll/Content/SectionTabs/DiagnosticsButton");
        _appearanceSectionButton.Visible = false;
        _equipmentSectionButton.Visible = false;
        _diagnosticsSectionButton.Visible = false;
        _brandingPanel = GetNode<Control>("SafeMargin/Scroll/Content/BrandingPanel");
        _brandingPreviewPanel = GetNode<Control>("SafeMargin/Scroll/Content/BrandingPreviewPanel");
        _animatedBannerTextsPanel = GetNode<Control>("SafeMargin/Scroll/Content/AnimatedBannerTextsPanel");
        _securityPanel = GetNode<Control>("SafeMargin/Scroll/Content/SecurityPanel");
        _masterPanel = GetNode<Control>("SafeMargin/Scroll/Content/MasterPanel");
        _musicVolumePanel = GetNode<Control>("SafeMargin/Scroll/Content/MusicPanel");
        _soundEffectsVolumePanel = GetNode<Control>("SafeMargin/Scroll/Content/SoundEffectsPanel");
        _advertisementVolumePanel = GetNode<Control>("SafeMargin/Scroll/Content/AdvertisementPanel");
        _testPanel = GetNode<Control>("SafeMargin/Scroll/Content/TestPanel");
        _advertisementSettingsPanel = GetNode<Control>("SafeMargin/Scroll/Content/AdvertisementSettingsPanel");
        _advertisementPosterPanel = GetNode<Control>("SafeMargin/Scroll/Content/AdvertisementPosterPanel");
        _advertisementPlaylistPanel = GetNode<Control>("SafeMargin/Scroll/Content/AdvertisementPlaylistPanel");
        _advertisementPreviewPanel = GetNode<Control>("SafeMargin/Scroll/Content/AdvertisementPreviewPanel");
        _appearanceThemePanel = GetNode<Control>("SafeMargin/Scroll/Content/AppearanceThemePanel");
        _appearanceEffectsPanel = GetNode<Control>("SafeMargin/Scroll/Content/AppearanceEffectsPanel");
        _appearanceNoticePanel = GetNode<Control>("SafeMargin/Scroll/Content/AppearanceNoticePanel");
        _pricePanel = GetNode<Control>("SafeMargin/Scroll/Content/PricePanel");
        _bonusPanel = GetNode<Control>("SafeMargin/Scroll/Content/BonusPanel");
        _previewPanel = GetNode<Control>("SafeMargin/Scroll/Content/PreviewPanel");
        _paymentMethodsPanel = GetNode<Control>("SafeMargin/Scroll/Content/PaymentMethodsPanel");
        _cardOptionsPanel = GetNode<Control>("SafeMargin/Scroll/Content/CardOptionsPanel");
        _timeoutsPanel = GetNode<Control>("SafeMargin/Scroll/Content/TimeoutsPanel");
        _equipmentConnectionPanel = GetNode<Control>("SafeMargin/Scroll/Content/EquipmentConnectionPanel");
        _equipmentWindowPanel = GetNode<Control>("SafeMargin/Scroll/Content/EquipmentWindowPanel");
        _diagnosticsStatusPanel = GetNode<Control>("SafeMargin/Scroll/Content/DiagnosticsStatusPanel");
        _diagnosticsLoggingPanel = GetNode<Control>("SafeMargin/Scroll/Content/DiagnosticsLoggingPanel");
        _diagnosticsTestPanel = GetNode<Control>("SafeMargin/Scroll/Content/DiagnosticsTestPanel");
        _serviceInventoryPanel = GetNode<Control>("SafeMargin/Scroll/Content/ServiceInventoryPanel");
        _menuVisibilityPanel = GetNode<Control>("SafeMargin/Scroll/Content/MenuVisibilityPanel");
        if (UsesExternalThemeScene)
        {
            _serviceInventoryDescription = GetThemeBinding<Label>(
                "settings.scroll.content.service_inventory_panel.margin.content.description");
            _inventoryAccountingEnabled = GetThemeBinding<CheckButton>(
                "settings.scroll.content.service_inventory_panel.margin.content.inventory_enabled");
            _purchaseLimitEnabled = GetThemeBinding<CheckButton>(
                "settings.scroll.content.service_inventory_panel.margin.content.purchase_limit_enabled");
        }
        else
        {
            _serviceInventoryDescription = GetNode<Label>(
                "SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Description");
            _inventoryAccountingEnabled = GetNode<CheckButton>(
                "SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/InventoryAccountingEnabled");
            _purchaseLimitEnabled = GetNode<CheckButton>(
                "SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/PurchaseLimitEnabled");
        }
        _serviceInventoryCount = GetNode<Label>("SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Count");
        _serviceHopper1Label = GetNode<Label>("SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Hopper1Label");
        _serviceHopper1RecountButton = GetNode<Button>("SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Hopper1/RecountButton");
        _serviceHopper1ManualAddButton = GetNode<Button>("SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Hopper1/ManualAddButton");
        _serviceHopper2Label = GetNode<Label>("SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Hopper2Label");
        _serviceHopper2RecountButton = GetNode<Button>("SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Hopper2/RecountButton");
        _serviceHopper2ManualAddButton = GetNode<Button>("SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Hopper2/ManualAddButton");
        _serviceInventoryStatus = GetNode<Label>("SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Status");
        _showStockStatus = GetNode<CheckButton>("SafeMargin/Scroll/Content/MenuVisibilityPanel/Margin/Content/ShowStockStatus");
        _showCustomText = GetNode<CheckButton>("SafeMargin/Scroll/Content/MenuVisibilityPanel/Margin/Content/ShowRightCharacter");
        _showPromotionBlock = GetNode<CheckButton>("SafeMargin/Scroll/Content/MenuVisibilityPanel/Margin/Content/ShowPromotionBlock");
        _showInteractivePet = GetNode<CheckButton>("SafeMargin/Scroll/Content/MenuVisibilityPanel/Margin/Content/ShowInteractivePet");
        _serviceDialogOverlay = GetNode<Control>("SafeMargin/ServiceDialogOverlay");
        _serviceDialogTitle = GetNode<Label>("SafeMargin/ServiceDialogOverlay/Dialog/Margin/Content/Title");
        _serviceDialogDescription = GetNode<Label>("SafeMargin/ServiceDialogOverlay/Dialog/Margin/Content/Description");
        _serviceDialogAmount = GetNode<SpinBox>("SafeMargin/ServiceDialogOverlay/Dialog/Margin/Content/Amount");
        _serviceDialogCancelButton = GetNode<Button>("SafeMargin/ServiceDialogOverlay/Dialog/Margin/Content/Buttons/CancelButton");
        _serviceDialogConfirmButton = GetNode<Button>("SafeMargin/ServiceDialogOverlay/Dialog/Margin/Content/Buttons/ConfirmButton");
        _masterVolumeSlider = GetNode<HSlider>("SafeMargin/Scroll/Content/MasterPanel/Margin/Content/VolumeRow/Slider");
        _masterVolumeValue = GetNode<Label>("SafeMargin/Scroll/Content/MasterPanel/Margin/Content/VolumeRow/Value");
        _muteButton = GetNode<CheckButton>("SafeMargin/Scroll/Content/MasterPanel/Margin/Content/Mute");
        _musicVolumeSlider = _musicVolumePanel.GetNode<HSlider>("Margin/Content/VolumeRow/Slider");
        _musicVolumeValue = _musicVolumePanel.GetNode<Label>("Margin/Content/VolumeRow/Value");
        _soundEffectsVolumeSlider = _soundEffectsVolumePanel.GetNode<HSlider>("Margin/Content/VolumeRow/Slider");
        _soundEffectsVolumeValue = _soundEffectsVolumePanel.GetNode<Label>("Margin/Content/VolumeRow/Value");
        _advertisementVolumeSlider = GetNode<HSlider>("SafeMargin/Scroll/Content/AdvertisementPanel/Margin/Content/VolumeRow/Slider");
        _advertisementVolumeValue = GetNode<Label>("SafeMargin/Scroll/Content/AdvertisementPanel/Margin/Content/VolumeRow/Value");
        _testSoundButton = GetNode<Button>("SafeMargin/Scroll/Content/TestPanel/Margin/Content/TestSoundButton");
        _statusLabel = GetNode<Label>("SafeMargin/Scroll/Content/TestPanel/Margin/Content/Status");
        _tokenPrice = GetNode<SpinBox>("SafeMargin/Scroll/Content/PricePanel/Margin/Content/Price");
        _tokenPriceError = GetNode<Label>("SafeMargin/Scroll/Content/PricePanel/Margin/Content/Error");
        _bonusRows = GetNode<VBoxContainer>("SafeMargin/Scroll/Content/BonusPanel/Margin/Content/Rows");
        _addBonusRuleButton = GetNode<Button>("SafeMargin/Scroll/Content/BonusPanel/Margin/Content/AddRuleButton");
        _bonusError = GetNode<Label>("SafeMargin/Scroll/Content/BonusPanel/Margin/Content/Error");
        _pricingPreview = GetNode<Label>("SafeMargin/Scroll/Content/PreviewPanel/Margin/Content/Values");
        _cashEnabled = GetNode<CheckButton>("SafeMargin/Scroll/Content/PaymentMethodsPanel/Margin/Content/CashEnabled");
        _cardEnabled = GetNode<CheckButton>("SafeMargin/Scroll/Content/PaymentMethodsPanel/Margin/Content/CardEnabled");
        _paymentMethodsError = GetNode<Label>("SafeMargin/Scroll/Content/PaymentMethodsPanel/Margin/Content/Error");
        _maximumCardAmount = GetNode<SpinBox>("SafeMargin/Scroll/Content/CardOptionsPanel/Margin/Content/Limits/MaxAmount");
        _customAmountStep = GetNode<SpinBox>("SafeMargin/Scroll/Content/CardOptionsPanel/Margin/Content/Limits/CustomStep");
        _cardLimitsError = GetNode<Label>("SafeMargin/Scroll/Content/CardOptionsPanel/Margin/Content/LimitsError");
        _paymentPresetRows = GetNode<VBoxContainer>("SafeMargin/Scroll/Content/CardOptionsPanel/Margin/Content/PresetRows");
        _addPaymentPresetButton = GetNode<Button>("SafeMargin/Scroll/Content/CardOptionsPanel/Margin/Content/AddPresetButton");
        _paymentPresetsError = GetNode<Label>("SafeMargin/Scroll/Content/CardOptionsPanel/Margin/Content/PresetsError");
        _sessionIdleTimeout = GetNode<SpinBox>("SafeMargin/Scroll/Content/TimeoutsPanel/Margin/Content/Grid/Idle");
        _cashPaymentTimeout = GetNode<SpinBox>("SafeMargin/Scroll/Content/TimeoutsPanel/Margin/Content/Grid/CashPayment");
        _cashlessPaymentTimeout = GetNode<SpinBox>("SafeMargin/Scroll/Content/TimeoutsPanel/Margin/Content/Grid/CashlessPayment");
        _successTimeout = GetNode<SpinBox>("SafeMargin/Scroll/Content/TimeoutsPanel/Margin/Content/Grid/Success");
        _timeoutsError = GetNode<Label>("SafeMargin/Scroll/Content/TimeoutsPanel/Margin/Content/Error");
        _applicationName = GetNode<LineEdit>("SafeMargin/Scroll/Content/BrandingPanel/Margin/Content/ApplicationName");
        _shortText = GetNode<LineEdit>("SafeMargin/Scroll/Content/BrandingPanel/Margin/Content/ShortText");
        _supportPhone = GetNode<LineEdit>("SafeMargin/Scroll/Content/BrandingPanel/Margin/Content/SupportPhone");
        _applicationNameError = GetNode<Label>("SafeMargin/Scroll/Content/BrandingPanel/Margin/Content/ApplicationNameError");
        _shortTextError = GetNode<Label>("SafeMargin/Scroll/Content/BrandingPanel/Margin/Content/ShortTextError");
        _supportPhoneError = GetNode<Label>("SafeMargin/Scroll/Content/BrandingPanel/Margin/Content/SupportPhoneError");
        _applicationNamePreview = GetNode<Label>("SafeMargin/Scroll/Content/BrandingPreviewPanel/Margin/Content/ApplicationName");
        _shortTextPreview = GetNode<Label>("SafeMargin/Scroll/Content/BrandingPreviewPanel/Margin/Content/ShortText");
        _supportPhonePreview = GetNode<Label>("SafeMargin/Scroll/Content/BrandingPreviewPanel/Margin/Content/SupportPhone");
        _cashPaymentBannerText = GetAnimatedBannerTextControl<LineEdit>("CashPayment");
        _cardAmountBannerText = GetAnimatedBannerTextControl<LineEdit>("CardAmount");
        _cardCustomAmountBannerText = GetAnimatedBannerTextControl<LineEdit>("CardCustomAmount");
        _cardTerminalCountdownBannerText = GetAnimatedBannerTextControl<LineEdit>("CardTerminalCountdown");
        ApplyBrandingUiPolicy();
        _servicePinEnabled = GetNode<CheckButton>("SafeMargin/Scroll/Content/SecurityPanel/Margin/Content/Enabled");
        _resetServicePinButton = GetNode<Button>("SafeMargin/Scroll/Content/SecurityPanel/Margin/Content/ResetPinButton");
        _servicePinStatus = GetNode<Label>("SafeMargin/Scroll/Content/SecurityPanel/Margin/Content/Status");
        _advertisementEnabled = GetNode<CheckButton>("SafeMargin/Scroll/Content/AdvertisementSettingsPanel/Margin/Content/Enabled");
        _advertisementMuted = GetNode<CheckButton>("SafeMargin/Scroll/Content/AdvertisementSettingsPanel/Margin/Content/Muted");
        _advertisementFallbackText = GetNode<LineEdit>("SafeMargin/Scroll/Content/AdvertisementSettingsPanel/Margin/Content/FallbackText");
        _advertisementFallbackError = GetNode<Label>("SafeMargin/Scroll/Content/AdvertisementSettingsPanel/Margin/Content/FallbackError");
        _advertisementPosterPath = GetNode<LineEdit>("SafeMargin/Scroll/Content/AdvertisementPosterPanel/Margin/Content/PosterPath");
        _selectAdvertisementPosterButton = GetNode<Button>("SafeMargin/Scroll/Content/AdvertisementPosterPanel/Margin/Content/SelectButton");
        _advertisementPosterError = GetNode<Label>("SafeMargin/Scroll/Content/AdvertisementPosterPanel/Margin/Content/Error");
        _advertisementVideoRows = GetNode<VBoxContainer>("SafeMargin/Scroll/Content/AdvertisementPlaylistPanel/Margin/Content/Rows");
        _addAdvertisementVideoButton = GetNode<Button>("SafeMargin/Scroll/Content/AdvertisementPlaylistPanel/Margin/Content/AddButton");
        _advertisementPlaylistError = GetNode<Label>("SafeMargin/Scroll/Content/AdvertisementPlaylistPanel/Margin/Content/Error");
        _advertisementSummary = GetNode<Label>("SafeMargin/Scroll/Content/AdvertisementPreviewPanel/Margin/Content/Summary");
        _externalDlcEnabled = GetNode<CheckButton>("SafeMargin/Scroll/Content/AppearanceThemePanel/Margin/Content/ExternalEnabled");
        _externalDlcPath = GetNode<LineEdit>("SafeMargin/Scroll/Content/AppearanceThemePanel/Margin/Content/ExternalPath");
        _externalDlcPathError = GetNode<Label>("SafeMargin/Scroll/Content/AppearanceThemePanel/Margin/Content/PathError");
        _externalDlcPackageStatus = GetNode<Label>("SafeMargin/Scroll/Content/AppearanceThemePanel/Margin/Content/PackageStatus");
        _reducedEffects = GetNode<CheckButton>("SafeMargin/Scroll/Content/AppearanceEffectsPanel/Margin/Content/ReducedEffects");
        _currentThemeStatus = GetNode<Label>("SafeMargin/Scroll/Content/AppearanceNoticePanel/Margin/Content/CurrentTheme");
        _useMock = GetNode<CheckButton>("SafeMargin/Scroll/Content/EquipmentConnectionPanel/Margin/Content/UseMock");
        _portName = GetNode<LineEdit>("SafeMargin/Scroll/Content/EquipmentConnectionPanel/Margin/Content/PortName");
        _portError = GetNode<Label>("SafeMargin/Scroll/Content/EquipmentConnectionPanel/Margin/Content/PortError");
        _baudRate = GetNode<OptionButton>("SafeMargin/Scroll/Content/EquipmentConnectionPanel/Margin/Content/BaudRate");
        _reconnectSeconds = GetNode<SpinBox>("SafeMargin/Scroll/Content/EquipmentConnectionPanel/Margin/Content/ReconnectSeconds");
        _connectionError = GetNode<Label>("SafeMargin/Scroll/Content/EquipmentConnectionPanel/Margin/Content/ConnectionError");
        _fullscreen = GetNode<CheckButton>("SafeMargin/Scroll/Content/EquipmentWindowPanel/Margin/Content/Fullscreen");
        _borderless = GetNode<CheckButton>("SafeMargin/Scroll/Content/EquipmentWindowPanel/Margin/Content/Borderless");
        _debugWindow = GetNode<CheckButton>("SafeMargin/Scroll/Content/EquipmentWindowPanel/Margin/Content/DebugWindow");
        _targetFps = GetNode<OptionButton>("SafeMargin/Scroll/Content/EquipmentWindowPanel/Margin/Content/TargetFps");
        _runtimeSummary = GetNode<Label>("SafeMargin/Scroll/Content/DiagnosticsStatusPanel/Margin/Content/RuntimeSummary");
        _recentErrors = GetNode<Label>("SafeMargin/Scroll/Content/DiagnosticsStatusPanel/Margin/Content/RecentErrors");
        _minimumLogLevel = GetNode<OptionButton>("SafeMargin/Scroll/Content/DiagnosticsLoggingPanel/Margin/Content/MinimumLevel");
        _maxLogFileSize = GetNode<SpinBox>("SafeMargin/Scroll/Content/DiagnosticsLoggingPanel/Margin/Content/MaxFileSize");
        _logRetentionDays = GetNode<SpinBox>("SafeMargin/Scroll/Content/DiagnosticsLoggingPanel/Margin/Content/RetentionDays");
        _loggingError = GetNode<Label>("SafeMargin/Scroll/Content/DiagnosticsLoggingPanel/Margin/Content/LoggingError");
        _testDispenseButton = GetNode<Button>("SafeMargin/Scroll/Content/DiagnosticsTestPanel/Margin/Content/TestDispenseButton");
        _testDispenseStatus = GetNode<Label>("SafeMargin/Scroll/Content/DiagnosticsTestPanel/Margin/Content/TestDispenseStatus");
        _footer = GetNode<Control>("Footer");
        _saveAndExitButton = GetNode<Button>("Footer/SaveAndExitButton");
        _footerMouseFilter = _footer.MouseFilter;
        _saveAndExitMouseFilter = _saveAndExitButton.MouseFilter;
        _scroll = GetNode<ScrollContainer>("SafeMargin/Scroll");
        _scroll.ScrollDeadzone = Mathf.RoundToInt(ScrollDragDeadzonePixels);
        _autoSaveTimer = GetNode<Timer>("AutoSaveTimer");
        _testTonePlayer = GetNode<AudioStreamPlayer>("TestTonePlayer");
        _advertisementMediaFileDialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = true,
        };
        AddChild(_advertisementMediaFileDialog);

        if (UsesSelfContainedThemeScene)
        {
            BindThemeCollectionSlots();
        }

        _testTonePlayer.Stream = new AudioStreamGenerator
        {
            MixRate = TestToneMixRate,
            BufferLength = 0.5f,
        };
        _testTonePlayer.Bus = AudioSettingsApplier.EffectsBusName;

        _themeManager = GetNode<ThemeManager>("/root/ThemeManager");
        if (!UsesSelfContainedThemeScene)
        {
            ApplyFallbackThemeSemantics();
        }

        _themeManager.ApplyTo(this);
        _themeManager.ThemeChanged += OnThemeChanged;

        foreach (int baudRate in HardwareSettingsValidator.AllowedBaudRates)
        {
            _baudRate.AddItem(baudRate.ToString(), baudRate);
        }

        foreach (int fps in new[] { 30, 60, 120 })
        {
            _targetFps.AddItem(fps.ToString(), fps);
        }

        foreach (string level in LoggingSettingsValidator.AllowedLevels)
        {
            _minimumLogLevel.AddItem(level);
        }

        _generalSectionButton.Pressed += OnGeneralSectionPressed;
        _audioSectionButton.Pressed += OnAudioSectionPressed;
        _advertisementSectionButton.Pressed += OnAdvertisementSectionPressed;
        _appearanceSectionButton.Pressed += OnAppearanceSectionPressed;
        _pricingSectionButton.Pressed += OnPricingSectionPressed;
        _paymentSectionButton.Pressed += OnPaymentSectionPressed;
        _serviceSectionButton.Pressed += OnServiceSectionPressed;
        _equipmentSectionButton.Pressed += OnEquipmentSectionPressed;
        _diagnosticsSectionButton.Pressed += OnDiagnosticsSectionPressed;
        _masterVolumeSlider.ValueChanged += OnMasterVolumeChanged;
        _muteButton.Toggled += OnMuteToggled;
        _musicVolumeSlider.ValueChanged += OnMusicVolumeChanged;
        _soundEffectsVolumeSlider.ValueChanged += OnSoundEffectsVolumeChanged;
        _advertisementVolumeSlider.ValueChanged += OnAdvertisementVolumeChanged;
        _testSoundButton.Pressed += OnTestSoundPressed;
        _tokenPrice.ValueChanged += OnTokenPriceChanged;
        _addBonusRuleButton.Pressed += OnAddBonusRulePressed;
        _cashEnabled.Toggled += OnCashEnabledToggled;
        _cardEnabled.Toggled += OnCardEnabledToggled;
        _maximumCardAmount.ValueChanged += OnMaximumCardAmountChanged;
        _customAmountStep.ValueChanged += OnCustomAmountStepChanged;
        _addPaymentPresetButton.Pressed += OnAddPaymentPresetPressed;
        _sessionIdleTimeout.ValueChanged += OnSessionIdleTimeoutChanged;
        _cashPaymentTimeout.ValueChanged += OnCashPaymentTimeoutChanged;
        _cashlessPaymentTimeout.ValueChanged += OnCashlessPaymentTimeoutChanged;
        _successTimeout.ValueChanged += OnSuccessTimeoutChanged;
        _applicationName.TextChanged += OnApplicationNameChanged;
        _shortText.TextChanged += OnShortTextChanged;
        _supportPhone.TextChanged += OnSupportPhoneChanged;
        _cashPaymentBannerText.TextChanged += OnCashPaymentBannerTextChanged;
        _cardAmountBannerText.TextChanged += OnCardAmountBannerTextChanged;
        _cardCustomAmountBannerText.TextChanged += OnCardCustomAmountBannerTextChanged;
        _cardTerminalCountdownBannerText.TextChanged += OnCardTerminalCountdownBannerTextChanged;
        _servicePinEnabled.Toggled += OnServicePinEnabledToggled;
        _resetServicePinButton.Pressed += OnResetServicePinPressed;
        _advertisementEnabled.Toggled += OnAdvertisementEnabledToggled;
        _advertisementMuted.Toggled += OnAdvertisementMutedToggled;
        _advertisementFallbackText.TextChanged += OnAdvertisementFallbackTextChanged;
        _advertisementPosterPath.TextChanged += OnAdvertisementPosterPathChanged;
        _selectAdvertisementPosterButton.Pressed += OnSelectAdvertisementPosterPressed;
        _addAdvertisementVideoButton.Pressed += OnAddAdvertisementVideoPressed;
        _advertisementMediaFileDialog.FileSelected += OnAdvertisementMediaFileSelected;
        _externalDlcEnabled.Toggled += OnExternalDlcEnabledToggled;
        _externalDlcPath.TextChanged += OnExternalDlcPathChanged;
        _reducedEffects.Toggled += OnReducedEffectsToggled;
        _useMock.Toggled += OnUseMockToggled;
        _portName.TextChanged += OnPortNameChanged;
        _baudRate.ItemSelected += OnBaudRateSelected;
        _reconnectSeconds.ValueChanged += OnReconnectSecondsChanged;
        _fullscreen.Toggled += OnFullscreenToggled;
        _borderless.Toggled += OnBorderlessToggled;
        _debugWindow.Toggled += OnDebugWindowToggled;
        _targetFps.ItemSelected += OnTargetFpsSelected;
        _minimumLogLevel.ItemSelected += OnMinimumLogLevelSelected;
        _maxLogFileSize.ValueChanged += OnMaxLogFileSizeChanged;
        _logRetentionDays.ValueChanged += OnLogRetentionDaysChanged;
        _testDispenseButton.Pressed += OnTestDispensePressed;
        _serviceHopper1RecountButton.Pressed += OnServiceHopper1RecountPressed;
        _serviceHopper1ManualAddButton.Pressed += OnServiceHopper1ManualAddPressed;
        _serviceHopper2RecountButton.Pressed += OnServiceHopper2RecountPressed;
        _serviceHopper2ManualAddButton.Pressed += OnServiceHopper2ManualAddPressed;
        _inventoryAccountingEnabled.Toggled += OnInventoryAccountingEnabledToggled;
        _purchaseLimitEnabled.Toggled += OnPurchaseLimitEnabledToggled;
        _showStockStatus.Toggled += OnShowStockStatusToggled;
        _showCustomText.Toggled += OnShowCustomTextToggled;
        _showPromotionBlock.Toggled += OnShowPromotionBlockToggled;
        _showInteractivePet.Toggled += OnShowInteractivePetToggled;
        _serviceDialogCancelButton.Pressed += OnServiceDialogCancelPressed;
        _serviceDialogConfirmButton.Pressed += OnServiceDialogConfirmPressed;
        _saveAndExitButton.Pressed += OnSaveAndExitPressed;
        _autoSaveTimer.WaitTime = AutoSaveDelaySeconds;
        _autoSaveTimer.Timeout += OnAutoSaveTimeout;
        InitializeOnScreenKeyboard();
        RefreshKeyboardInputBindings();
    }

    public override void _Input(InputEvent inputEvent)
    {
        switch (inputEvent)
        {
            case InputEventScreenTouch touch when touch.Index == 0:
                HandleScrollTouch(touch);
                break;

            case InputEventScreenDrag drag when drag.Index == _activeScrollTouchIndex:
                if (UpdateScrollDrag(drag.Position.Y))
                {
                    GetViewport().SetInputAsHandled();
                }

                break;

            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mouseButton:
                HandleScrollMouseButton(mouseButton);
                break;

            case InputEventMouseMotion mouseMotion when _mouseScrollPressed && _activeScrollTouchIndex < 0:
                if (UpdateScrollDrag(mouseMotion.Position.Y))
                {
                    GetViewport().SetInputAsHandled();
                }

                break;
        }
    }

    private void HandleScrollTouch(InputEventScreenTouch touch)
    {
        if (touch.Pressed)
        {
            if (!CanStartScrollDrag(touch.Position))
            {
                return;
            }

            _activeScrollTouchIndex = touch.Index;
            _mouseScrollPressed = false;
            BeginScrollDrag(touch.Position.Y);
            return;
        }

        if (touch.Index != _activeScrollTouchIndex)
        {
            return;
        }

        bool wasDragging = _scrollDragActive;
        ResetScrollDrag();
        _activeScrollTouchIndex = -1;
        if (wasDragging)
        {
            GetViewport().SetInputAsHandled();
        }
    }

    private void HandleScrollMouseButton(InputEventMouseButton mouseButton)
    {
        if (mouseButton.Pressed)
        {
            if (_activeScrollTouchIndex >= 0 || !CanStartScrollDrag(mouseButton.Position))
            {
                return;
            }

            _mouseScrollPressed = true;
            BeginScrollDrag(mouseButton.Position.Y);
            return;
        }

        if (!_mouseScrollPressed)
        {
            return;
        }

        bool wasDragging = _scrollDragActive;
        _mouseScrollPressed = false;
        ResetScrollDrag();
        if (wasDragging)
        {
            GetViewport().SetInputAsHandled();
        }
    }

    private bool CanStartScrollDrag(Vector2 pointerPosition)
    {
        if (!IsVisibleInTree()
            || !GodotObject.IsInstanceValid(_scroll)
            || !_scroll.IsVisibleInTree()
            || _serviceDialogOverlay.Visible
            || (_onScreenKeyboard?.IsVisible ?? false)
            || (GodotObject.IsInstanceValid(_footer) && _footer.GetGlobalRect().HasPoint(pointerPosition))
            || !_scroll.GetGlobalRect().HasPoint(pointerPosition))
        {
            return false;
        }

        VScrollBar verticalScrollBar = _scroll.GetVScrollBar();
        return !verticalScrollBar.Visible || !verticalScrollBar.GetGlobalRect().HasPoint(pointerPosition);
    }

    private void BeginScrollDrag(float pointerY)
    {
        _scrollDragActive = false;
        _scrollDragStartY = pointerY;
        _scrollDragStartOffset = _scroll.ScrollVertical;
    }

    private bool UpdateScrollDrag(float pointerY)
    {
        float dragDistance = pointerY - _scrollDragStartY;
        if (!_scrollDragActive && Mathf.Abs(dragDistance) < ScrollDragDeadzonePixels)
        {
            return false;
        }

        _scrollDragActive = true;
        _scroll.ScrollVertical = _scrollDragStartOffset - Mathf.RoundToInt(dragDistance);
        return true;
    }

    private void ResetScrollDrag()
    {
        _scrollDragActive = false;
        _scrollDragStartY = 0.0f;
        _scrollDragStartOffset = 0;
    }

    public override void _ExitTree()
    {
        if (_onScreenKeyboard is not null)
        {
            _onScreenKeyboard.VisibilityChanged -= OnScreenKeyboardVisibilityChanged;
            _onScreenKeyboard.Dispose();
        }

        _onScreenKeyboard = null;

        if (GodotObject.IsInstanceValid(_themeManager))
        {
            _themeManager.ThemeChanged -= OnThemeChanged;
        }

        if (GodotObject.IsInstanceValid(_masterVolumeSlider))
        {
            _generalSectionButton.Pressed -= OnGeneralSectionPressed;
            _audioSectionButton.Pressed -= OnAudioSectionPressed;
            _advertisementSectionButton.Pressed -= OnAdvertisementSectionPressed;
            _appearanceSectionButton.Pressed -= OnAppearanceSectionPressed;
            _pricingSectionButton.Pressed -= OnPricingSectionPressed;
            _paymentSectionButton.Pressed -= OnPaymentSectionPressed;
            _serviceSectionButton.Pressed -= OnServiceSectionPressed;
            _equipmentSectionButton.Pressed -= OnEquipmentSectionPressed;
            _diagnosticsSectionButton.Pressed -= OnDiagnosticsSectionPressed;
            _masterVolumeSlider.ValueChanged -= OnMasterVolumeChanged;
            _muteButton.Toggled -= OnMuteToggled;
            _musicVolumeSlider.ValueChanged -= OnMusicVolumeChanged;
            _soundEffectsVolumeSlider.ValueChanged -= OnSoundEffectsVolumeChanged;
            _advertisementVolumeSlider.ValueChanged -= OnAdvertisementVolumeChanged;
            _testSoundButton.Pressed -= OnTestSoundPressed;
            _tokenPrice.ValueChanged -= OnTokenPriceChanged;
            _addBonusRuleButton.Pressed -= OnAddBonusRulePressed;
            _cashEnabled.Toggled -= OnCashEnabledToggled;
            _cardEnabled.Toggled -= OnCardEnabledToggled;
            _maximumCardAmount.ValueChanged -= OnMaximumCardAmountChanged;
            _customAmountStep.ValueChanged -= OnCustomAmountStepChanged;
            _addPaymentPresetButton.Pressed -= OnAddPaymentPresetPressed;
            _sessionIdleTimeout.ValueChanged -= OnSessionIdleTimeoutChanged;
            _cashPaymentTimeout.ValueChanged -= OnCashPaymentTimeoutChanged;
            _cashlessPaymentTimeout.ValueChanged -= OnCashlessPaymentTimeoutChanged;
            _successTimeout.ValueChanged -= OnSuccessTimeoutChanged;
            _applicationName.TextChanged -= OnApplicationNameChanged;
            _shortText.TextChanged -= OnShortTextChanged;
            _supportPhone.TextChanged -= OnSupportPhoneChanged;
            _cashPaymentBannerText.TextChanged -= OnCashPaymentBannerTextChanged;
            _cardAmountBannerText.TextChanged -= OnCardAmountBannerTextChanged;
            _cardCustomAmountBannerText.TextChanged -= OnCardCustomAmountBannerTextChanged;
            _cardTerminalCountdownBannerText.TextChanged -= OnCardTerminalCountdownBannerTextChanged;
            _servicePinEnabled.Toggled -= OnServicePinEnabledToggled;
            _resetServicePinButton.Pressed -= OnResetServicePinPressed;
            _advertisementEnabled.Toggled -= OnAdvertisementEnabledToggled;
            _advertisementMuted.Toggled -= OnAdvertisementMutedToggled;
            _advertisementFallbackText.TextChanged -= OnAdvertisementFallbackTextChanged;
            _advertisementPosterPath.TextChanged -= OnAdvertisementPosterPathChanged;
            _selectAdvertisementPosterButton.Pressed -= OnSelectAdvertisementPosterPressed;
            _addAdvertisementVideoButton.Pressed -= OnAddAdvertisementVideoPressed;
            _advertisementMediaFileDialog.FileSelected -= OnAdvertisementMediaFileSelected;
            _externalDlcEnabled.Toggled -= OnExternalDlcEnabledToggled;
            _externalDlcPath.TextChanged -= OnExternalDlcPathChanged;
            _reducedEffects.Toggled -= OnReducedEffectsToggled;
            _useMock.Toggled -= OnUseMockToggled;
            _portName.TextChanged -= OnPortNameChanged;
            _baudRate.ItemSelected -= OnBaudRateSelected;
            _reconnectSeconds.ValueChanged -= OnReconnectSecondsChanged;
            _fullscreen.Toggled -= OnFullscreenToggled;
            _borderless.Toggled -= OnBorderlessToggled;
            _debugWindow.Toggled -= OnDebugWindowToggled;
            _targetFps.ItemSelected -= OnTargetFpsSelected;
            _minimumLogLevel.ItemSelected -= OnMinimumLogLevelSelected;
            _maxLogFileSize.ValueChanged -= OnMaxLogFileSizeChanged;
            _logRetentionDays.ValueChanged -= OnLogRetentionDaysChanged;
            _testDispenseButton.Pressed -= OnTestDispensePressed;
            _serviceHopper1RecountButton.Pressed -= OnServiceHopper1RecountPressed;
            _serviceHopper1ManualAddButton.Pressed -= OnServiceHopper1ManualAddPressed;
            _serviceHopper2RecountButton.Pressed -= OnServiceHopper2RecountPressed;
            _serviceHopper2ManualAddButton.Pressed -= OnServiceHopper2ManualAddPressed;
            _inventoryAccountingEnabled.Toggled -= OnInventoryAccountingEnabledToggled;
            _purchaseLimitEnabled.Toggled -= OnPurchaseLimitEnabledToggled;
            _showStockStatus.Toggled -= OnShowStockStatusToggled;
            _showCustomText.Toggled -= OnShowCustomTextToggled;
            _showPromotionBlock.Toggled -= OnShowPromotionBlockToggled;
            _showInteractivePet.Toggled -= OnShowInteractivePetToggled;
            _serviceDialogCancelButton.Pressed -= OnServiceDialogCancelPressed;
            _serviceDialogConfirmButton.Pressed -= OnServiceDialogConfirmPressed;
            _saveAndExitButton.Pressed -= OnSaveAndExitPressed;
            _autoSaveTimer.Timeout -= OnAutoSaveTimeout;
        }

        UnbindThemeCollectionSlots();
    }

    public void Configure(
        SettingsService settingsService,
        Func<string>? runtimeSummaryProvider = null,
        Func<IReadOnlyList<SafeDiagnosticEntry>>? recentErrorsProvider = null)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _runtimeSummaryProvider = runtimeSummaryProvider;
        _recentErrorsProvider = recentErrorsProvider;
    }

    public void BeginEdit(ServiceAccessLevel accessLevel = ServiceAccessLevel.Debug)
    {
        if (_settingsService is null)
        {
            throw new InvalidOperationException("Сервис настроек не настроен.");
        }

        _accessLevel = accessLevel;
        _autoSaveEnabled = false;
        _onScreenKeyboard?.Hide();
        _autoSaveTimer.Stop();
        _workingCopy = _settingsService.CreateWorkingCopy();
        _equipmentSectionButton.Disabled = true;
        _testDispenseConfirmationExpiresAt = 0;
        _serviceTestInProgress = false;
        _tokenRecountInProgress = false;
        CloseServiceDialog();
        _statusLabel.Text = "Настройки сохраняются автоматически.";
        PopulateAllControls();
        ShowGeneralSection();
        _autoSaveEnabled = true;
    }

    public void SetTokenInventoryCounts(int hopper1Count, int hopper2Count)
    {
        int hopper1 = Math.Clamp(hopper1Count, 0, TokenInventoryStore.MaximumTokenCount);
        int hopper2 = Math.Clamp(hopper2Count, 0, TokenInventoryStore.MaximumTokenCount);
        _tokenInventoryCount = (int)Math.Min(
            TokenInventoryStore.MaximumTokenCount,
            (_hopper1Configured ? (long)hopper1 : 0L)
            + (_hopper2Configured ? hopper2 : 0));
        _serviceInventoryCount.Text = TokenInventoryPolicy.FormatExactCount(_tokenInventoryCount);
        _serviceHopper1Label.Text = TokenInventoryPolicy.FormatHopperExactCount(1, hopper1);
        _serviceHopper2Label.Text = TokenInventoryPolicy.FormatHopperExactCount(2, hopper2);
    }

    public void SetConfiguredHoppers(bool hopper1Configured, bool hopper2Configured)
    {
        _hopper1Configured = hopper1Configured;
        _hopper2Configured = hopper2Configured;
        if (!_hopper1Configured && _activeServiceHopper == 1)
        {
            _activeServiceHopper = _hopper2Configured ? 2 : 1;
        }

        ApplyTokenInventoryUiPolicy();
    }

    public void SetTokenRecountAvailability(bool hopper1Available, bool hopper2Available, string status)
    {
        _hopper1TokenRecountAvailable = _hopper1Configured && hopper1Available;
        _hopper2TokenRecountAvailable = _hopper2Configured && hopper2Available;
        RefreshTokenServiceButtons();
        if (!_tokenRecountInProgress && !string.IsNullOrWhiteSpace(status))
        {
            _serviceInventoryStatus.Text = status;
        }
    }

    public void SetTokenRecountProgress(int hopperNumber, int count)
    {
        _activeServiceHopper = NormalizeHopperNumber(hopperNumber);
        _tokenRecountInProgress = true;
        RefreshTokenServiceButtons();
        _serviceInventoryStatus.Text =
            $"Хоппер {_activeServiceHopper}: пересчитано {FormatTokenCount(Math.Max(0, count))}.";
    }

    public void SetTokenRecountCompleted(int hopperNumber, int count)
    {
        _activeServiceHopper = NormalizeHopperNumber(hopperNumber);
        _tokenRecountInProgress = false;
        RefreshTokenServiceButtons();
        _serviceInventoryStatus.Text =
            $"Хоппер {_activeServiceHopper} пересчитан: {FormatTokenCount(Math.Max(0, count))}.";
    }

    public void SetTokenRecountFailure(string status)
    {
        _tokenRecountInProgress = false;
        RefreshTokenServiceButtons();
        _serviceInventoryStatus.Text = string.IsNullOrWhiteSpace(status)
            ? "Не удалось пересчитать жетоны."
            : status;
    }

    private void RefreshTokenServiceButtons()
    {
        bool operationInProgress = _tokenRecountInProgress;
        _inventoryAccountingEnabled.Disabled = operationInProgress;
        _purchaseLimitEnabled.Disabled = operationInProgress;
        _serviceHopper1RecountButton.Disabled = operationInProgress || !_hopper1TokenRecountAvailable;
        _serviceHopper2RecountButton.Disabled = operationInProgress || !_hopper2TokenRecountAvailable;
        _serviceHopper1ManualAddButton.Disabled = operationInProgress || !_hopper1Configured;
        _serviceHopper2ManualAddButton.Disabled = operationInProgress || !_hopper2Configured;
    }

    public void SetServiceTestAvailability(bool available, string status)
    {
        _serviceTestAvailable = available;
        _testDispenseButton.Disabled = !available || _serviceTestInProgress;
        _testDispenseStatus.Text = string.IsNullOrWhiteSpace(status) ? "Тест не запущен." : status;
        if (!available)
        {
            _testDispenseConfirmationExpiresAt = 0;
        }
    }

    public void SetServiceTestResult(bool success, int dispensedTokens, bool canRunAgain)
    {
        _serviceTestInProgress = false;
        _serviceTestAvailable = canRunAgain;
        _testDispenseButton.Disabled = !canRunAgain;
        _testDispenseStatus.Text = success
            ? $"Тест завершён: подтверждена выдача {dispensedTokens} жетона."
            : $"Тест завершён ошибкой. Подтверждено жетонов: {dispensedTokens}.";
        RefreshDiagnostics();
        ValidateAllAndRefresh();
    }

    private void PopulateAllControls()
    {
        if (_workingCopy is null)
        {
            return;
        }

        PopulateAudioControls(_workingCopy.Audio);
        PopulatePricingControls();
        PopulatePaymentControls();
        PopulateBrandingControls();
        PopulateAnimatedBannerTextControls();
        PopulateSecurityControls();
        PopulateAdvertisementControls();
        PopulateTokenInventoryControls();
        PopulateMenuVisibilityControls();
        PopulateAppearanceControls();
        PopulateOperationalControls();
        ValidateAllAndRefresh();
    }

    private void PopulateOperationalControls()
    {
        if (_workingCopy is null)
        {
            return;
        }

        _updatingControls = true;
        _useMock.ButtonPressed = _workingCopy.Hardware.UseMock;
        _portName.Text = _workingCopy.Hardware.PortName;
        SelectOptionById(_baudRate, _workingCopy.Hardware.BaudRate);
        _reconnectSeconds.Value = _workingCopy.Hardware.ReconnectIntervalSeconds;
        _fullscreen.ButtonPressed = _workingCopy.Window.Fullscreen;
        _borderless.ButtonPressed = _workingCopy.Window.Borderless;
        _debugWindow.ButtonPressed = _workingCopy.Window.DebugWindowInDebugBuild;
        SelectOptionById(_targetFps, _workingCopy.Window.TargetFps);
        SelectOptionByText(_minimumLogLevel, _workingCopy.Logging.MinimumLevel);
        _maxLogFileSize.Value = _workingCopy.Logging.MaxFileSizeMb;
        _logRetentionDays.Value = _workingCopy.Logging.RetentionDays;
        _updatingControls = false;
        RefreshDiagnostics();
    }

    private void PopulateMenuVisibilityControls()
    {
        if (_workingCopy is null)
        {
            return;
        }

        _updatingControls = true;
        _showStockStatus.ButtonPressed = _workingCopy.MenuVisibility.ShowStockStatus;
        _showCustomText.Text = "ПОКАЗЫВАТЬ ПОЛЬЗОВАТЕЛЬСКИЙ ТЕКСТ";
        _showCustomText.ButtonPressed = _workingCopy.MenuVisibility.ShowCustomText;
        _showPromotionBlock.ButtonPressed = _workingCopy.MenuVisibility.ShowPromotionBlock;
        RefreshInteractivePetToggle();
        _updatingControls = false;
    }

    private void RefreshInteractivePetToggle()
    {
        bool previousUpdatingState = _updatingControls;
        _updatingControls = true;
        bool themeProvidesPet = _themeManager.CurrentTheme?.InteractivePetScene is not null;
        _showInteractivePet.Text = "ИНТЕРАКТИВНЫЙ ПИТОМЕЦ";
        _showInteractivePet.Disabled = !themeProvidesPet;
        _showInteractivePet.ButtonPressed = themeProvidesPet
            && _workingCopy?.MenuVisibility.ShowInteractivePet == true;
        _updatingControls = previousUpdatingState;
    }

    private void PopulateTokenInventoryControls()
    {
        if (_workingCopy is null)
        {
            return;
        }

        _updatingControls = true;
        _inventoryAccountingEnabled.ButtonPressed = _workingCopy.TokenInventory.Enabled;
        _purchaseLimitEnabled.ButtonPressed = _workingCopy.TokenInventory.PurchaseLimitEnabled;
        _updatingControls = false;
        ApplyTokenInventoryUiPolicy();
    }

    private void ApplyTokenInventoryUiPolicy()
    {
        bool enabled = _workingCopy?.TokenInventory.Enabled ?? true;
        bool purchaseLimitEnabled = _workingCopy?.TokenInventory.PurchaseLimitEnabled ?? true;
        _serviceInventoryDescription.Text = !enabled
            ? "Учёт отключён. Остаток не записывается и не ограничивает покупку."
            : purchaseLimitEnabled
                ? "Остаток учитывается и ограничивает покупку."
                : "Остаток учитывается, но не ограничивает покупку.";
        _purchaseLimitEnabled.Visible = enabled;
        _serviceInventoryCount.Visible = enabled;
        _serviceHopper1Label.Visible = enabled && _hopper1Configured;
        _serviceHopper1RecountButton.Visible = enabled && _hopper1Configured;
        _serviceHopper1ManualAddButton.Visible = enabled && _hopper1Configured;
        _serviceHopper2Label.Visible = enabled && _hopper2Configured;
        _serviceHopper2RecountButton.Visible = enabled && _hopper2Configured;
        _serviceHopper2ManualAddButton.Visible = enabled && _hopper2Configured;
        _serviceInventoryStatus.Visible = enabled;
        RefreshTokenServiceButtons();
    }

    private void PopulateAppearanceControls()
    {
        if (_workingCopy is null)
        {
            return;
        }

        _updatingControls = true;
        _externalDlcEnabled.ButtonPressed = _workingCopy.Themes.ExternalDlcEnabled;
        _externalDlcPath.Text = _workingCopy.Themes.ExternalDlcPath;
        _reducedEffects.ButtonPressed = _workingCopy.Themes.ReducedEffects;
        _updatingControls = false;
        ValidateAllAndRefresh();
    }

    private void PopulateAdvertisementControls()
    {
        if (_workingCopy is null)
        {
            return;
        }

        _updatingControls = true;
        _advertisementEnabled.ButtonPressed = _workingCopy.Advertisement.Enabled;
        _advertisementMuted.ButtonPressed = _workingCopy.Advertisement.Muted;
        _advertisementFallbackText.Text = _workingCopy.Advertisement.FallbackText;
        _advertisementPosterPath.Text = _workingCopy.Advertisement.PosterPath;
        _updatingControls = false;
        RebuildAdvertisementVideoRows();
        ValidateAllAndRefresh();
    }

    private void PopulateBrandingControls()
    {
        if (_workingCopy is null)
        {
            return;
        }

        _updatingControls = true;
        _applicationName.Text = _workingCopy.Branding.ApplicationName;
        _shortText.Text = _workingCopy.Branding.ShortText;
        _supportPhone.Text = _workingCopy.Branding.SupportPhone;
        _updatingControls = false;
        RefreshBrandingPreview();
        ValidateAllAndRefresh();
    }

    private void ApplyBrandingUiPolicy()
    {
        _applicationName.Visible = false;
        _applicationNameError.Visible = false;
        _brandingPreviewPanel.Visible = false;

        if (_brandingPanel.FindChild("ApplicationNameLabel", recursive: true, owned: false) is CanvasItem applicationNameLabel)
        {
            applicationNameLabel.Visible = false;
        }

        if (_brandingPanel.FindChild("ShortTextLabel", recursive: true, owned: false) is Label shortTextLabel)
        {
            shortTextLabel.Text = "СВОЙ ТЕКСТ • ДО 48 ЗНАКОВ";
        }

        if (_brandingPanel.FindChild("Description", recursive: true, owned: false) is Label description)
        {
            description.Text = "Здесь можно изменить свой текст и телефон для покупателей.";
        }
    }

    private void PopulateAnimatedBannerTextControls()
    {
        if (_workingCopy is null)
        {
            return;
        }

        _updatingControls = true;
        _cashPaymentBannerText.Text = _workingCopy.AnimatedBannerTexts.CashPayment;
        _cardAmountBannerText.Text = _workingCopy.AnimatedBannerTexts.CardAmount;
        _cardCustomAmountBannerText.Text = _workingCopy.AnimatedBannerTexts.CardCustomAmount;
        _cardTerminalCountdownBannerText.Text = _workingCopy.AnimatedBannerTexts.CardTerminalCountdownTemplate;
        _updatingControls = false;
    }

    private T GetAnimatedBannerTextControl<T>(string name)
        where T : Node => _animatedBannerTextsPanel.GetNode<T>($"Margin/Content/{name}");

    private void PopulateSecurityControls()
    {
        if (_workingCopy is null)
        {
            return;
        }

        _updatingControls = true;
        _servicePinEnabled.ButtonPressed = _workingCopy.Security.ServicePinEnabled;
        _updatingControls = false;
        RefreshServicePinStatus();
    }

    private void PopulateAudioControls(AudioSettings settings)
    {
        _updatingControls = true;
        _masterVolumeSlider.Value = settings.MasterVolumePercent;
        _muteButton.ButtonPressed = settings.Muted;
        _musicVolumeSlider.Value = settings.MusicVolumePercent;
        _soundEffectsVolumeSlider.Value = settings.SoundEffectsVolumePercent;
        _advertisementVolumeSlider.Value = settings.AdvertisementVolumePercent;
        _masterVolumeValue.Text = FormatPercent(settings.MasterVolumePercent);
        _musicVolumeValue.Text = FormatPercent(settings.MusicVolumePercent);
        _soundEffectsVolumeValue.Text = FormatPercent(settings.SoundEffectsVolumePercent);
        _advertisementVolumeValue.Text = FormatPercent(settings.AdvertisementVolumePercent);
        _updatingControls = false;
    }

    private void PopulatePricingControls()
    {
        if (_workingCopy is null)
        {
            return;
        }

        SortBonusRules();
        _updatingControls = true;
        _tokenPrice.Value = _workingCopy.Pricing.TokenPriceRubles;
        _updatingControls = false;
        RebuildBonusRows();
    }

    private void PopulatePaymentControls()
    {
        if (_workingCopy is null)
        {
            return;
        }

        SortPaymentPresets();
        _updatingControls = true;
        _cashEnabled.ButtonPressed = _workingCopy.Payments.CashEnabled;
        _cardEnabled.ButtonPressed = _workingCopy.Payments.CardEnabled;
        _maximumCardAmount.Value = _workingCopy.Pricing.MaxCardAmountRubles;
        _customAmountStep.Value = _workingCopy.Pricing.CustomAmountStepRubles;
        _sessionIdleTimeout.Value = _workingCopy.Timeouts.SessionIdleSeconds;
        _cashPaymentTimeout.Value = _workingCopy.Timeouts.CashPaymentSeconds;
        _cashlessPaymentTimeout.Value = _workingCopy.Timeouts.CashlessPaymentSeconds;
        _successTimeout.Value = _workingCopy.Timeouts.SuccessSeconds;
        _updatingControls = false;
        RebuildPaymentPresetRows();
        ValidateAllAndRefresh();
    }

    private void ShowGeneralSection()
    {
        SetSectionVisibility(SettingsSection.General);
    }

    private void ShowAudioSection()
    {
        SetSectionVisibility(SettingsSection.Audio);
    }

    private void ShowAdvertisementSection()
    {
        SetSectionVisibility(SettingsSection.Advertisement);
    }

    private void ShowAppearanceSection()
    {
        SetSectionVisibility(SettingsSection.Appearance);
    }

    private void ShowPricingSection()
    {
        SetSectionVisibility(SettingsSection.Pricing);
    }

    private void ShowPaymentSection()
    {
        SetSectionVisibility(SettingsSection.Payment);
    }

    private void ShowServiceSection()
    {
        SetSectionVisibility(SettingsSection.Service);
    }

    private void ShowEquipmentSection()
    {
        if (_accessLevel is ServiceAccessLevel.Engineer or ServiceAccessLevel.Debug)
        {
            SetSectionVisibility(SettingsSection.Equipment);
        }
    }

    private void ShowDiagnosticsSection()
    {
        RefreshDiagnostics();
        SetSectionVisibility(SettingsSection.Diagnostics);
    }

    private void SetSectionVisibility(SettingsSection section)
    {
        _onScreenKeyboard?.Hide();
        bool showGeneral = section == SettingsSection.General;
        bool showAudio = section == SettingsSection.Audio;
        bool showAdvertisement = section == SettingsSection.Advertisement;
        bool showAppearance = false;
        bool showPricing = section == SettingsSection.Pricing;
        bool showPayment = section == SettingsSection.Payment;
        bool showService = section == SettingsSection.Service;
        bool showEquipment = false;
        bool showDiagnostics = false;
        _generalSectionButton.ButtonPressed = showGeneral;
        _audioSectionButton.ButtonPressed = showAudio;
        _advertisementSectionButton.ButtonPressed = showAdvertisement;
        _appearanceSectionButton.ButtonPressed = showAppearance;
        _pricingSectionButton.ButtonPressed = showPricing;
        _paymentSectionButton.ButtonPressed = showPayment;
        _serviceSectionButton.ButtonPressed = showService;
        _equipmentSectionButton.ButtonPressed = showEquipment;
        _diagnosticsSectionButton.ButtonPressed = showDiagnostics;
        _brandingPanel.Visible = showGeneral;
        _brandingPreviewPanel.Visible = false;
        _animatedBannerTextsPanel.Visible = showGeneral;
        _securityPanel.Visible = showGeneral;
        _masterPanel.Visible = showAudio;
        _musicVolumePanel.Visible = showAudio;
        _soundEffectsVolumePanel.Visible = showAudio;
        _advertisementVolumePanel.Visible = showAudio;
        _testPanel.Visible = showAudio;
        _advertisementSettingsPanel.Visible = showAdvertisement;
        _advertisementPosterPanel.Visible = showAdvertisement;
        _advertisementPlaylistPanel.Visible = showAdvertisement;
        _advertisementPreviewPanel.Visible = false;
        _appearanceThemePanel.Visible = false;
        _appearanceEffectsPanel.Visible = false;
        _appearanceNoticePanel.Visible = false;
        _pricePanel.Visible = showPricing;
        _bonusPanel.Visible = showPricing;
        _previewPanel.Visible = showPricing;
        _paymentMethodsPanel.Visible = showPayment;
        _cardOptionsPanel.Visible = showPayment;
        _timeoutsPanel.Visible = showPayment;
        _serviceInventoryPanel.Visible = showService;
        _menuVisibilityPanel.Visible = showService;
        _equipmentConnectionPanel.Visible = false;
        _equipmentWindowPanel.Visible = false;
        _diagnosticsStatusPanel.Visible = false;
        _diagnosticsLoggingPanel.Visible = false;
        _diagnosticsTestPanel.Visible = false;
        _scroll.ScrollVertical = 0;
    }

    private void OnGeneralSectionPressed() => ShowGeneralSection();

    private void OnAudioSectionPressed() => ShowAudioSection();

    private void OnAdvertisementSectionPressed() => ShowAdvertisementSection();

    private void OnAppearanceSectionPressed() => ShowAppearanceSection();

    private void OnPricingSectionPressed() => ShowPricingSection();

    private void OnPaymentSectionPressed() => ShowPaymentSection();

    private void OnServiceSectionPressed() => ShowServiceSection();

    private void OnServiceHopper1RecountPressed() => OpenServiceRecountDialog(1);

    private void OnServiceHopper2RecountPressed() => OpenServiceRecountDialog(2);

    private void OnServiceHopper1ManualAddPressed() => OpenServiceManualAddDialog(1);

    private void OnServiceHopper2ManualAddPressed() => OpenServiceManualAddDialog(2);

    private void OpenServiceRecountDialog(int hopperNumber)
    {
        if (_workingCopy?.TokenInventory.Enabled != true || !IsHopperConfigured(hopperNumber))
        {
            return;
        }

        bool available = hopperNumber == 1
            ? _hopper1TokenRecountAvailable
            : _hopper2TokenRecountAvailable;
        if (!available || _tokenRecountInProgress)
        {
            return;
        }

        _activeServiceHopper = NormalizeHopperNumber(hopperNumber);
        OpenServiceDialog(
            ServiceDialogMode.ConfirmRecountStart,
            $"ПЕРЕСЧИТАТЬ ХОППЕР {_activeServiceHopper}?",
            "Хоппер выдаст все жетоны. Программа сама посчитает сигналы контроллера и запишет новый остаток.",
            0,
            "НАЧАТЬ");
    }

    private void OpenServiceManualAddDialog(int hopperNumber)
    {
        if (_workingCopy?.TokenInventory.Enabled != true
            || _tokenRecountInProgress
            || !IsHopperConfigured(hopperNumber))
        {
            return;
        }

        _activeServiceHopper = NormalizeHopperNumber(hopperNumber);
        OpenServiceDialog(
            ServiceDialogMode.ManualEntry,
            $"ДОБАВИТЬ В ХОППЕР {_activeServiceHopper}",
            "Введите количество жетонов, которое нужно добавить к остатку этого хоппера.",
            0,
            "ДАЛЕЕ");
    }

    private bool IsHopperConfigured(int hopperNumber) =>
        hopperNumber == 1 ? _hopper1Configured : _hopper2Configured;

    private void OnServiceDialogCancelPressed()
    {
        if (_serviceDialogMode == ServiceDialogMode.ManualConfirm)
        {
            OpenServiceDialog(
                ServiceDialogMode.ManualEntry,
                $"ДОБАВИТЬ В ХОППЕР {_activeServiceHopper}",
                "Исправьте количество жетонов для добавления.",
                (int)Math.Round(_serviceDialogAmount.Value),
                "ДАЛЕЕ");
            return;
        }

        CloseServiceDialog();
    }

    private void OnServiceDialogConfirmPressed()
    {
        int amount = Math.Clamp((int)Math.Round(_serviceDialogAmount.Value), 0, 1_000_000);
        switch (_serviceDialogMode)
        {
            case ServiceDialogMode.ConfirmRecountStart:
                CloseServiceDialog();
                _tokenRecountInProgress = true;
                RefreshTokenServiceButtons();
                _serviceInventoryStatus.Text = $"Запускаем пересчёт хоппера {_activeServiceHopper}…";
                TokenRecountRequested?.Invoke(_activeServiceHopper);
                break;
            case ServiceDialogMode.ManualEntry:
                if (amount <= 0)
                {
                    _serviceDialogDescription.Text = "Введите количество больше нуля.";
                    return;
                }

                OpenServiceDialog(
                    ServiceDialogMode.ManualConfirm,
                    "ПРОВЕРЬТЕ КОЛИЧЕСТВО",
                    $"Добавить {FormatTokenCount(amount)} в хоппер {_activeServiceHopper}?",
                    amount,
                    "ДОБАВИТЬ");
                break;
            case ServiceDialogMode.ManualConfirm:
                CloseServiceDialog();
                ManualTokenAdditionConfirmed?.Invoke(_activeServiceHopper, amount);
                _serviceInventoryStatus.Text = $"Жетоны добавлены в хоппер {_activeServiceHopper}.";
                break;
        }
    }

    private void OpenServiceDialog(
        ServiceDialogMode mode,
        string title,
        string description,
        int amount,
        string confirmText)
    {
        _onScreenKeyboard?.Hide();
        _serviceDialogMode = mode;
        _serviceDialogTitle.Text = title;
        _serviceDialogDescription.Text = description;
        _serviceDialogAmount.Value = Math.Clamp(amount, 0, 1_000_000);
        _serviceDialogAmount.Visible = mode != ServiceDialogMode.ConfirmRecountStart;
        _serviceDialogAmount.Editable = mode != ServiceDialogMode.ManualConfirm;
        _serviceDialogConfirmButton.Text = confirmText;
        _serviceDialogOverlay.Visible = true;
        if (mode == ServiceDialogMode.ManualEntry)
        {
            ShowServiceDialogNumericKeyboard();
        }
    }

    private async void ShowServiceDialogNumericKeyboard()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (_serviceDialogMode != ServiceDialogMode.ManualEntry
            || !GodotObject.IsInstanceValid(_serviceDialogAmount)
            || !_serviceDialogAmount.IsVisibleInTree())
        {
            return;
        }

        LineEdit amountEdit = _serviceDialogAmount.GetLineEdit();
        amountEdit.GrabFocus();
        amountEdit.SelectAll();
        _onScreenKeyboard?.ShowNumericField(_serviceDialogAmount);
    }

    private void CloseServiceDialog()
    {
        _onScreenKeyboard?.Hide();
        _serviceDialogMode = ServiceDialogMode.None;
        if (GodotObject.IsInstanceValid(_serviceDialogOverlay))
        {
            _serviceDialogOverlay.Visible = false;
        }
    }

    private void OnEquipmentSectionPressed() => ShowEquipmentSection();

    private void OnDiagnosticsSectionPressed() => ShowDiagnosticsSection();

    private void OnMasterVolumeChanged(double value)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Audio.MasterVolumePercent = (int)Math.Round(value);
        _masterVolumeValue.Text = FormatPercent(_workingCopy.Audio.MasterVolumePercent);
        PreviewAudio();
        ScheduleAutoSave();
    }

    private void OnMuteToggled(bool toggledOn)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Audio.Muted = toggledOn;
        PreviewAudio();
        ScheduleAutoSave();
    }

    private void OnMusicVolumeChanged(double value)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Audio.MusicVolumePercent = (int)Math.Round(value);
        _musicVolumeValue.Text = FormatPercent(_workingCopy.Audio.MusicVolumePercent);
        PreviewAudio();
        ScheduleAutoSave();
    }

    private void OnSoundEffectsVolumeChanged(double value)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Audio.SoundEffectsVolumePercent = (int)Math.Round(value);
        _soundEffectsVolumeValue.Text = FormatPercent(_workingCopy.Audio.SoundEffectsVolumePercent);
        PreviewAudio();
        ScheduleAutoSave();
    }

    private void OnAdvertisementVolumeChanged(double value)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Audio.AdvertisementVolumePercent = (int)Math.Round(value);
        _advertisementVolumeValue.Text = FormatPercent(_workingCopy.Audio.AdvertisementVolumePercent);
        PreviewAudio();
        ScheduleAutoSave();
    }

    private void OnTokenPriceChanged(double value)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Pricing.TokenPriceRubles = (int)Math.Round(value);
        ValidateAllAndRefresh();
    }

    private void OnCashEnabledToggled(bool enabled)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Payments.CashEnabled = enabled;
        ValidateAllAndRefresh();
    }

    private void OnCardEnabledToggled(bool enabled)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Payments.CardEnabled = enabled;
        if ((_workingCopy.Pricing.PaymentPresets?.Length ?? 0) == 0)
        {
            RebuildPaymentPresetRows();
        }
        ValidateAllAndRefresh();
    }

    private void OnMaximumCardAmountChanged(double value)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Pricing.MaxCardAmountRubles = (int)Math.Round(value);
        ValidateAllAndRefresh();
    }

    private void OnCustomAmountStepChanged(double value)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Pricing.CustomAmountStepRubles = (int)Math.Round(value);
        ValidateAllAndRefresh();
    }

    private void OnSessionIdleTimeoutChanged(double value)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Timeouts.SessionIdleSeconds = (int)Math.Round(value);
        ValidateAllAndRefresh();
    }

    private void OnCashPaymentTimeoutChanged(double value)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Timeouts.CashPaymentSeconds = (int)Math.Round(value);
        ValidateAllAndRefresh();
    }

    private void OnCashlessPaymentTimeoutChanged(double value)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Timeouts.CashlessPaymentSeconds = (int)Math.Round(value);
        ValidateAllAndRefresh();
    }

    private void OnSuccessTimeoutChanged(double value)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Timeouts.SuccessSeconds = (int)Math.Round(value);
        ValidateAllAndRefresh();
    }

    private void OnApplicationNameChanged(string value)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Branding.ApplicationName = value;
        RefreshBrandingPreview();
        ValidateAllAndRefresh();
    }

    private void OnSupportPhoneChanged(string value)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Branding.SupportPhone = value;
        RefreshBrandingPreview();
        ValidateAllAndRefresh();
    }

    private void OnShortTextChanged(string value)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Branding.ShortText = value;
        RefreshBrandingPreview();
        ValidateAllAndRefresh();
    }

    private void OnCashPaymentBannerTextChanged(string value)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.AnimatedBannerTexts.CashPayment = value;
        ValidateAllAndRefresh();
    }

    private void OnCardAmountBannerTextChanged(string value)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.AnimatedBannerTexts.CardAmount = value;
        ValidateAllAndRefresh();
    }

    private void OnCardCustomAmountBannerTextChanged(string value)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.AnimatedBannerTexts.CardCustomAmount = value;
        ValidateAllAndRefresh();
    }

    private void OnCardTerminalCountdownBannerTextChanged(string value)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.AnimatedBannerTexts.CardTerminalCountdownTemplate = value;
        ValidateAllAndRefresh();
    }

    private void OnServicePinEnabledToggled(bool enabled)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Security.ServicePinEnabled = enabled;
        ServiceAccessPolicy.ClearPin(_workingCopy.Security);
        RefreshServicePinStatus();
        ValidateAllAndRefresh();
    }

    private void OnResetServicePinPressed()
    {
        if (_workingCopy is null || !_workingCopy.Security.ServicePinEnabled)
        {
            return;
        }

        ServiceAccessPolicy.ClearPin(_workingCopy.Security);
        RefreshServicePinStatus();
        ValidateAllAndRefresh();
    }

    private void OnAdvertisementEnabledToggled(bool enabled)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Advertisement.Enabled = enabled;
        ValidateAllAndRefresh();
    }

    private void OnShowStockStatusToggled(bool enabled)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.MenuVisibility.ShowStockStatus = enabled;
        ValidateAllAndRefresh();
    }

    private void OnInventoryAccountingEnabledToggled(bool enabled)
    {
        if (_updatingControls || _workingCopy is null || _tokenRecountInProgress)
        {
            return;
        }

        _workingCopy.TokenInventory.Enabled = enabled;
        ApplyTokenInventoryUiPolicy();
        ValidateAllAndRefresh();
    }

    private void OnPurchaseLimitEnabledToggled(bool enabled)
    {
        if (_updatingControls || _workingCopy is null || _tokenRecountInProgress)
        {
            return;
        }

        _workingCopy.TokenInventory.PurchaseLimitEnabled = enabled;
        ApplyTokenInventoryUiPolicy();
        ValidateAllAndRefresh();
    }

    private void OnShowCustomTextToggled(bool enabled)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.MenuVisibility.ShowCustomText = enabled;
        ValidateAllAndRefresh();
    }

    private void OnShowPromotionBlockToggled(bool enabled)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.MenuVisibility.ShowPromotionBlock = enabled;
        ValidateAllAndRefresh();
    }

    private void OnShowInteractivePetToggled(bool enabled)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.MenuVisibility.ShowInteractivePet = enabled;
        ValidateAllAndRefresh();
    }

    private void OnAdvertisementMutedToggled(bool muted)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Advertisement.Muted = muted;
        ValidateAllAndRefresh();
    }

    private void OnAdvertisementFallbackTextChanged(string value)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Advertisement.FallbackText = value;
        ValidateAllAndRefresh();
    }

    private void OnAdvertisementPosterPathChanged(string value)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Advertisement.PosterPath = value;
        ValidateAllAndRefresh();
    }

    private void OnSelectAdvertisementPosterPressed() =>
        OpenAdvertisementFileDialog(AdvertisementAssetType.Poster);

    private void OnExternalDlcEnabledToggled(bool enabled)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Themes.ExternalDlcEnabled = enabled;
        ValidateAllAndRefresh();
    }

    private void OnExternalDlcPathChanged(string value)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Themes.ExternalDlcPath = value;
        ValidateAllAndRefresh();
    }

    private void OnReducedEffectsToggled(bool enabled)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Themes.ReducedEffects = enabled;
        _themeManager.SetReducedEffects(enabled);
        ValidateAllAndRefresh();
    }

    private bool CanEditEngineering => _accessLevel is ServiceAccessLevel.Engineer or ServiceAccessLevel.Debug;

    private void OnUseMockToggled(bool enabled)
    {
        if (_updatingControls || _workingCopy is null || !CanEditEngineering)
        {
            return;
        }

        _workingCopy.Hardware.UseMock = enabled;
        ValidateAllAndRefresh();
    }

    private void OnPortNameChanged(string value)
    {
        if (_updatingControls || _workingCopy is null || !CanEditEngineering)
        {
            return;
        }

        _workingCopy.Hardware.PortName = value;
        ValidateAllAndRefresh();
    }

    private void OnBaudRateSelected(long index)
    {
        if (_updatingControls || _workingCopy is null || !CanEditEngineering)
        {
            return;
        }

        _workingCopy.Hardware.BaudRate = _baudRate.GetItemId((int)index);
        ValidateAllAndRefresh();
    }

    private void OnReconnectSecondsChanged(double value)
    {
        if (_updatingControls || _workingCopy is null || !CanEditEngineering)
        {
            return;
        }

        _workingCopy.Hardware.ReconnectIntervalSeconds = (int)Math.Round(value);
        ValidateAllAndRefresh();
    }

    private void OnFullscreenToggled(bool enabled)
    {
        if (_updatingControls || _workingCopy is null || !CanEditEngineering)
        {
            return;
        }

        _workingCopy.Window.Fullscreen = enabled;
        ValidateAllAndRefresh();
    }

    private void OnBorderlessToggled(bool enabled)
    {
        if (_updatingControls || _workingCopy is null || !CanEditEngineering)
        {
            return;
        }

        _workingCopy.Window.Borderless = enabled;
        ValidateAllAndRefresh();
    }

    private void OnDebugWindowToggled(bool enabled)
    {
        if (_updatingControls || _workingCopy is null || !CanEditEngineering)
        {
            return;
        }

        _workingCopy.Window.DebugWindowInDebugBuild = enabled;
        ValidateAllAndRefresh();
    }

    private void OnTargetFpsSelected(long index)
    {
        if (_updatingControls || _workingCopy is null || !CanEditEngineering)
        {
            return;
        }

        _workingCopy.Window.TargetFps = _targetFps.GetItemId((int)index);
        ValidateAllAndRefresh();
    }

    private void OnMinimumLogLevelSelected(long index)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Logging.MinimumLevel = _minimumLogLevel.GetItemText((int)index);
        ValidateAllAndRefresh();
    }

    private void OnMaxLogFileSizeChanged(double value)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Logging.MaxFileSizeMb = (int)Math.Round(value);
        ValidateAllAndRefresh();
    }

    private void OnLogRetentionDaysChanged(double value)
    {
        if (_updatingControls || _workingCopy is null)
        {
            return;
        }

        _workingCopy.Logging.RetentionDays = (int)Math.Round(value);
        ValidateAllAndRefresh();
    }

    private void OnTestDispensePressed()
    {
        if (!_serviceTestAvailable)
        {
            _testDispenseStatus.Text = "Тест недоступен: контроллер не готов или выполняется операция.";
            return;
        }

        ulong now = Time.GetTicksMsec();
        if (_testDispenseConfirmationExpiresAt == 0 || now > _testDispenseConfirmationExpiresAt)
        {
            _testDispenseConfirmationExpiresAt = now + 10000UL;
            _testDispenseButton.Text = "ПОДТВЕРДИТЬ ВЫДАЧУ 1 ЖЕТОНА";
            _testDispenseStatus.Text = "Нажмите кнопку ещё раз в течение 10 секунд.";
            return;
        }

        _testDispenseConfirmationExpiresAt = 0;
        _testDispenseButton.Text = "ТЕСТ: ВЫДАТЬ 1 ЖЕТОН";
        _testDispenseButton.Disabled = true;
        _serviceTestInProgress = true;
        _autoSaveTimer.Stop();
        _testDispenseStatus.Text = "Команда отправлена; ожидается подтверждение контроллера.";
        TestDispenseRequested?.Invoke();
    }

    private void OnAddAdvertisementVideoPressed()
    {
        if (_workingCopy is null)
        {
            return;
        }

        string[] paths = _workingCopy.Advertisement.VideoPaths ?? Array.Empty<string>();
        if (paths.Length >= AdvertisementSettingsValidator.MaximumVideoPaths)
        {
            _advertisementPlaylistError.Text =
                $"Можно добавить не больше {AdvertisementSettingsValidator.MaximumVideoPaths} промо-роликов.";
            return;
        }

        OpenAdvertisementFileDialog(AdvertisementAssetType.Video);
    }

    private void OpenAdvertisementFileDialog(AdvertisementAssetType assetType)
    {
        if (_workingCopy is null)
        {
            return;
        }

        _onScreenKeyboard?.Hide();
        _advertisementMediaSelectionType = assetType;
        _advertisementMediaFileDialog.Title = assetType == AdvertisementAssetType.Poster
            ? "ВЫБЕРИТЕ КАРТИНКУ ДЛЯ ПРОМО"
            : "ВЫБЕРИТЕ ПРОМО-РОЛИК";
        _advertisementMediaFileDialog.Filters = assetType == AdvertisementAssetType.Poster
            ? ["*.png, *.jpg, *.jpeg, *.webp; Изображения PNG, JPG, WEBP"]
            : ["*.ogv, *.mp4, *.mov, *.m4v; Видео OGV, MP4, MOV, M4V"];
        _advertisementMediaFileDialog.PopupCenteredRatio(0.85f);
    }

    private void OnAdvertisementMediaFileSelected(string selectedPath)
    {
        if (_workingCopy is null || _advertisementMediaSelectionType is null)
        {
            return;
        }

        string normalizedPath = AdvertisementVideoLibrary.NormalizeSelectedVideo(selectedPath);
        AdvertisementAssetType selectionType = _advertisementMediaSelectionType.Value;
        _advertisementMediaSelectionType = null;
        if (selectionType == AdvertisementAssetType.Poster)
        {
            _updatingControls = true;
            _workingCopy.Advertisement.PosterPath = normalizedPath;
            _advertisementPosterPath.Text = normalizedPath;
            _updatingControls = false;
            ValidateAllAndRefresh();
            return;
        }

        string[] paths = _workingCopy.Advertisement.VideoPaths ?? Array.Empty<string>();
        if (paths.Length >= AdvertisementSettingsValidator.MaximumVideoPaths)
        {
            _advertisementPlaylistError.Text =
                $"Можно добавить не больше {AdvertisementSettingsValidator.MaximumVideoPaths} промо-роликов.";
            return;
        }

        normalizedPath = AdvertisementVideoLibrary.NormalizeSelectedVideo(normalizedPath);

        _workingCopy.Advertisement.VideoPaths = [.. paths, normalizedPath];
        RebuildAdvertisementVideoRows();
        ValidateAllAndRefresh();
    }

    private void OnAddPaymentPresetPressed()
    {
        if (_workingCopy is null)
        {
            return;
        }

        PaymentPreset[] presets = _workingCopy.Pricing.PaymentPresets ?? Array.Empty<PaymentPreset>();
        if (presets.Length >= MaximumEditablePaymentPresets)
        {
            _paymentPresetsError.Text = $"Можно добавить не больше {MaximumEditablePaymentPresets} сумм.";
            return;
        }

        int step = Math.Max(1, _workingCopy.Pricing.CustomAmountStepRubles);
        int price = Math.Max(1, _workingCopy.Pricing.TokenPriceRubles);
        int lastAmount = presets.Select(preset => preset?.AmountRubles ?? 0).DefaultIfEmpty(0).Max();
        int suggested = presets.Length == 0 ? Math.Max(price, 100) : lastAmount + step;
        int aligned = ((suggested + step - 1) / step) * step;
        int amount = Math.Clamp(
            aligned,
            price,
            Math.Max(price, _workingCopy.Pricing.MaxCardAmountRubles));

        _workingCopy.Pricing.PaymentPresets =
        [
            .. presets.Where(preset => preset is not null),
            new PaymentPreset { AmountRubles = amount },
        ];
        SortPaymentPresets();
        RebuildPaymentPresetRows();
        ValidateAllAndRefresh();
    }

    private void InitializeOnScreenKeyboard()
    {
        var textKeys = new List<Button>(OnScreenKeyboardState.TextKeyCount);
        for (int index = 0; index < OnScreenKeyboardState.TextKeyCount; index++)
        {
            int row = index < 12 ? 0 : index < 23 ? 1 : 2;
            textKeys.Add(GetNode<Button>(
                $"SafeMargin/KeyboardOverlay/TextKeyboard/Content/LetterRows/Row{row}/Key_{index:00}"));
        }

        var digits = new List<Button>(10);
        for (int digit = 0; digit <= 9; digit++)
        {
            digits.Add(GetNode<Button>(
                $"SafeMargin/KeyboardOverlay/NumericKeyboard/Content/Grid/Digit{digit}"));
        }

        _onScreenKeyboard = new OnScreenKeyboardController(
            new OnScreenKeyboardView
            {
                TextKeyboard = GetNode<Control>("SafeMargin/KeyboardOverlay/TextKeyboard"),
                TextHide = GetNode<Button>(
                    "SafeMargin/KeyboardOverlay/TextKeyboard/Content/Header/HideButton"),
                TextKeys = textKeys,
                Shift = GetNode<Button>(
                    "SafeMargin/KeyboardOverlay/TextKeyboard/Content/PrimaryActions/ShiftButton"),
                Language = GetNode<Button>(
                    "SafeMargin/KeyboardOverlay/TextKeyboard/Content/PrimaryActions/LanguageButton"),
                Symbols = GetNode<Button>(
                    "SafeMargin/KeyboardOverlay/TextKeyboard/Content/PrimaryActions/SymbolsButton"),
                TextBackspace = GetNode<Button>(
                    "SafeMargin/KeyboardOverlay/TextKeyboard/Content/PrimaryActions/BackspaceButton"),
                TextClear = GetNode<Button>(
                    "SafeMargin/KeyboardOverlay/TextKeyboard/Content/SecondaryActions/ClearButton"),
                Space = GetNode<Button>(
                    "SafeMargin/KeyboardOverlay/TextKeyboard/Content/SecondaryActions/SpaceButton"),
                CaretLeft = GetNode<Button>(
                    "SafeMargin/KeyboardOverlay/TextKeyboard/Content/SecondaryActions/CaretLeftButton"),
                CaretRight = GetNode<Button>(
                    "SafeMargin/KeyboardOverlay/TextKeyboard/Content/SecondaryActions/CaretRightButton"),
                NumericKeyboard = GetNode<Control>("SafeMargin/KeyboardOverlay/NumericKeyboard"),
                NumericHide = GetNode<Button>(
                    "SafeMargin/KeyboardOverlay/NumericKeyboard/Content/Header/HideButton"),
                Digits = digits,
                NumericBackspace = GetNode<Button>(
                    "SafeMargin/KeyboardOverlay/NumericKeyboard/Content/Grid/BackspaceButton"),
                NumericClear = GetNode<Button>(
                    "SafeMargin/KeyboardOverlay/NumericKeyboard/Content/Grid/ClearButton"),
            },
            _scroll);
        _onScreenKeyboard.VisibilityChanged += OnScreenKeyboardVisibilityChanged;
        OnScreenKeyboardVisibilityChanged(_onScreenKeyboard.IsVisible);
    }

    private void OnScreenKeyboardVisibilityChanged(bool isVisible)
    {
        if (GodotObject.IsInstanceValid(_saveAndExitButton))
        {
            _saveAndExitButton.Disabled = isVisible;
            _saveAndExitButton.MouseFilter = isVisible
                ? Control.MouseFilterEnum.Ignore
                : _saveAndExitMouseFilter;
        }

        if (GodotObject.IsInstanceValid(_footer))
        {
            _footer.MouseFilter = isVisible
                ? Control.MouseFilterEnum.Ignore
                : _footerMouseFilter;
        }
    }

    private void RefreshKeyboardInputBindings()
    {
        if (_onScreenKeyboard is null || !GodotObject.IsInstanceValid(_scroll))
        {
            return;
        }

        _onScreenKeyboard.ClearRegisteredFields();
        RegisterEditableDescendants(_scroll);
        _onScreenKeyboard.RegisterNumericField(_serviceDialogAmount);
    }

    private void RegisterEditableDescendants(Node node)
    {
        if (_onScreenKeyboard is null)
        {
            return;
        }

        if (node is SpinBox spinBox)
        {
            _onScreenKeyboard.RegisterNumericField(spinBox);
            return;
        }

        if (node is LineEdit lineEdit)
        {
            _onScreenKeyboard.RegisterTextField(lineEdit);
            return;
        }

        foreach (Node child in node.GetChildren(includeInternal: true))
        {
            RegisterEditableDescendants(child);
        }
    }

    private void BindThemeCollectionSlots()
    {
        const string bonusBase = "settings.scroll.content.bonus_panel.margin.content.rows";
        _themeBonusEmptyState = GetThemeBinding<Label>($"{bonusBase}.empty_state");
        for (int index = 0; index < MaximumEditableBonusRules; index++)
        {
            int slotIndex = index;
            string slotBase = $"{bonusBase}.slot_{index:00}";
            var slot = new ThemeBonusSlot(
                GetThemeBinding<Control>(slotBase),
                GetThemeBinding<SpinBox>($"{slotBase}.threshold"),
                GetThemeBinding<SpinBox>($"{slotBase}.bonus"),
                GetThemeBinding<Button>($"{slotBase}.remove_button"),
                GetThemeBinding<Label>($"{slotBase}.error"),
                value => OnThemeBonusThresholdChanged(slotIndex, value),
                value => OnThemeBonusValueChanged(slotIndex, value),
                () => RemoveThemeBonusRule(slotIndex));
            slot.Subscribe();
            _themeBonusSlots.Add(slot);
        }

        const string paymentBase =
            "settings.scroll.content.card_options_panel.margin.content.preset_rows";
        _themePaymentPresetEmptyState = GetThemeBinding<Label>($"{paymentBase}.empty_state");
        for (int index = 0; index < MaximumEditablePaymentPresets; index++)
        {
            int slotIndex = index;
            string slotBase = $"{paymentBase}.slot_{index:00}";
            var slot = new ThemePaymentPresetSlot(
                GetThemeBinding<Control>(slotBase),
                GetThemeBinding<SpinBox>($"{slotBase}.amount"),
                GetThemeBinding<Button>($"{slotBase}.remove_button"),
                GetThemeBinding<Label>($"{slotBase}.error"),
                value => OnThemePaymentPresetChanged(slotIndex, value),
                () => RemoveThemePaymentPreset(slotIndex));
            slot.Subscribe();
            _themePaymentPresetSlots.Add(slot);
        }

        const string advertisementBase =
            "settings.scroll.content.advertisement_playlist_panel.margin.content.rows";
        _themeAdvertisementVideoEmptyState =
            GetThemeBinding<Label>($"{advertisementBase}.empty_state");
        for (int index = 0; index < AdvertisementSettingsValidator.MaximumVideoPaths; index++)
        {
            int slotIndex = index;
            string slotBase = $"{advertisementBase}.slot_{index:00}";
            var slot = new ThemeAdvertisementVideoSlot(
                GetThemeBinding<Control>(slotBase),
                GetThemeBinding<LineEdit>($"{slotBase}.path"),
                GetThemeBinding<Button>($"{slotBase}.remove_button"),
                GetThemeBinding<Label>($"{slotBase}.error"),
                value => OnThemeAdvertisementVideoChanged(slotIndex, value),
                () => RemoveAdvertisementVideo(slotIndex));
            slot.Subscribe();
            _themeAdvertisementVideoSlots.Add(slot);
        }
    }

    private void UnbindThemeCollectionSlots()
    {
        foreach (ThemeBonusSlot slot in _themeBonusSlots)
        {
            slot.Unsubscribe();
        }

        foreach (ThemePaymentPresetSlot slot in _themePaymentPresetSlots)
        {
            slot.Unsubscribe();
        }

        foreach (ThemeAdvertisementVideoSlot slot in _themeAdvertisementVideoSlots)
        {
            slot.Unsubscribe();
        }

        _themeBonusSlots.Clear();
        _themePaymentPresetSlots.Clear();
        _themeAdvertisementVideoSlots.Clear();
    }

    private void UpdateThemeBonusSlots()
    {
        _bonusRuleErrorLabels.Clear();
        BonusRule[] rules = _workingCopy?.Pricing.BonusRules ?? Array.Empty<BonusRule>();
        _themeBonusEmptyState!.Visible = rules.Length == 0;
        bool wasUpdating = _updatingControls;
        _updatingControls = true;
        try
        {
            for (int index = 0; index < _themeBonusSlots.Count; index++)
            {
                ThemeBonusSlot slot = _themeBonusSlots[index];
                bool active = index < rules.Length;
                slot.Root.Visible = active;
                if (!active)
                {
                    continue;
                }

                slot.Threshold.MaxValue = _workingCopy!.Pricing.MaxCardAmountRubles;
                slot.Threshold.Value = rules[index].MinimumAmountRubles;
                slot.Bonus.Suffix = string.Empty;
                slot.Bonus.Value = rules[index].BonusTokens;
                slot.Error.Text = string.Empty;
                _bonusRuleErrorLabels.Add(slot.Error);
            }
        }
        finally
        {
            _updatingControls = wasUpdating;
        }
    }

    private void UpdateThemePaymentPresetSlots()
    {
        _paymentPresetErrorLabels.Clear();
        PaymentPreset[] presets = _workingCopy?.Pricing.PaymentPresets ?? Array.Empty<PaymentPreset>();
        _themePaymentPresetEmptyState!.Visible = presets.Length == 0;
        _themePaymentPresetEmptyState.Text = _workingCopy?.Payments.CardEnabled != false
            ? "Готовые суммы пока не добавлены. Покупатель сможет ввести сумму сам."
            : "Карточная оплата выключена.";
        bool wasUpdating = _updatingControls;
        _updatingControls = true;
        try
        {
            for (int index = 0; index < _themePaymentPresetSlots.Count; index++)
            {
                ThemePaymentPresetSlot slot = _themePaymentPresetSlots[index];
                bool active = index < presets.Length;
                slot.Root.Visible = active;
                if (!active)
                {
                    continue;
                }

                slot.Amount.Value = presets[index].AmountRubles;
                slot.Error.Text = string.Empty;
                _paymentPresetErrorLabels.Add(slot.Error);
            }
        }
        finally
        {
            _updatingControls = wasUpdating;
        }
    }

    private void UpdateThemeAdvertisementVideoSlots()
    {
        _advertisementVideoErrorLabels.Clear();
        string[] paths = _workingCopy?.Advertisement.VideoPaths ?? Array.Empty<string>();
        _themeAdvertisementVideoEmptyState!.Visible = paths.Length == 0;
        bool wasUpdating = _updatingControls;
        _updatingControls = true;
        try
        {
            for (int index = 0; index < _themeAdvertisementVideoSlots.Count; index++)
            {
                ThemeAdvertisementVideoSlot slot = _themeAdvertisementVideoSlots[index];
                bool active = index < paths.Length;
                slot.Root.Visible = active;
                if (!active)
                {
                    continue;
                }

                slot.Path.Text = paths[index];
                slot.Error.Text = string.Empty;
                _advertisementVideoErrorLabels.Add(slot.Error);
            }
        }
        finally
        {
            _updatingControls = wasUpdating;
        }
    }

    private void OnThemeBonusThresholdChanged(int index, double value)
    {
        BonusRule[] rules = _workingCopy?.Pricing.BonusRules ?? Array.Empty<BonusRule>();
        if (_updatingControls || index >= rules.Length)
        {
            return;
        }

        rules[index].MinimumAmountRubles = (int)Math.Round(value);
        ValidateAllAndRefresh();
    }

    private void OnThemeBonusValueChanged(int index, double value)
    {
        BonusRule[] rules = _workingCopy?.Pricing.BonusRules ?? Array.Empty<BonusRule>();
        if (_updatingControls || index >= rules.Length)
        {
            return;
        }

        rules[index].BonusTokens = (int)Math.Round(value);
        ValidateAllAndRefresh();
    }

    private void RemoveThemeBonusRule(int index)
    {
        BonusRule[] rules = _workingCopy?.Pricing.BonusRules ?? Array.Empty<BonusRule>();
        if (index < rules.Length)
        {
            RemoveBonusRule(rules[index]);
        }
    }

    private void OnThemePaymentPresetChanged(int index, double value)
    {
        PaymentPreset[] presets = _workingCopy?.Pricing.PaymentPresets ?? Array.Empty<PaymentPreset>();
        if (_updatingControls || index >= presets.Length)
        {
            return;
        }

        presets[index].AmountRubles = (int)Math.Round(value);
        ValidateAllAndRefresh();
    }

    private void RemoveThemePaymentPreset(int index)
    {
        PaymentPreset[] presets = _workingCopy?.Pricing.PaymentPresets ?? Array.Empty<PaymentPreset>();
        if (index < presets.Length)
        {
            RemovePaymentPreset(presets[index]);
        }
    }

    private void OnThemeAdvertisementVideoChanged(int index, string value)
    {
        string[] paths = _workingCopy?.Advertisement.VideoPaths ?? Array.Empty<string>();
        if (_updatingControls || index >= paths.Length)
        {
            return;
        }

        paths[index] = value;
        ValidateAllAndRefresh();
    }

    private void RebuildAdvertisementVideoRows()
    {
        if (UsesSelfContainedThemeScene)
        {
            UpdateThemeAdvertisementVideoSlots();
            RefreshKeyboardInputBindings();
            return;
        }

        foreach (Node child in _advertisementVideoRows.GetChildren())
        {
            _advertisementVideoRows.RemoveChild(child);
            child.QueueFree();
        }

        _advertisementVideoErrorLabels.Clear();
        if (_workingCopy is null)
        {
            return;
        }

        string[] paths = _workingCopy.Advertisement.VideoPaths ?? Array.Empty<string>();
        if (paths.Length == 0)
        {
            _advertisementVideoRows.AddChild(new Label
            {
                Text = "Промо-ролики пока не добавлены. Будет показана картинка или текст вместо промо.",
                CustomMinimumSize = new Vector2(0f, 47f),
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                ThemeTypeVariation = ThemeSemanticTypes.PanelText,
            });
            RefreshKeyboardInputBindings();
            return;
        }

        for (int index = 0; index < paths.Length; index++)
        {
            int rowIndex = index;
            var wrapper = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            wrapper.AddThemeConstantOverride("separation", 4);
            var row = new HBoxContainer
            {
                CustomMinimumSize = new Vector2(0f, 70f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            row.AddThemeConstantOverride("separation", 16);
            var path = new LineEdit
            {
                Text = paths[index],
                PlaceholderText = "user://advertisement/video.ogv",
                MaxLength = AdvertisementSettingsValidator.MaximumAssetPathLength,
                CustomMinimumSize = new Vector2(0f, 67f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            path.AddThemeFontSizeOverride("font_size", 23);
            var removeButton = new Button
            {
                Text = "УДАЛИТЬ",
                CustomMinimumSize = new Vector2(127f, 67f),
                ThemeTypeVariation = ThemeSemanticTypes.DangerButton,
            };
            var error = new Label
            {
                CustomMinimumSize = new Vector2(0f, 28f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                ThemeTypeVariation = ThemeSemanticTypes.ErrorText,
            };
            error.AddThemeColorOverride("font_color", new Color(1.0f, 0.4f, 0.35f));
            error.AddThemeFontSizeOverride("font_size", 20);

            row.AddChild(path);
            row.AddChild(removeButton);
            wrapper.AddChild(row);
            wrapper.AddChild(error);
            _advertisementVideoRows.AddChild(wrapper);
            _advertisementVideoErrorLabels.Add(error);

            path.TextChanged += value =>
            {
                if (_workingCopy is null)
                {
                    return;
                }

                string[] currentPaths = _workingCopy.Advertisement.VideoPaths ?? Array.Empty<string>();
                if (rowIndex < currentPaths.Length)
                {
                    currentPaths[rowIndex] = value;
                    ValidateAllAndRefresh();
                }
            };
            removeButton.Pressed += () => RemoveAdvertisementVideo(rowIndex);
        }

        RefreshKeyboardInputBindings();
    }

    private void RemoveAdvertisementVideo(int index)
    {
        if (_workingCopy is null)
        {
            return;
        }

        string[] paths = _workingCopy.Advertisement.VideoPaths ?? Array.Empty<string>();
        if (index < 0 || index >= paths.Length)
        {
            return;
        }

        _workingCopy.Advertisement.VideoPaths = paths.Where((_, candidateIndex) => candidateIndex != index).ToArray();
        RebuildAdvertisementVideoRows();
        ValidateAllAndRefresh();
    }

    private void OnAddBonusRulePressed()
    {
        if (_workingCopy is null)
        {
            return;
        }

        BonusRule[] rules = _workingCopy.Pricing.BonusRules ?? Array.Empty<BonusRule>();
        if (rules.Length >= MaximumEditableBonusRules)
        {
            _bonusError.Text = $"Можно добавить не больше {MaximumEditableBonusRules} бонусов.";
            return;
        }

        int price = Math.Max(PricingSettingsValidator.MinimumTokenPriceRubles, _workingCopy.Pricing.TokenPriceRubles);
        int lastThreshold = rules.Select(rule => rule?.MinimumAmountRubles ?? 0).DefaultIfEmpty(0).Max();
        int suggestedThreshold = rules.Length == 0
            ? Math.Max(price, 100)
            : lastThreshold + price;
        int nextThreshold = Math.Clamp(
            suggestedThreshold,
            price,
            Math.Max(price, _workingCopy.Pricing.MaxCardAmountRubles));
        int nextBonus = rules.Select(rule => rule?.BonusTokens ?? 0).DefaultIfEmpty(0).Max() + 1;

        _workingCopy.Pricing.BonusRules =
        [
            .. rules.Where(rule => rule is not null),
            new BonusRule
            {
                MinimumAmountRubles = nextThreshold,
                BonusTokens = Math.Min(nextBonus, PricingSettingsValidator.MaximumBonusTokens),
            },
        ];
        SortBonusRules();
        RebuildBonusRows();
        ValidateAllAndRefresh();
    }

    private void RebuildBonusRows()
    {
        if (UsesSelfContainedThemeScene)
        {
            UpdateThemeBonusSlots();
            RefreshKeyboardInputBindings();
            return;
        }

        foreach (Node child in _bonusRows.GetChildren())
        {
            _bonusRows.RemoveChild(child);
            child.QueueFree();
        }

        _bonusRuleErrorLabels.Clear();
        if (_workingCopy is null)
        {
            return;
        }

        BonusRule[] rules = _workingCopy.Pricing.BonusRules ?? Array.Empty<BonusRule>();
        if (rules.Length == 0)
        {
            _bonusRows.AddChild(new Label
            {
                Text = "Бонусы пока не настроены.",
                CustomMinimumSize = new Vector2(0f, 47f),
                VerticalAlignment = VerticalAlignment.Center,
                ThemeTypeVariation = ThemeSemanticTypes.PanelText,
            });
            RefreshKeyboardInputBindings();
            return;
        }

        foreach (BonusRule rule in rules)
        {
            var wrapper = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            wrapper.AddThemeConstantOverride("separation", 4);

            var row = new HBoxContainer
            {
                CustomMinimumSize = new Vector2(0f, 70f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            row.AddThemeConstantOverride("separation", 16);

            var threshold = new SpinBox
            {
                MinValue = 0,
                MaxValue = _workingCopy.Pricing.MaxCardAmountRubles,
                Step = 1,
                Value = rule.MinimumAmountRubles,
                Suffix = " ₽",
                UpdateOnTextChanged = true,
                CustomMinimumSize = new Vector2(200f, 67f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            var arrow = new Label
            {
                Text = "→",
                CustomMinimumSize = new Vector2(32f, 67f),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ThemeTypeVariation = ThemeSemanticTypes.PanelText,
            };
            arrow.AddThemeFontSizeOverride("font_size", 28);

            var bonus = new SpinBox
            {
                MinValue = 0,
                MaxValue = PricingSettingsValidator.MaximumBonusTokens,
                Step = 1,
                Value = rule.BonusTokens,
                UpdateOnTextChanged = true,
                CustomMinimumSize = new Vector2(160f, 67f),
            };
            var removeButton = new Button
            {
                Text = "УДАЛИТЬ",
                CustomMinimumSize = new Vector2(103f, 67f),
                ThemeTypeVariation = ThemeSemanticTypes.DangerButton,
            };
            var error = new Label
            {
                CustomMinimumSize = new Vector2(0f, 25f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                ThemeTypeVariation = ThemeSemanticTypes.ErrorText,
            };
            error.AddThemeColorOverride("font_color", new Color(1.0f, 0.4f, 0.35f));
            error.AddThemeFontSizeOverride("font_size", 20);

            row.AddChild(threshold);
            row.AddChild(arrow);
            row.AddChild(bonus);
            row.AddChild(removeButton);
            wrapper.AddChild(row);
            wrapper.AddChild(error);
            _bonusRows.AddChild(wrapper);
            _bonusRuleErrorLabels.Add(error);

            threshold.ValueChanged += value =>
            {
                rule.MinimumAmountRubles = (int)Math.Round(value);
                ValidateAllAndRefresh();
            };
            bonus.ValueChanged += value =>
            {
                rule.BonusTokens = (int)Math.Round(value);
                ValidateAllAndRefresh();
            };
            removeButton.Pressed += () => RemoveBonusRule(rule);
        }

        RefreshKeyboardInputBindings();
    }

    private void RemoveBonusRule(BonusRule rule)
    {
        if (_workingCopy is null)
        {
            return;
        }

        _workingCopy.Pricing.BonusRules = (_workingCopy.Pricing.BonusRules ?? Array.Empty<BonusRule>())
            .Where(candidate => candidate is not null && !ReferenceEquals(candidate, rule))
            .ToArray();
        RebuildBonusRows();
        ValidateAllAndRefresh();
    }

    private void SortBonusRules()
    {
        if (_workingCopy is null)
        {
            return;
        }

        _workingCopy.Pricing.BonusRules = (_workingCopy.Pricing.BonusRules ?? Array.Empty<BonusRule>())
            .Where(rule => rule is not null)
            .OrderBy(rule => rule.MinimumAmountRubles)
            .ThenBy(rule => rule.BonusTokens)
            .ToArray();
    }

    private void RebuildPaymentPresetRows()
    {
        if (UsesSelfContainedThemeScene)
        {
            UpdateThemePaymentPresetSlots();
            RefreshKeyboardInputBindings();
            return;
        }

        foreach (Node child in _paymentPresetRows.GetChildren())
        {
            _paymentPresetRows.RemoveChild(child);
            child.QueueFree();
        }

        _paymentPresetErrorLabels.Clear();
        if (_workingCopy is null)
        {
            return;
        }

        PaymentPreset[] presets = _workingCopy.Pricing.PaymentPresets ?? Array.Empty<PaymentPreset>();
        if (presets.Length == 0)
        {
            _paymentPresetRows.AddChild(new Label
            {
                Text = _workingCopy.Payments.CardEnabled
                    ? "Готовые суммы пока не добавлены. Покупатель сможет ввести сумму сам."
                    : "Карточная оплата выключена.",
                CustomMinimumSize = new Vector2(0f, 47f),
                VerticalAlignment = VerticalAlignment.Center,
                ThemeTypeVariation = ThemeSemanticTypes.PanelText,
            });
            RefreshKeyboardInputBindings();
            return;
        }

        foreach (PaymentPreset preset in presets)
        {
            var wrapper = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            wrapper.AddThemeConstantOverride("separation", 4);
            var row = new HBoxContainer
            {
                CustomMinimumSize = new Vector2(0f, 70f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            row.AddThemeConstantOverride("separation", 16);
            var amount = new SpinBox
            {
                MinValue = 0,
                MaxValue = PricingSettingsValidator.MaximumCardAmountRubles,
                Step = 1,
                Value = preset.AmountRubles,
                Suffix = " ₽",
                UpdateOnTextChanged = true,
                CustomMinimumSize = new Vector2(0f, 67f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            var removeButton = new Button
            {
                Text = "УДАЛИТЬ",
                CustomMinimumSize = new Vector2(127f, 67f),
                ThemeTypeVariation = ThemeSemanticTypes.DangerButton,
            };
            var error = new Label
            {
                CustomMinimumSize = new Vector2(0f, 25f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                ThemeTypeVariation = ThemeSemanticTypes.ErrorText,
            };
            error.AddThemeColorOverride("font_color", new Color(1.0f, 0.4f, 0.35f));
            error.AddThemeFontSizeOverride("font_size", 20);

            row.AddChild(amount);
            row.AddChild(removeButton);
            wrapper.AddChild(row);
            wrapper.AddChild(error);
            _paymentPresetRows.AddChild(wrapper);
            _paymentPresetErrorLabels.Add(error);

            amount.ValueChanged += value =>
            {
                preset.AmountRubles = (int)Math.Round(value);
                ValidateAllAndRefresh();
            };
            removeButton.Pressed += () => RemovePaymentPreset(preset);
        }

        RefreshKeyboardInputBindings();
    }

    private void RemovePaymentPreset(PaymentPreset preset)
    {
        if (_workingCopy is null)
        {
            return;
        }

        _workingCopy.Pricing.PaymentPresets = (_workingCopy.Pricing.PaymentPresets ?? Array.Empty<PaymentPreset>())
            .Where(candidate => candidate is not null && !ReferenceEquals(candidate, preset))
            .ToArray();
        RebuildPaymentPresetRows();
        ValidateAllAndRefresh();
    }

    private void SortPaymentPresets()
    {
        if (_workingCopy is null)
        {
            return;
        }

        _workingCopy.Pricing.PaymentPresets = (_workingCopy.Pricing.PaymentPresets ?? Array.Empty<PaymentPreset>())
            .Where(preset => preset is not null)
            .OrderBy(preset => preset.AmountRubles)
            .ToArray();
    }

    private PricingValidationResult ValidatePricingAndRefreshPreview()
    {
        if (_workingCopy is null)
        {
            return new PricingValidationResult(Array.Empty<PricingValidationIssue>());
        }

        PricingValidationResult validation = PricingSettingsValidator.Validate(_workingCopy.Pricing);
        _tokenPriceError.Text = string.Join(
            "\n",
            validation.Issues
                .Where(issue => issue.Field == PricingValidationField.TokenPrice)
                .Select(issue => issue.Message));

        for (int index = 0; index < _bonusRuleErrorLabels.Count; index++)
        {
            _bonusRuleErrorLabels[index].Text = string.Join(
                " ",
                validation.Issues
                    .Where(issue => issue.Field == PricingValidationField.BonusRule && issue.RuleIndex == index)
                    .Select(issue => issue.Message));
        }

        bool hasBonusErrors = validation.Issues.Any(issue => issue.Field == PricingValidationField.BonusRule);
        bool hasTokenPriceErrors = validation.Issues.Any(issue => issue.Field == PricingValidationField.TokenPrice);
        bool hasCardLimitErrors = validation.Issues.Any(issue =>
            issue.Field is PricingValidationField.MaximumCardAmount or PricingValidationField.CustomAmountStep);
        _bonusError.Text = hasBonusErrors ? "Исправьте ошибки в бонусах." : string.Empty;
        _cardLimitsError.Text = string.Join(
            "\n",
            validation.Issues
                .Where(issue => issue.Field is PricingValidationField.MaximumCardAmount or PricingValidationField.CustomAmountStep)
                .Select(issue => issue.Message));
        for (int index = 0; index < _paymentPresetErrorLabels.Count; index++)
        {
            _paymentPresetErrorLabels[index].Text = string.Join(
                " ",
                validation.Issues
                    .Where(issue => issue.Field == PricingValidationField.PaymentPreset && issue.PresetIndex == index)
                    .Select(issue => issue.Message));
        }

        _addBonusRuleButton.Disabled = hasTokenPriceErrors
                                       || (_workingCopy.Pricing.BonusRules?.Length ?? 0) >= MaximumEditableBonusRules;
        _addPaymentPresetButton.Disabled = hasTokenPriceErrors
                                           || hasCardLimitErrors
                                           || (_workingCopy.Pricing.PaymentPresets?.Length ?? 0) >= MaximumEditablePaymentPresets;
        RefreshPricingPreview(validation.IsValid);
        return validation;
    }

    private bool ValidateAllAndRefresh(bool scheduleAutoSave = true)
    {
        if (_workingCopy is null)
        {
            return false;
        }

        PricingValidationResult pricing = ValidatePricingAndRefreshPreview();
        PaymentValidationResult payment = PaymentSettingsValidator.Validate(_workingCopy);
        BrandingValidationResult branding = BrandingSettingsValidator.Validate(_workingCopy.Branding);
        AdvertisementValidationResult advertisement = AdvertisementSettingsValidator.Validate(
            _workingCopy.Advertisement,
            AdvertisementAssetExists);
        ThemeValidationResult theme = ThemeSettingsValidator.Validate(_workingCopy.Themes);
        HardwareValidationResult hardware = HardwareSettingsValidator.Validate(_workingCopy.Hardware);
        LoggingValidationResult logging = LoggingSettingsValidator.Validate(_workingCopy.Logging);
        _applicationNameError.Text = string.Empty;
        _shortTextError.Text = JoinBrandingIssues(branding, BrandingValidationField.ShortText);
        _supportPhoneError.Text = JoinBrandingIssues(branding, BrandingValidationField.SupportPhone);
        _advertisementFallbackError.Text = JoinAdvertisementIssues(
            advertisement,
            AdvertisementValidationField.FallbackText);
        _advertisementPosterError.Text = JoinAdvertisementIssues(
            advertisement,
            AdvertisementValidationField.PosterPath);
        _externalDlcPathError.Text = string.Join(
            "\n",
            theme.Issues
                .Where(issue => issue.Field == ThemeValidationField.ExternalDlcPath)
                .Select(issue => issue.Message));
        _portError.Text = CanEditEngineering
            ? string.Join(
                "\n",
                hardware.Issues
                    .Where(issue => issue.Field == HardwareValidationField.PortName)
                    .Select(issue => issue.Message))
            : string.Empty;
        _connectionError.Text = CanEditEngineering
            ? string.Join(
                "\n",
                hardware.Issues
                    .Where(issue => issue.Field is HardwareValidationField.BaudRate
                        or HardwareValidationField.ReconnectIntervalSeconds)
                    .Select(issue => issue.Message))
            : string.Empty;
        _loggingError.Text = string.Join("\n", logging.Issues.Select(issue => issue.Message));
        for (int index = 0; index < _advertisementVideoErrorLabels.Count; index++)
        {
            _advertisementVideoErrorLabels[index].Text = string.Join(
                " ",
                advertisement.Issues
                    .Where(issue => issue.Field == AdvertisementValidationField.VideoPath
                                    && issue.VideoIndex == index)
                    .Select(issue => issue.Message));
        }

        bool hasVideoErrors = advertisement.Issues.Any(issue => issue.Field == AdvertisementValidationField.VideoPath);
        string playlistIssues = JoinAdvertisementIssues(advertisement, AdvertisementValidationField.Playlist);
        _advertisementPlaylistError.Text = string.Join(
            "\n",
            new[]
            {
                hasVideoErrors ? "Проверьте пути к промо-роликам." : string.Empty,
                playlistIssues,
            }.Where(message => message.Length > 0));
        _addAdvertisementVideoButton.Disabled =
            (_workingCopy.Advertisement.VideoPaths?.Length ?? 0) >= AdvertisementSettingsValidator.MaximumVideoPaths;
        RefreshAdvertisementSummary();
        RefreshAppearanceSummary(theme.IsValid);
        _paymentMethodsError.Text = string.Join(
            "\n",
            payment.Issues
                .Where(issue => issue.Field == PaymentValidationField.PaymentMethods)
                .Select(issue => issue.Message));
        _timeoutsError.Text = string.Join(
            "\n",
            payment.Issues
                .Where(issue => issue.Field is PaymentValidationField.SessionIdleTimeout
                    or PaymentValidationField.CashPaymentTimeout
                    or PaymentValidationField.CashlessPaymentTimeout
                    or PaymentValidationField.SuccessTimeout)
                .Select(issue => issue.Message));

        bool hasPresetRowErrors = pricing.Issues.Any(issue => issue.Field == PricingValidationField.PaymentPreset);
        string presetSummary = hasPresetRowErrors ? "Исправьте ошибки в готовых суммах." : string.Empty;
        _paymentPresetsError.Text = presetSummary;

        bool ownerBrandingValid = !branding.Issues.Any(issue =>
            issue.Field is BrandingValidationField.ShortText or BrandingValidationField.SupportPhone);
        bool valid = pricing.IsValid
                      && payment.IsValid
                      && ownerBrandingValid
                      && advertisement.IsValid;
        if (valid && scheduleAutoSave)
        {
            ScheduleAutoSave();
        }

        return valid;
    }

    private void RefreshBrandingPreview()
    {
        if (_workingCopy is null)
        {
            return;
        }

        _applicationNamePreview.Text = PreviewText(_workingCopy.Branding.ApplicationName);
        _shortTextPreview.Text = PreviewText(_workingCopy.Branding.ShortText);
        _supportPhonePreview.Text = $"ТЕХПОДДЕРЖКА • {PreviewText(_workingCopy.Branding.SupportPhone)}";
    }

    private void RefreshServicePinStatus()
    {
        if (_workingCopy is null)
        {
            return;
        }

        bool configured = new ServiceAccessPolicy(_workingCopy.Security).IsAvailable;
        _resetServicePinButton.Disabled = !_workingCopy.Security.ServicePinEnabled || !configured;
        _servicePinStatus.Text = !_workingCopy.Security.ServicePinEnabled
            ? "Защита выключена. Настройки открываются без PIN-кода."
            : configured
                ? "PIN-код настроен. Нажмите «Придумать новый PIN», если хотите его изменить."
                : "Защита включена. При следующем входе приложение попросит придумать PIN-код.";
    }

    private void RefreshDiagnostics()
    {
        _runtimeSummary.Text = _runtimeSummaryProvider?.Invoke()
            ?? "Сведения о runtime недоступны.";
        IReadOnlyList<SafeDiagnosticEntry> entries = _recentErrorsProvider?.Invoke()
            ?? Array.Empty<SafeDiagnosticEntry>();
        _recentErrors.Text = entries.Count == 0
            ? "Нет безопасных предупреждений в текущем запуске."
            : string.Join(
                "\n",
                entries.Select(entry =>
                    $"{entry.Timestamp:HH:mm:ss} • {entry.Level} • {entry.Category}: {entry.Message}"));
    }

    private static void SelectOptionById(OptionButton option, int id)
    {
        for (int index = 0; index < option.ItemCount; index++)
        {
            if (option.GetItemId(index) == id)
            {
                option.Select(index);
                return;
            }
        }

        option.AddItem(id.ToString(CultureInfo.InvariantCulture), id);
        option.Select(option.ItemCount - 1);
    }

    private static void SelectOptionByText(OptionButton option, string text)
    {
        for (int index = 0; index < option.ItemCount; index++)
        {
            if (option.GetItemText(index).Equals(text, StringComparison.OrdinalIgnoreCase))
            {
                option.Select(index);
                return;
            }
        }

        option.Select(0);
    }

    private static string JoinBrandingIssues(BrandingValidationResult validation, BrandingValidationField field)
    {
        return string.Join(
            "\n",
            validation.Issues.Where(issue => issue.Field == field).Select(issue => issue.Message));
    }

    private static string PreviewText(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private void RefreshAdvertisementSummary()
    {
        if (_workingCopy is null)
        {
            return;
        }

        AdvertisementSettings settings = _workingCopy.Advertisement;
        if (!settings.Enabled)
        {
            _advertisementSummary.Text = "Промо выключено. Добавленные промо-ролики сохранятся.";
            return;
        }

        int videoCount = settings.VideoPaths?.Length ?? 0;
        string poster = string.IsNullOrWhiteSpace(settings.PosterPath) ? "без постера" : "постер задан";
        string sound = settings.Muted ? "без звука" : "со звуком";
        _advertisementSummary.Text =
            $"Промо включено • {poster} • промо-роликов: {videoCount} • {sound}\nТекст вместо промо: {PreviewText(settings.FallbackText)}";
    }

    private static string JoinAdvertisementIssues(
        AdvertisementValidationResult validation,
        AdvertisementValidationField field)
    {
        return string.Join(
            "\n",
            validation.Issues.Where(issue => issue.Field == field).Select(issue => issue.Message));
    }

    private static bool AdvertisementAssetExists(string path, AdvertisementAssetType assetType)
    {
        string expectedType = assetType == AdvertisementAssetType.Poster ? "Texture2D" : "VideoStream";
        if (ResourceLoader.Exists(path, expectedType))
        {
            return true;
        }

        bool godotPath = path.StartsWith("res://", StringComparison.OrdinalIgnoreCase)
                         || path.StartsWith("user://", StringComparison.OrdinalIgnoreCase);
        return godotPath ? Godot.FileAccess.FileExists(path) : System.IO.File.Exists(path);
    }

    private void RefreshAppearanceSummary(bool pathIsValid)
    {
        if (_workingCopy is null)
        {
            return;
        }

        ThemeSettings settings = _workingCopy.Themes;
        if (!settings.ExternalDlcEnabled)
        {
            _externalDlcPackageStatus.Text = "Внешний DLC отключён; после перезапуска будет использована встроенная тема.";
        }
        else if (!pathIsValid)
        {
            _externalDlcPackageStatus.Text = "Исправьте путь к пакету до сохранения.";
        }
        else if (ThemePackageExists(settings.ExternalDlcPath))
        {
            _externalDlcPackageStatus.Text = "Пакет найден. Manifest и ресурсы будут проверены при следующем запуске.";
        }
        else
        {
            _externalDlcPackageStatus.Text =
                "Пакет пока не найден. Сохранение разрешено; при запуске останется встроенная тема.";
        }

        _currentThemeStatus.Text = _themeManager.IsExternalDlcActive && _themeManager.CurrentTheme is not null
            ? $"Сейчас активна внешняя тема: {_themeManager.CurrentTheme.DisplayName}."
            : "Сейчас активна встроенная тема по умолчанию.";
    }

    private static bool ThemePackageExists(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return false;
        }

        string path = configuredPath.Trim();
        if (path.StartsWith("user://", StringComparison.OrdinalIgnoreCase))
        {
            return System.IO.File.Exists(ProjectSettings.GlobalizePath(path));
        }

        return Path.IsPathFullyQualified(path) && System.IO.File.Exists(path);
    }

    private void RefreshPricingPreview(bool pricingIsValid)
    {
        if (_workingCopy is null)
        {
            return;
        }

        if (!pricingIsValid)
        {
            _pricingPreview.Text = "Исправьте ошибки выше, чтобы увидеть пример расчёта.";
            return;
        }

        PricingSettings pricing = _workingCopy.Pricing;
        int[] amounts = (pricing.PaymentPresets ?? Array.Empty<PaymentPreset>())
            .Select(preset => preset?.AmountRubles ?? 0)
            .Append(pricing.TokenPriceRubles)
            .Where(amount => amount >= pricing.TokenPriceRubles)
            .Distinct()
            .OrderBy(amount => amount)
            .Take(6)
            .ToArray();
        if (amounts.Length == 0)
        {
            amounts = [pricing.TokenPriceRubles];
        }

        _pricingPreview.Text = string.Join(
            "\n",
            amounts.Select(amount =>
            {
                TokenCalculation result = PricingPolicy.Calculate(pricing, amount);
                string bonus = result.BonusTokens > 0
                    ? $" (в том числе {FormatTokenCount(result.BonusTokens)} в подарок)"
                    : string.Empty;
                return $"{amount} ₽ — {FormatTokenCount(result.TotalTokens)}{bonus}";
            }));
    }

    private void PreviewAudio()
    {
        if (_workingCopy is not null)
        {
            AudioSettingsApplier.Apply(_workingCopy.Audio);
        }
    }

    private void OnTestSoundPressed()
    {
        if (_workingCopy is null)
        {
            return;
        }

        PreviewAudio();
        if (_workingCopy.Audio.Muted || _workingCopy.Audio.MasterVolumePercent <= 0)
        {
            _statusLabel.Text = "Звук отключён. Включите его, чтобы проверить громкость.";
            return;
        }

        _testTonePlayer.Stop();
        _testTonePlayer.Play();
        if (_testTonePlayer.GetStreamPlayback() is not AudioStreamGeneratorPlayback playback)
        {
            _statusLabel.Text = "Не удалось воспроизвести звук.";
            return;
        }

        int frameCount = (int)(TestToneMixRate * TestToneDurationSeconds);
        for (int frame = 0; frame < frameCount; frame++)
        {
            float position = frame / (float)frameCount;
            float envelope = MathF.Min(1.0f, MathF.Min(position, 1.0f - position) * 16.0f);
            float sample = MathF.Sin(MathF.Tau * TestToneFrequency * frame / TestToneMixRate)
                * 0.2f
                * envelope;
            playback.PushFrame(new Vector2(sample, sample));
        }

        _statusLabel.Text = "Звук воспроизведён.";
    }

    public bool TryFinishEditing()
    {
        if (_workingCopy is null)
        {
            _onScreenKeyboard?.Hide();
            return true;
        }

        if (_serviceTestInProgress)
        {
            _testDispenseStatus.Text = "Дождитесь завершения тестовой выдачи перед выходом.";
            return false;
        }

        if (_tokenRecountInProgress)
        {
            _serviceInventoryStatus.Text = "Дождитесь завершения пересчёта жетонов перед выходом.";
            return false;
        }

        if (_serviceDialogMode != ServiceDialogMode.None)
        {
            CloseServiceDialog();
            return false;
        }

        _onScreenKeyboard?.Hide();
        _autoSaveTimer.Stop();
        if (!TryAutoSave())
        {
            return false;
        }

        _autoSaveEnabled = false;
        _testTonePlayer.Stop();
        _workingCopy = null;
        return true;
    }

    private void OnSaveAndExitPressed()
    {
        if (_onScreenKeyboard?.IsVisible ?? false)
        {
            return;
        }

        if (TryFinishEditing())
        {
            SaveAndExitRequested?.Invoke();
        }
    }

    private void ScheduleAutoSave()
    {
        if (!_autoSaveEnabled
            || _autoSaveInProgress
            || _updatingControls
            || _serviceTestInProgress
            || _tokenRecountInProgress
            || _workingCopy is null)
        {
            return;
        }

        _autoSaveTimer.Start(AutoSaveDelaySeconds);
    }

    private void OnAutoSaveTimeout() => TryAutoSave();

    private bool TryAutoSave()
    {
        if (_settingsService is null || _workingCopy is null)
        {
            return true;
        }

        _autoSaveInProgress = true;
        try
        {
            SortBonusRules();
            SortPaymentPresets();
            if (!ValidateAllAndRefresh(scheduleAutoSave: false))
            {
                _statusLabel.Text = "Исправьте отмеченные поля — после этого настройки сохранятся сами.";
                return false;
            }

            AppSettings previous = _settingsService.Current;
            SettingsChangeSummary summary = SettingsChangeDetector.Compare(previous, _workingCopy);
            if (!summary.HasChanges)
            {
                return true;
            }

            AppSettings saved = _settingsService.Save(_workingCopy);
            AudioSettingsApplier.Apply(saved.Audio);
            _themeManager.SetReducedEffects(saved.Themes.ReducedEffects);
            _statusLabel.Text = summary.RestartRequired
                ? $"Настройки сохранены. После перезапуска изменятся: {string.Join(", ", summary.RestartRequiredSections)}."
                : "Настройки сохранены.";
            AppLogger.Info("Settings", "Настройки приложения сохранены автоматически.");
            Saved?.Invoke(saved, summary, _accessLevel);
            return true;
        }
        catch (Exception exception)
        {
            AppLogger.Error("Settings", $"Не удалось автоматически сохранить настройки: {exception.GetType().Name}.");
            _statusLabel.Text = "Не удалось сохранить настройки. Попробуйте ещё раз.";
            _bonusError.Text = "Не удалось сохранить настройки. Попробуйте ещё раз.";
            return false;
        }
        finally
        {
            _autoSaveInProgress = false;
        }
    }

    private void OnThemeChanged(VendingThemeDefinition definition)
    {
        _themeManager.ApplyTo(this);
        RefreshInteractivePetToggle();
    }

    private void ApplyFallbackThemeSemantics()
    {
        Control header = GetNode<Control>("SafeMargin/Scroll/Content/Header");
        GetNode<Label>("SafeMargin/Scroll/Content/Header/Margin/Content/Title").ThemeTypeVariation =
            ThemeSemanticTypes.ScreenTitle;

        foreach (Node child in GetNode("SafeMargin/Scroll/Content/SectionTabs").GetChildren())
        {
            if (child is Button button)
            {
                button.ThemeTypeVariation = ThemeSemanticTypes.SectionTabButton;
            }
        }

        Node content = GetNode("SafeMargin/Scroll/Content");
        foreach (Node child in content.GetChildren())
        {
            if (child is not PanelContainer panel || panel == header)
            {
                continue;
            }

            panel.ThemeTypeVariation = ThemeSemanticTypes.WhitePanel;
            ApplyPanelSemantics(panel);
        }
    }

    private static void ApplyPanelSemantics(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            switch (child)
            {
                case Label label:
                    string labelName = label.Name.ToString();
                    label.ThemeTypeVariation = labelName == "Title"
                        ? ThemeSemanticTypes.PanelTitle
                        : labelName.EndsWith("Error", StringComparison.Ordinal)
                            ? ThemeSemanticTypes.ErrorText
                            : labelName.Contains("Status", StringComparison.Ordinal)
                                ? ThemeSemanticTypes.StatusText
                                : labelName is "Description" or "Hint"
                                    ? ThemeSemanticTypes.SupportText
                                    : ThemeSemanticTypes.FieldLabel;
                    break;
                case CheckButton:
                case OptionButton:
                    break;
                case Button button:
                    string buttonName = button.Name.ToString();
                    button.ThemeTypeVariation = buttonName.Contains("Remove", StringComparison.Ordinal)
                        || buttonName.Contains("Delete", StringComparison.Ordinal)
                        || buttonName.Contains("Discard", StringComparison.Ordinal)
                            ? ThemeSemanticTypes.DangerButton
                            : buttonName.Contains("Cancel", StringComparison.Ordinal)
                                || buttonName.Contains("Reset", StringComparison.Ordinal)
                                || buttonName.Contains("Close", StringComparison.Ordinal)
                                ? ThemeSemanticTypes.SecondaryButton
                                : ThemeSemanticTypes.CompactButton;
                    break;
            }

            ApplyPanelSemantics(child);
        }
    }

    private static string FormatPercent(int value) => $"{value}%";

    private static int NormalizeHopperNumber(int hopperNumber) => hopperNumber == 2 ? 2 : 1;

    private static string FormatTokenCount(int count)
    {
        int absolute = Math.Abs(count);
        int lastTwoDigits = absolute % 100;
        int lastDigit = absolute % 10;
        string word = lastTwoDigits is >= 11 and <= 14
            ? "жетонов"
            : lastDigit switch
            {
                1 => "жетон",
                2 or 3 or 4 => "жетона",
                _ => "жетонов",
            };
        return $"{count} {word}";
    }

    private sealed class ThemeBonusSlot(
        Control root,
        SpinBox threshold,
        SpinBox bonus,
        Button removeButton,
        Label error,
        Godot.Range.ValueChangedEventHandler thresholdChanged,
        Godot.Range.ValueChangedEventHandler bonusChanged,
        Action removePressed)
    {
        public Control Root { get; } = root;
        public SpinBox Threshold { get; } = threshold;
        public SpinBox Bonus { get; } = bonus;
        public Button RemoveButton { get; } = removeButton;
        public Label Error { get; } = error;

        public void Subscribe()
        {
            Threshold.ValueChanged += thresholdChanged;
            Bonus.ValueChanged += bonusChanged;
            RemoveButton.Pressed += removePressed;
        }

        public void Unsubscribe()
        {
            if (!GodotObject.IsInstanceValid(Root))
            {
                return;
            }

            Threshold.ValueChanged -= thresholdChanged;
            Bonus.ValueChanged -= bonusChanged;
            RemoveButton.Pressed -= removePressed;
        }
    }

    private sealed class ThemePaymentPresetSlot(
        Control root,
        SpinBox amount,
        Button removeButton,
        Label error,
        Godot.Range.ValueChangedEventHandler amountChanged,
        Action removePressed)
    {
        public Control Root { get; } = root;
        public SpinBox Amount { get; } = amount;
        public Button RemoveButton { get; } = removeButton;
        public Label Error { get; } = error;

        public void Subscribe()
        {
            Amount.ValueChanged += amountChanged;
            RemoveButton.Pressed += removePressed;
        }

        public void Unsubscribe()
        {
            if (!GodotObject.IsInstanceValid(Root))
            {
                return;
            }

            Amount.ValueChanged -= amountChanged;
            RemoveButton.Pressed -= removePressed;
        }
    }

    private sealed class ThemeAdvertisementVideoSlot(
        Control root,
        LineEdit path,
        Button removeButton,
        Label error,
        LineEdit.TextChangedEventHandler pathChanged,
        Action removePressed)
    {
        public Control Root { get; } = root;
        public LineEdit Path { get; } = path;
        public Button RemoveButton { get; } = removeButton;
        public Label Error { get; } = error;

        public void Subscribe()
        {
            Path.TextChanged += pathChanged;
            RemoveButton.Pressed += removePressed;
        }

        public void Unsubscribe()
        {
            if (!GodotObject.IsInstanceValid(Root))
            {
                return;
            }

            Path.TextChanged -= pathChanged;
            RemoveButton.Pressed -= removePressed;
        }
    }

    private enum SettingsSection
    {
        General,
        Audio,
        Advertisement,
        Appearance,
        Pricing,
        Payment,
        Service,
        Equipment,
        Diagnostics,
    }

    private enum ServiceDialogMode
    {
        None,
        ConfirmRecountStart,
        ManualEntry,
        ManualConfirm,
    }
}
