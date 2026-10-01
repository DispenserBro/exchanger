using Exchanger.Core.Audio;
using Exchanger.Core.Configuration;
using Exchanger.Core.Inventory;
using Exchanger.Core.Logging;
using Exchanger.Core.Navigation;
using Exchanger.Core.Session;
using Exchanger.Core.Theming;
using Exchanger.Core.TechnicalConfiguration;
using Exchanger.Hardware.Abstractions;
using Exchanger.Hardware.MockHardware;
using Exchanger.Hardware.MockMachine;
using Exchanger.Hardware.PeripheryController;
using Exchanger.Hardware.SerialPortHardware;
using Godot;
using System;
using System.Collections.Generic;
using CardAmountScreenControl = Exchanger.Scenes.CardAmountScreen.CardAmountScreen;
using CardCustomAmountScreenControl = Exchanger.Scenes.CardCustomAmountScreen.CardCustomAmountScreen;
using CardTerminalScreenControl = Exchanger.Scenes.CardTerminalScreen.CardTerminalScreen;
using CashPaymentScreenControl = Exchanger.Scenes.CashPaymentScreen.CashPaymentScreen;
using ErrorScreenControl = Exchanger.Scenes.ErrorScreen.ErrorScreen;
using HomeScreenControl = Exchanger.Scenes.HomeScreen.HomeScreen;
using SettingsScreenControl = Exchanger.Scenes.SettingsScreen.SettingsScreen;
using ServiceAccessScreenControl = Exchanger.Scenes.ServiceAccessScreen.ServiceAccessScreen;
using SuccessScreenControl = Exchanger.Scenes.SuccessScreen.SuccessScreen;
using InteractivePetHostControl = Exchanger.UI.InteractivePet.InteractivePetHost;
using KioskFooterControl = Exchanger.UI.KioskFooter.KioskFooter;

namespace Exchanger.Scenes.App;

/// <summary>
/// Композиционный корень приложения: конфигурация, экраны, сессия и
/// выбранные реализации аппаратных границ.
/// </summary>
public partial class App : Node
{
    private SettingsService _settingsService = null!;
    private AppSettings _settings = null!;
    private TokenInventoryStore _tokenInventoryStore = null!;
    private TokenInventorySnapshot _tokenInventory = null!;
    private IHardwarePort? _hardwarePort;
    private IMachineController _machineController = null!;
    private ITokenRecountController? _tokenRecountController;
    private IDualHopperMachineController? _dualHopperController;
    private IDualHopperTokenRecountController? _dualHopperRecountController;
    private MockMachineController? _mockMachine;
    private PeripheryMachineController? _peripheryController;
    private IDisposable? _controllerCipher;
    private SessionController _sessionController = null!;
    private ScreenManager _screenManager = null!;
    private HomeScreenControl _homeScreen = null!;
    private CashPaymentScreenControl _cashPaymentScreen = null!;
    private CardAmountScreenControl _cardAmountScreen = null!;
    private CardCustomAmountScreenControl _cardCustomAmountScreen = null!;
    private CardTerminalScreenControl _cardTerminalScreen = null!;
    private SuccessScreenControl _successScreen = null!;
    private ErrorScreenControl _errorScreen = null!;
    private SettingsScreenControl _settingsScreen = null!;
    private ServiceAccessScreenControl _serviceAccessScreen = null!;
    private InteractivePetHostControl _interactivePetHost = null!;
    private ApplicationAudio _applicationAudio = null!;
    private ServiceAccessPolicy _serviceAccessPolicy = null!;
    private bool _serviceDispenseInProgress;
    private (bool Hopper1Enabled, bool Hopper2Enabled) _configuredHoppers;
    private bool _shutdownCompleted;
    private HardwareReconnectScheduler _hardwareReconnectScheduler = null!;
    private IReadOnlyList<string> _automaticPortCandidates = Array.Empty<string>();
    private int _nextAutomaticPortCandidateIndex;
    private bool _automaticPortAttemptInProgress;
    private string? _activeHardwarePortName;
    private bool _productionKioskMode;
    private ulong _nextKioskWindowCheckAt;
    private ulong _nextTokenRecountAvailabilityRefreshAt;
    private int _lastScreenCount;
    private Vector2I _lastScreenSize;
    private int _lastCashBalanceRubles;

    public override void _EnterTree()
    {
        _settingsService = AppSettingsLoader.CreateService();
        _settings = AppSettingsLoader.Load(_settingsService);
        _configuredHoppers = TechnicalSettings.ResolveConfiguredHoppers(_settings.Hardware.DispenseMode);
        AppLogger.Initialize(_settings.Logging);
        AppLogger.Debug(
            "Application",
            $"Старт DEBUG runtime: os={OS.GetName()}, mock={_settings.Hardware.UseMock}, "
            + $"port={_settings.Hardware.PortName}, baud={_settings.Hardware.BaudRate}.");
        _tokenInventoryStore = TokenInventoryLoader.CreateStore();
        _tokenInventory = _tokenInventoryStore.Load();
        OperationJournal.Initialize(_settings.Logging);
        ConfigurationAuditJournal.Initialize(_settings.Logging);
        _serviceAccessPolicy = new ServiceAccessPolicy(_settings.Security);
        ConfigureWindow(_settings.Window);
        AudioSettingsApplier.Apply(_settings.Audio);

        ThemeManager themeManager = GetNode<ThemeManager>("/root/ThemeManager");
        themeManager.Configure(_settings.Themes);
    }

    public override void _Ready()
    {
        _sessionController = GetNode<SessionController>("SessionController");

        ConfigureHardware();
        _tokenRecountController = _machineController as ITokenRecountController;
        _dualHopperController = _machineController as IDualHopperMachineController;
        _dualHopperRecountController = _machineController as IDualHopperTokenRecountController;
        _dualHopperController?.SetInventoryAccountingEnabled(_settings.TokenInventory.Enabled);
        _dualHopperController?.SetPurchaseLimitEnabled(_settings.TokenInventory.PurchaseLimitEnabled);
        (int hopper1Inventory, int hopper2Inventory) = GetConfiguredInventoryCounts();
        _dualHopperController?.SetHopperInventory(
            hopper1Inventory,
            hopper2Inventory);

        ConfigureScreens();
        SubscribeSession();
        SubscribeMachine();
        _sessionController.Configure(_settings, _machineController, GetTokenPurchaseLimit());
        _homeScreen.SetConnectionState(_machineController.ConnectionState);
        RefreshInventoryState();
        OnSessionStateChanged(SessionState.Home);
        AppLogger.Debug(
            "Application",
            $"Первичное подключение: reconnectSeconds={_settings.Hardware.ReconnectIntervalSeconds}, "
            + $"handshakeTimeoutMs={_settings.Hardware.HandshakeTimeoutMilliseconds}.");
        _hardwareReconnectScheduler = new HardwareReconnectScheduler(
            _settings.Hardware.ReconnectIntervalSeconds);
        _hardwareReconnectScheduler.ScheduleFrom(Time.GetTicksMsec());
        ConnectConfiguredHardwarePort(refreshAutomaticCandidates: true, attemptKind: "первичное подключение");

        if (_settings.Hardware.UseMock)
        {
            _hardwarePort!.SendCommand("PING\n");
            AppLogger.Info("Application", "Mock-сценарий Exchanger запущен.");
        }
        else
        {
            AppLogger.Info("Application", "Запущена интеграция универсального периферийного контроллера 2.3.5.");
        }
    }

    public override void _Process(double delta)
    {
        _hardwarePort?.ProcessPendingEvents();
        ulong now = Time.GetTicksMsec();
        _peripheryController?.Update(now);
        bool transportConnected = _hardwarePort?.IsConnected == true;
        if (!_settings.Hardware.UseMock
            && _hardwareReconnectScheduler.ObserveConnection(transportConnected, now))
        {
            AppLogger.Debug(
                "Application",
                $"Обнаружен обрыв транспорта; следующая попытка подключения через "
                + $"{_settings.Hardware.ReconnectIntervalSeconds} с.");
        }

        if (!_settings.Hardware.UseMock
            && _hardwarePort is not null
            && !transportConnected
            && _hardwareReconnectScheduler.TryBeginAttempt(now))
        {
            ConnectConfiguredHardwarePort(refreshAutomaticCandidates: true, attemptKind: "reconnect");
        }

        if (_productionKioskMode && now >= _nextKioskWindowCheckAt)
        {
            _nextKioskWindowCheckAt = now + 2000UL;
            EnsureKioskWindowState();
        }

        if (_screenManager is not null
            && _screenManager.CurrentScreenId == ScreenId.Settings
            && _tokenRecountController?.IsTokenRecountInProgress != true
            && now >= _nextTokenRecountAvailabilityRefreshAt)
        {
            _nextTokenRecountAvailabilityRefreshAt = now + 500UL;
            RefreshTokenInventoryControls(preserveStatus: true);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!OS.IsDebugBuild()
            || !@event.IsActionPressed("debug_service_settings")
            || _sessionController.State != SessionState.Home
            || _screenManager.TransitionInProgress
            || _screenManager.CurrentScreenId is not (ScreenId.Home or ScreenId.Settings))
        {
            return;
        }

        OnPhysicalServiceRequested();
        GetViewport().SetInputAsHandled();
    }

    public override void _ExitTree()
    {
        Shutdown();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            Shutdown();
        }
        else if (what == NotificationApplicationFocusOut)
        {
            AppLogger.Info("Application", "Окно потеряло фокус; таймауты используют монотонное реальное время.");
        }
        else if (what == NotificationApplicationFocusIn)
        {
            AppLogger.Info("Application", "Фокус окна восстановлен.");
            EnsureKioskWindowState();
        }
    }

    private void ConfigureWindow(WindowSettings settings)
    {
        Engine.MaxFps = settings.TargetFps;

        bool useDebugWindow = OS.IsDebugBuild() && settings.DebugWindowInDebugBuild;
        _productionKioskMode = !OS.IsDebugBuild() && settings.Fullscreen;
        if (useDebugWindow || !settings.Fullscreen)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);

            var windowSize = new Vector2I(settings.DebugWindowWidth, settings.DebugWindowHeight);
            DisplayServer.WindowSetSize(windowSize);
            Vector2I screenSize = DisplayServer.ScreenGetSize();
            DisplayServer.WindowSetPosition((screenSize - windowSize) / 2);
            return;
        }

        Input.MouseMode = Input.MouseModeEnum.Hidden;
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, settings.Borderless);
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
        _lastScreenCount = DisplayServer.GetScreenCount();
        _lastScreenSize = DisplayServer.ScreenGetSize();
        _nextKioskWindowCheckAt = Time.GetTicksMsec() + 2000UL;
    }

    private void EnsureKioskWindowState()
    {
        if (!_productionKioskMode)
        {
            return;
        }

        int screenCount = Math.Max(1, DisplayServer.GetScreenCount());
        int currentScreen = Math.Clamp(DisplayServer.WindowGetCurrentScreen(), 0, screenCount - 1);
        if (DisplayServer.WindowGetCurrentScreen() != currentScreen)
        {
            DisplayServer.WindowSetCurrentScreen(currentScreen);
        }

        Vector2I screenSize = DisplayServer.ScreenGetSize(currentScreen);
        if (screenCount != _lastScreenCount || screenSize != _lastScreenSize)
        {
            AppLogger.Info(
                "Display",
                $"Конфигурация дисплея изменилась: screens={screenCount}, size={screenSize.X}x{screenSize.Y}.");
            _lastScreenCount = screenCount;
            _lastScreenSize = screenSize;
        }

        Input.MouseMode = Input.MouseModeEnum.Hidden;
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, _settings.Window.Borderless);
        if (DisplayServer.WindowGetMode() != DisplayServer.WindowMode.Fullscreen)
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
            AppLogger.Warning("Display", "Полноэкранный киоск-режим восстановлен автоматически.");
        }
    }

    private void ConfigureScreens()
    {
        _screenManager = GetNode<ScreenManager>("/root/ScreenManager");
        Control screenHost = GetNode<Control>("UiLayer/ScreenHost");
        ThemeManager themeManager = GetNode<ThemeManager>("/root/ThemeManager");
        _screenManager.Initialize(
            screenHost,
            ScreenRegistry.LoadDefault(),
            themeManager.CurrentTheme?.ScreenScenes,
            themeManager.CurrentTheme?.ScreenBindingContracts);

        _homeScreen = _screenManager.GetScreen<HomeScreenControl>(ScreenId.Home);
        _cashPaymentScreen = _screenManager.GetScreen<CashPaymentScreenControl>(ScreenId.CashPayment);
        _cardAmountScreen = _screenManager.GetScreen<CardAmountScreenControl>(ScreenId.CardAmount);
        _cardCustomAmountScreen = _screenManager.GetScreen<CardCustomAmountScreenControl>(ScreenId.CardCustomAmount);
        _cardTerminalScreen = _screenManager.GetScreen<CardTerminalScreenControl>(ScreenId.CardTerminal);
        _successScreen = _screenManager.GetScreen<SuccessScreenControl>(ScreenId.Success);
        _errorScreen = _screenManager.GetScreen<ErrorScreenControl>(ScreenId.Error);
        _serviceAccessScreen = _screenManager.GetScreen<ServiceAccessScreenControl>(ScreenId.ServiceAccess);
        _settingsScreen = _screenManager.GetScreen<SettingsScreenControl>(ScreenId.Settings);

        _homeScreen.Configure(_settings);
        _applicationAudio = GetNode<ApplicationAudio>("ApplicationAudio");
        _interactivePetHost = GetNode<InteractivePetHostControl>("InteractivePetHost");
        _interactivePetHost.Configure(themeManager, _screenManager);
        _interactivePetHost.SetEnabled(_settings.MenuVisibility.ShowInteractivePet);
        _interactivePetHost.PetInteracted += OnInteractivePetInteracted;
        _interactivePetHost.PetDragStarted += OnInteractivePetDragStarted;
        _interactivePetHost.PetDragReleased += OnInteractivePetDragReleased;
        int? tokenPurchaseLimit = GetTokenPurchaseLimit();
        _cashPaymentScreen.Configure(_settings.Pricing, tokenPurchaseLimit, _settings.AnimatedBannerTexts);
        _cardAmountScreen.Configure(_settings.Pricing, tokenPurchaseLimit, _settings.AnimatedBannerTexts);
        _cardCustomAmountScreen.Configure(_settings.Pricing, tokenPurchaseLimit, _settings.AnimatedBannerTexts);
        _cardTerminalScreen.Configure(_settings.AnimatedBannerTexts);
        ConfigureFooters();
        _cashPaymentScreen.SetDebugControlsVisible(_settings.Hardware.UseMock);
        _cardTerminalScreen.SetDebugControlsVisible(_settings.Hardware.UseMock);
        _settingsScreen.Configure(
            _settingsService,
            BuildRuntimeSummary,
            () => AppLogger.GetRecentSafeEntries(5));
        _settingsScreen.SetConfiguredHoppers(
            _configuredHoppers.Hopper1Enabled,
            _configuredHoppers.Hopper2Enabled);

        _homeScreen.CashRequested += OnCashRequested;
        _homeScreen.CardRequested += OnCardRequested;
        _cashPaymentScreen.BackRequested += OnCancelRequested;
        _cashPaymentScreen.HomeRequested += OnCancelRequested;
        _cashPaymentScreen.MockCashRequested += OnMockCashRequested;
        _cashPaymentScreen.DispenseRequested += OnCashDispenseRequested;
        _cardAmountScreen.BackRequested += OnCancelRequested;
        _cardAmountScreen.HomeRequested += OnCancelRequested;
        _cardAmountScreen.AmountSelected += OnCardAmountSelected;
        _cardAmountScreen.OtherAmountRequested += OnOtherCardAmountRequested;
        _cardCustomAmountScreen.BackRequested += OnCustomAmountBackRequested;
        _cardCustomAmountScreen.HomeRequested += OnCancelRequested;
        _cardCustomAmountScreen.AmountConfirmed += OnCardAmountSelected;
        _cardTerminalScreen.BackRequested += OnTerminalBackRequested;
        _cardTerminalScreen.HomeRequested += OnCancelRequested;
        _cardTerminalScreen.CancelRequested += OnCancelRequested;
        _cardTerminalScreen.DebugApproveRequested += OnDebugCardApproved;
        _cardTerminalScreen.DebugDeclineRequested += OnDebugCardDeclined;
        _cardTerminalScreen.DebugDispenseFailureRequested += OnDebugDispenseFailure;
        _cardTerminalScreen.DebugPartialDispenseRequested += OnDebugPartialDispense;
        _cardTerminalScreen.DebugConnectionLossRequested += OnDebugConnectionLoss;
        _successScreen.HomeRequested += OnReturnHomeRequested;
        _errorScreen.HomeRequested += OnReturnHomeRequested;
        _serviceAccessScreen.Authorized += OnServiceAuthorized;
        _serviceAccessScreen.PinCreated += OnServicePinCreated;
        _serviceAccessScreen.Cancelled += OnServiceAccessCancelled;
        _settingsScreen.Saved += OnSettingsSaved;
        _settingsScreen.SaveAndExitRequested += OnSettingsSaveAndExitRequested;
        _settingsScreen.TestDispenseRequested += OnServiceTestDispenseRequested;
        _settingsScreen.TokenRecountRequested += OnTokenRecountRequested;
        _settingsScreen.ManualTokenAdditionConfirmed += OnManualTokenAdditionConfirmed;
    }

    private void SubscribeSession()
    {
        _sessionController.StateChanged += OnSessionStateChanged;
        _sessionController.CashBalanceChanged += OnCashBalanceChanged;
        _sessionController.StockChanged += OnStockChanged;
        _sessionController.CountdownChanged += OnCountdownChanged;
    }

    private void SubscribeMachine()
    {
        _machineController.ConnectionStateChanged += OnConnectionStateChanged;
        _machineController.DispenseFinished += OnMachineDispenseFinished;
        if (_dualHopperController is not null)
        {
            _dualHopperController.HopperStockChanged += OnHopperStockChanged;
            _dualHopperController.HopperDispenseAccounted += OnHopperDispenseAccounted;
            _dualHopperController.DispenseProgressChanged += OnDispenseProgressChanged;
        }

        if (_dualHopperRecountController is not null)
        {
            _dualHopperRecountController.HopperTokenRecountProgressChanged += OnHopperTokenRecountProgressChanged;
            _dualHopperRecountController.HopperTokenRecountFinished += OnHopperTokenRecountFinished;
        }
        else if (_tokenRecountController is not null)
        {
            _tokenRecountController.TokenRecountProgressChanged += OnTokenRecountProgressChanged;
            _tokenRecountController.TokenRecountFinished += OnTokenRecountFinished;
        }

        if (_peripheryController is not null)
        {
            _peripheryController.ServiceRequested += OnPhysicalServiceRequested;
        }
    }

    private void OnSessionStateChanged(SessionState state)
    {
        switch (state)
        {
            case SessionState.Home:
                _lastCashBalanceRubles = 0;
                RefreshHomeAvailability();
                _screenManager.Show(ScreenId.Home, animate: _screenManager.CurrentScreenId is not null);
                break;
            case SessionState.CashAccepting:
                _lastCashBalanceRubles = _sessionController.CashBalanceRubles;
                _cashPaymentScreen.SetBalance(_sessionController.CashBalanceRubles);
                _screenManager.Show(ScreenId.CashPayment);
                break;
            case SessionState.CardAmountSelection:
                _screenManager.Show(ScreenId.CardAmount);
                break;
            case SessionState.CardCustomAmount:
                _cardCustomAmountScreen.ResetInput();
                _screenManager.Show(ScreenId.CardCustomAmount);
                break;
            case SessionState.CardTerminalWait:
                _cardTerminalScreen.ShowPayment(
                    _sessionController.SelectedAmountRubles,
                    _sessionController.BaseTokens,
                    _sessionController.BonusTokens);
                _screenManager.Show(ScreenId.CardTerminal);
                break;
            case SessionState.Dispensing:
                _successScreen.ShowDispensing(_sessionController.TotalTokens);
                _screenManager.Show(ScreenId.Success);
                _interactivePetHost.RequestDispenseCelebration(4);
                break;
            case SessionState.Completed:
                _applicationAudio.PlayPaymentSuccess();
                _successScreen.ShowCompleted(_sessionController.BaseTokens, _sessionController.BonusTokens);
                _screenManager.Show(ScreenId.Success);
                break;
            case SessionState.PaymentDeclined:
                _applicationAudio.PlayPaymentError();
                _errorScreen.ShowError(_sessionController.ErrorTitle, _sessionController.ErrorMessage);
                _screenManager.Show(ScreenId.Error);
                break;
            case SessionState.Error:
                _errorScreen.ShowError(_sessionController.ErrorTitle, _sessionController.ErrorMessage);
                _screenManager.Show(ScreenId.Error);
                break;
            case SessionState.Cancelled:
                break;
            default:
                AppLogger.Error("Application", $"Неизвестное состояние сессии: {state}. Выполняется безопасный возврат домой.");
                _sessionController.ReturnHome();
                break;
        }
    }

    private void OnCashRequested() => _sessionController.BeginCash();

    private void OnCardRequested() => _sessionController.BeginCard();

    private void OnCancelRequested() => _sessionController.Cancel();

    private void OnReturnHomeRequested() => _sessionController.ReturnHome();

    private void OnInteractivePetInteracted() => _sessionController.NotifyUserActivity();

    private void OnInteractivePetDragStarted() => _applicationAudio.PlayPetHeld();

    private void OnInteractivePetDragReleased() => _applicationAudio.PlayPetReleased();

    private void OnSettingsSaved(
        AppSettings savedSettings,
        SettingsChangeSummary changeSummary,
        ServiceAccessLevel accessLevel)
    {
        _settings = savedSettings;
        _serviceAccessPolicy = new ServiceAccessPolicy(_settings.Security);
        AppLogger.Initialize(_settings.Logging);
        OperationJournal.Initialize(_settings.Logging);
        ConfigurationAuditJournal.Initialize(_settings.Logging);
        ConfigurationAuditJournal.Record(changeSummary, accessLevel);
        GetNode<ThemeManager>("/root/ThemeManager").SetReducedEffects(_settings.Themes.ReducedEffects);
        _interactivePetHost.SetEnabled(_settings.MenuVisibility.ShowInteractivePet);
        _homeScreen.Configure(_settings);
        _dualHopperController?.SetInventoryAccountingEnabled(_settings.TokenInventory.Enabled);
        _dualHopperController?.SetPurchaseLimitEnabled(_settings.TokenInventory.PurchaseLimitEnabled);
        int? tokenPurchaseLimit = GetTokenPurchaseLimit();
        _cashPaymentScreen.Configure(_settings.Pricing, tokenPurchaseLimit, _settings.AnimatedBannerTexts);
        _cardAmountScreen.Configure(_settings.Pricing, tokenPurchaseLimit, _settings.AnimatedBannerTexts);
        _cardCustomAmountScreen.Configure(_settings.Pricing, tokenPurchaseLimit, _settings.AnimatedBannerTexts);
        _cardTerminalScreen.Configure(_settings.AnimatedBannerTexts);
        ConfigureFooters();
        _sessionController.Configure(_settings, _machineController, tokenPurchaseLimit);
        RefreshInventoryState();
        _homeScreen.SetConnectionState(_machineController.ConnectionState);
        RefreshServiceTestAvailability();
    }

    private void OnSettingsSaveAndExitRequested()
    {
        _screenManager.Show(ScreenId.Home);
        AppLogger.Info("Service", "Настройки сохранены и закрыты экранной кнопкой.");
    }

    private void ConfigureFooters()
    {
        Control[] screens =
        {
            _cashPaymentScreen,
            _cardAmountScreen,
            _cardCustomAmountScreen,
            _cardTerminalScreen,
            _successScreen,
            _errorScreen,
            _serviceAccessScreen,
        };

        foreach (Control screen in screens)
        {
            if (screen.FindChild("Footer", recursive: true, owned: false) is not Control footer)
            {
                continue;
            }

            if (footer is KioskFooterControl builtInFooter)
            {
                builtInFooter.Configure(_settings.Branding);
                continue;
            }

            // Внешняя тема владеет готовой сценой footer. Меняем только уже существующую
            // текстовую точку компонента, не добавляя и не перестраивая её визуальные узлы.
            if (footer.FindChild("Support", recursive: true, owned: false) is Label supportLabel)
            {
                supportLabel.Text = $"ТЕХПОДДЕРЖКА:\n{_settings.Branding.SupportPhone}";
            }
        }
    }

    private void OnPhysicalServiceRequested()
    {
        if (_screenManager.TransitionInProgress)
        {
            AppLogger.Warning("Service", "Запрос SERVICE отклонён: выполняется переход между экранами.");
            return;
        }

        if (_screenManager.CurrentScreenId == ScreenId.Settings)
        {
            if (_settingsScreen.TryFinishEditing())
            {
                _screenManager.Show(ScreenId.Home);
                AppLogger.Info("Service", "Настройки закрыты повторным нажатием сервисной кнопки.");
            }
            else
            {
                AppLogger.Warning("Service", "Выход из настроек отложен: автосохранение не завершено.");
            }

            return;
        }

        if (_sessionController.State != SessionState.Home
            || _screenManager.CurrentScreenId != ScreenId.Home)
        {
            AppLogger.Warning("Service", "Запрос SERVICE отклонён: приложение занято пользовательской операцией.");
            return;
        }

        if (!_settings.Security.ServicePinEnabled)
        {
            OpenSettingsWithoutPin();
            AppLogger.Info("Service", "Настройки открыты напрямую: защита сервисным PIN выключена.");
            return;
        }

        _serviceAccessScreen.Begin(_serviceAccessPolicy);
        _screenManager.Show(ScreenId.ServiceAccess);
        AppLogger.Info("Service", "Открыт защищённый сервисный вход по физическому запросу.");
    }

    private void OpenSettingsWithoutPin()
    {
        ServiceAccessLevel accessLevel = OS.IsDebugBuild()
            ? ServiceAccessLevel.Debug
            : ServiceAccessLevel.Engineer;
        _settingsScreen.BeginEdit(accessLevel);
        _screenManager.Show(ScreenId.Settings);
        RefreshServiceTestAvailability();
        RefreshTokenInventoryControls();
    }

    private void OnServicePinCreated(string pin)
    {
        try
        {
            AppSettings previous = _settingsService.Current;
            AppSettings candidate = _settingsService.CreateWorkingCopy();
            ServiceAccessPolicy.SetPin(candidate.Security, pin);
            AppSettings saved = _settingsService.Save(candidate);
            SettingsChangeSummary summary = SettingsChangeDetector.Compare(previous, saved);
            OnSettingsSaved(saved, summary, ServiceAccessLevel.Engineer);
            AppLogger.Info("Service", "Новый сервисный PIN настроен внутри приложения.");
            OnServiceAuthorized(ServiceAccessLevel.Engineer);
        }
        catch (Exception exception)
        {
            AppLogger.Error("Service", $"Не удалось сохранить сервисный PIN: {exception.GetType().Name}.");
            _serviceAccessScreen.ShowPersistenceError();
        }
    }

    private void OnServiceAuthorized(ServiceAccessLevel accessLevel)
    {
        if (accessLevel is not (ServiceAccessLevel.Operator or ServiceAccessLevel.Engineer))
        {
            return;
        }

        _settingsScreen.BeginEdit(accessLevel);
        _screenManager.Show(ScreenId.Settings);
        RefreshServiceTestAvailability();
        RefreshTokenInventoryControls();
        AppLogger.Info("Service", $"Сервисный доступ подтверждён: role={accessLevel}.");
    }

    private void OnServiceAccessCancelled() => _screenManager.Show(ScreenId.Home);

    private void OnServiceTestDispenseRequested()
    {
        if (_serviceDispenseInProgress || !CanRunServiceDispense())
        {
            _settingsScreen.SetServiceTestAvailability(false, "Тест отклонён: контроллер не готов или приложение занято.");
            return;
        }

        _serviceDispenseInProgress = true;
        _settingsScreen.SetServiceTestAvailability(false, "Команда отправлена; ожидается подтверждение выдачи.");
        AppLogger.Warning("Service", "Запущена подтверждённая сервисная выдача одного жетона.");
        _machineController.DispenseTokens(1);
    }

    private void OnMachineDispenseFinished(bool success, int dispensedTokens, string? error)
    {
        if (_settings.TokenInventory.Enabled
            && _dualHopperController is null
            && dispensedTokens > 0)
        {
            try
            {
                _tokenInventory = _tokenInventoryStore.RemoveDispensed(dispensedTokens);
                RefreshInventoryState();
            }
            catch (Exception exception)
            {
                AppLogger.Error("TokenInventory", $"Не удалось списать выданные жетоны: {exception.GetType().Name}.");
            }
        }

        if (!_serviceDispenseInProgress)
        {
            return;
        }

        _serviceDispenseInProgress = false;
        _settingsScreen.SetServiceTestResult(success, dispensedTokens, CanRunServiceDispense());
        AppLogger.Info(
            "Service",
            $"Сервисная выдача завершена: success={success}, confirmed={dispensedTokens}.");
    }

    private void OnHopperDispenseAccounted(int hopper1Count, int hopper2Count)
    {
        if (!_settings.TokenInventory.Enabled)
        {
            return;
        }

        try
        {
            _tokenInventory = _tokenInventoryStore.SetCounts(
                Math.Max(0, _tokenInventory.Hopper1Count - hopper1Count),
                Math.Max(0, _tokenInventory.Hopper2Count - hopper2Count));
            RefreshInventoryState();
            AppLogger.Info(
                "TokenInventory",
                $"Списана подтверждённая выдача: hopper1={hopper1Count}, hopper2={hopper2Count}, total={_tokenInventory.Count}.");
        }
        catch (Exception exception)
        {
            AppLogger.Error("TokenInventory", $"Не удалось списать выдачу по хопперам: {exception.GetType().Name}.");
        }
    }

    private void OnDispenseProgressChanged(int confirmedTokens, int requestedTokens)
    {
        if (_sessionController.State == SessionState.Dispensing)
        {
            _successScreen.ShowDispenseProgress(confirmedTokens, requestedTokens);
        }
    }

    private void OnTokenRecountRequested(int hopperNumber)
    {
        if (!_settings.TokenInventory.Enabled)
        {
            RefreshTokenInventoryControls();
            return;
        }

        HopperChannel hopper = ToHopperChannel(hopperNumber);
        bool started = _dualHopperRecountController is not null
            ? CanRunTokenRecount(hopper)
              && _dualHopperRecountController.TryStartTokenRecount(hopper, Time.GetTicksMsec())
            : hopper == HopperChannel.Hopper1
              && CanRunTokenRecount(hopper)
              && _tokenRecountController!.TryStartTokenRecount(Time.GetTicksMsec());
        if (!started)
        {
            RefreshTokenInventoryControls();
            _settingsScreen.SetTokenRecountFailure("Пересчёт сейчас недоступен. Проверьте подключение контроллера.");
            return;
        }

        _settingsScreen.SetTokenRecountProgress((int)hopper, 0);
        AppLogger.Warning("TokenInventory", $"Запущен автоматический пересчёт хоппера {(int)hopper}.");
    }

    private void OnTokenRecountProgressChanged(int count) =>
        OnHopperTokenRecountProgressChanged(HopperChannel.Hopper1, count);

    private void OnHopperTokenRecountProgressChanged(HopperChannel hopper, int count)
    {
        if (GodotObject.IsInstanceValid(_settingsScreen))
        {
            _settingsScreen.SetTokenRecountProgress((int)hopper, count);
        }
    }

    private void OnTokenRecountFinished(bool success, int countedTokens, string? errorCode) =>
        OnHopperTokenRecountFinished(HopperChannel.Hopper1, success, countedTokens, errorCode);

    private void OnHopperTokenRecountFinished(
        HopperChannel hopper,
        bool success,
        int countedTokens,
        string? errorCode)
    {
        if (!GodotObject.IsInstanceValid(_settingsScreen))
        {
            return;
        }

        if (!success)
        {
            AppLogger.Warning(
                "TokenInventory",
                $"Пересчёт хоппера {(int)hopper} прерван: code={errorCode ?? "UNKNOWN"}, counted={countedTokens}.");
            RefreshTokenInventoryControls();
            _settingsScreen.SetTokenRecountFailure("Пересчёт остановлен. Старый учтённый остаток не изменён.");
            return;
        }

        try
        {
            _tokenInventory = _tokenInventoryStore.SetHopperCount(ToTokenHopper(hopper), countedTokens);
            RefreshInventoryState();
            _settingsScreen.SetTokenRecountCompleted((int)hopper, countedTokens);
            AppLogger.Info(
                "TokenInventory",
                $"Хоппер {(int)hopper} пересчитан автоматически: {countedTokens}; общий остаток {_tokenInventory.Count}.");
        }
        catch (Exception exception)
        {
            AppLogger.Error("TokenInventory", $"Не удалось записать остаток после пересчёта: {exception.GetType().Name}.");
            _settingsScreen.SetTokenRecountFailure("Пересчёт завершён, но не удалось записать результат. Повторите операцию.");
        }
    }

    private void OnManualTokenAdditionConfirmed(int hopperNumber, int count)
    {
        HopperChannel channel = ToHopperChannel(hopperNumber);
        if (!_settings.TokenInventory.Enabled || !IsHopperConfigured(channel))
        {
            AppLogger.Warning("TokenInventory", $"Пополнение отключённого хоппера {hopperNumber} проигнорировано.");
            RefreshTokenInventoryControls();
            return;
        }

        TokenHopper hopper = hopperNumber == 2 ? TokenHopper.Hopper2 : TokenHopper.Hopper1;
        try
        {
            _tokenInventory = _tokenInventoryStore.Add(hopper, count);
            RefreshInventoryState();
            AppLogger.Info(
                "TokenInventory",
                $"В хоппер {hopperNumber} вручную добавлено {count}; общий остаток {_tokenInventory.Count}.");
        }
        catch (Exception exception)
        {
            AppLogger.Error("TokenInventory", $"Не удалось добавить жетоны вручную: {exception.GetType().Name}.");
            _settingsScreen.SetTokenRecountFailure("Не удалось записать новый остаток. Проверьте количество и повторите.");
        }
    }

    private bool CanRunTokenRecount(HopperChannel hopper)
    {
        if (!_settings.TokenInventory.Enabled || !IsHopperConfigured(hopper))
        {
            return false;
        }

        bool controllerReady = _dualHopperRecountController is not null
            ? _dualHopperRecountController.CanStartHopperTokenRecount(hopper)
            : hopper == HopperChannel.Hopper1
              && _tokenRecountController?.CanStartTokenRecount == true;
        return controllerReady
               && _sessionController.State == SessionState.Home
               && _screenManager.CurrentScreenId == ScreenId.Settings;
    }

    private void RefreshTokenInventoryControls(bool preserveStatus = false)
    {
        if (!GodotObject.IsInstanceValid(_settingsScreen))
        {
            return;
        }

        _settingsScreen.SetTokenInventoryCounts(
            _tokenInventory.Hopper1Count,
            _tokenInventory.Hopper2Count);
        if (!_settings.TokenInventory.Enabled)
        {
            _settingsScreen.SetTokenRecountAvailability(
                false,
                false,
                preserveStatus ? string.Empty : "Учёт жетонов отключён.");
            return;
        }

        bool hopper1Available = CanRunTokenRecount(HopperChannel.Hopper1);
        bool hopper2Available = CanRunTokenRecount(HopperChannel.Hopper2);
        bool singleConfiguredHopper = _configuredHoppers.Hopper1Enabled
                                      != _configuredHoppers.Hopper2Enabled;
        string status = preserveStatus
            ? string.Empty
            : !_configuredHoppers.Hopper1Enabled && !_configuredHoppers.Hopper2Enabled
                ? "Хопперы отключены в технической конфигурации."
                : hopper1Available || hopper2Available
                ? singleConfiguredHopper
                    ? "Подключённый хоппер можно пересчитать или пополнить."
                    : "Выберите хоппер: его можно пересчитать или пополнить отдельно."
                : "Пересчёт станет доступен после подключения контроллера. Жетоны можно добавить вручную.";
        _settingsScreen.SetTokenRecountAvailability(hopper1Available, hopper2Available, status);
    }

    private void RefreshInventoryState()
    {
        _dualHopperController?.SetInventoryAccountingEnabled(_settings.TokenInventory.Enabled);
        _dualHopperController?.SetPurchaseLimitEnabled(_settings.TokenInventory.PurchaseLimitEnabled);
        (int hopper1Inventory, int hopper2Inventory) = GetConfiguredInventoryCounts();
        _dualHopperController?.SetHopperInventory(
            hopper1Inventory,
            hopper2Inventory);
        if (GodotObject.IsInstanceValid(_settingsScreen))
        {
            _settingsScreen.SetTokenInventoryCounts(
                _tokenInventory.Hopper1Count,
                _tokenInventory.Hopper2Count);
        }

        RefreshTokenPurchaseLimits();
        RefreshHomeAvailability();
    }

    private void RefreshHomeAvailability()
    {
        MachineStockLevel hopper1 = _dualHopperController?.Hopper1StockLevel ?? _machineController.StockLevel;
        MachineStockLevel hopper2 = _dualHopperController?.Hopper2StockLevel ?? _machineController.StockLevel;
        (int hopper1Inventory, int hopper2Inventory) = GetConfiguredInventoryCounts();
        int configuredInventory = hopper1Inventory + hopper2Inventory;
        if (_configuredHoppers.Hopper1Enabled && !_configuredHoppers.Hopper2Enabled)
        {
            hopper2 = hopper1;
        }
        else if (!_configuredHoppers.Hopper1Enabled && _configuredHoppers.Hopper2Enabled)
        {
            hopper1 = hopper2;
        }

        TokenAvailabilityLevel level = IsPurchaseLimitActive()
            ? TokenInventoryPolicy.ResolveAvailability(configuredInventory, hopper1, hopper2)
            : TokenInventoryPolicy.ResolveWithoutInventory(
                hopper1,
                hopper2,
                _configuredHoppers.Hopper1Enabled,
                _configuredHoppers.Hopper2Enabled);
        _homeScreen.SetTokenAvailability(level, configuredInventory);
    }

    private int? GetTokenPurchaseLimit()
    {
        if (!IsPurchaseLimitActive())
        {
            return null;
        }

        (int Hopper1, int Hopper2) inventory = GetConfiguredInventoryCounts();
        return inventory.Hopper1 + inventory.Hopper2;
    }

    private bool IsPurchaseLimitActive() =>
        _settings.TokenInventory.Enabled && _settings.TokenInventory.PurchaseLimitEnabled;

    private void RefreshTokenPurchaseLimits()
    {
        if (!GodotObject.IsInstanceValid(_cashPaymentScreen))
        {
            return;
        }

        int? limit = GetTokenPurchaseLimit();
        _sessionController.SetMaximumAvailableTokens(limit);
        _cashPaymentScreen.SetTokenPurchaseLimit(limit);
        _cardAmountScreen.SetTokenPurchaseLimit(limit);
        _cardCustomAmountScreen.SetTokenPurchaseLimit(limit);
    }

    private (int Hopper1, int Hopper2) GetConfiguredInventoryCounts() =>
        (
            _configuredHoppers.Hopper1Enabled ? _tokenInventory.Hopper1Count : 0,
            _configuredHoppers.Hopper2Enabled ? _tokenInventory.Hopper2Count : 0
        );

    private bool IsHopperConfigured(HopperChannel hopper) =>
        hopper == HopperChannel.Hopper1
            ? _configuredHoppers.Hopper1Enabled
            : _configuredHoppers.Hopper2Enabled;

    private static HopperChannel ToHopperChannel(int hopperNumber) =>
        hopperNumber == 2 ? HopperChannel.Hopper2 : HopperChannel.Hopper1;

    private static TokenHopper ToTokenHopper(HopperChannel hopper) =>
        hopper == HopperChannel.Hopper2 ? TokenHopper.Hopper2 : TokenHopper.Hopper1;

    private bool CanRunServiceDispense()
    {
        bool supportedProfile = _settings.Hardware.UseMock
                                || _settings.Hardware.DispenseMode.Equals("Hopper1Auto", StringComparison.OrdinalIgnoreCase)
                                || _settings.Hardware.DispenseMode.Equals("Hopper2Auto", StringComparison.OrdinalIgnoreCase)
                                || _settings.Hardware.DispenseMode.Equals("DualHopperAuto", StringComparison.OrdinalIgnoreCase);
        return !_serviceDispenseInProgress
               && supportedProfile
               && (!IsPurchaseLimitActive()
                   || GetConfiguredInventoryCounts() is var configuredInventory
                   && configuredInventory.Hopper1 + configuredInventory.Hopper2 > 0)
               && _sessionController.State == SessionState.Home
               && _machineController.ConnectionState == MachineConnectionState.Ready
               && _machineController.StockLevel is MachineStockLevel.Enough or MachineStockLevel.Low
               && _screenManager.CurrentScreenId == ScreenId.Settings;
    }

    private void RefreshServiceTestAvailability()
    {
        if (!GodotObject.IsInstanceValid(_settingsScreen))
        {
            return;
        }

        bool available = CanRunServiceDispense();
        string status = available
            ? "Контроллер готов. Для запуска потребуется двойное подтверждение."
            : "Тест недоступен: требуется Home, готовое соединение и настроенная команда выдачи.";
        _settingsScreen.SetServiceTestAvailability(available, status);
    }

    private string BuildRuntimeSummary()
    {
        string version = ProjectSettings.GetSetting("application/config/version", "не указана").AsString();
        ThemeManager themeManager = GetNode<ThemeManager>("/root/ThemeManager");
        string theme = themeManager.CurrentTheme?.DisplayName ?? "встроенная fallback";
        string port = _settings.Hardware.UseMock ? "имитация оборудования" : _settings.Hardware.PortName;
        string summary = $"Версия: {version}\nТема: {theme}\nПорт: {port}\nСоединение: {_machineController.ConnectionState}\nЗапас: {_machineController.StockLevel}";
        if (!OS.IsDebugBuild())
        {
            return summary;
        }

        string transport = _hardwarePort?.IsConnected == true ? "Connected" : "Disconnected";
        string authentication = _peripheryController?.AuthenticationState.ToString()
            ?? (_settings.Hardware.UseMock ? "Mock" : "NotCreated");
        return $"{summary}\nПлатформа: {OS.GetName()}\nТранспорт: {transport}\nАвторизация: {authentication}\n"
               + $"UART: {_settings.Hardware.BaudRate} 8N1, reconnect {_settings.Hardware.ReconnectIntervalSeconds} с";
    }

    private void OnCustomAmountBackRequested() => _sessionController.ReturnToCardAmounts();

    private void OnTerminalBackRequested() => _sessionController.ReturnToCardAmounts();

    private void OnCardAmountSelected(int amountRubles) => _sessionController.SelectCardAmount(amountRubles);

    private void OnOtherCardAmountRequested() => _sessionController.OpenCustomCardAmount();

    private void OnCashDispenseRequested() => _sessionController.ConfirmCashDispense();

    private void OnMockCashRequested(int amountRubles)
    {
        _sessionController.NotifyUserActivity();
        _mockMachine?.AddCash(amountRubles);
    }

    private void OnDebugCardApproved() => _mockMachine?.SimulateCardResult(MachineCardResult.Approved);

    private void OnDebugCardDeclined() => _mockMachine?.SimulateCardResult(MachineCardResult.Declined);

    private void OnDebugDispenseFailure()
    {
        _mockMachine?.SimulateNextDispenseFailure();
        _mockMachine?.SimulateCardResult(MachineCardResult.Approved);
    }

    private void OnDebugPartialDispense()
    {
        int partialCount = Math.Max(1, _sessionController.TotalTokens / 2);
        _mockMachine?.SimulateNextPartialDispense(partialCount);
        _mockMachine?.SimulateCardResult(MachineCardResult.Approved);
    }

    private void OnDebugConnectionLoss() => _mockMachine?.SimulateConnectionLoss();

    private void OnCashBalanceChanged(int balanceRubles)
    {
        if (balanceRubles > _lastCashBalanceRubles)
        {
            _applicationAudio.PlayCashAdded();
        }

        _lastCashBalanceRubles = balanceRubles;
        _cashPaymentScreen.SetBalance(balanceRubles);
    }

    private void OnStockChanged(MachineStockLevel stockLevel)
    {
        RefreshHomeAvailability();
        RefreshServiceTestAvailability();
    }

    private void OnHopperStockChanged(HopperChannel hopper, MachineStockLevel stockLevel)
    {
        RefreshHomeAvailability();
        RefreshServiceTestAvailability();
    }

    private void OnConnectionStateChanged(MachineConnectionState state)
    {
        if (state == MachineConnectionState.Ready
            && IsAutomaticPortSearch()
            && !string.IsNullOrWhiteSpace(_activeHardwarePortName))
        {
            _automaticPortAttemptInProgress = false;
            AppLogger.Info("Application", $"Контроллер авторизован через автоматически найденный порт {_activeHardwarePortName}.");
        }

        _homeScreen.SetConnectionState(state);
        RefreshHomeAvailability();
        RefreshServiceTestAvailability();
        RefreshTokenInventoryControls();
    }

    private void OnCountdownChanged(int seconds)
    {
        _cardTerminalScreen.SetCountdown(seconds);
        _successScreen.SetCountdown(seconds);
        _errorScreen.SetCountdown(seconds);
    }

    private void Shutdown()
    {
        if (_shutdownCompleted)
        {
            return;
        }

        _shutdownCompleted = true;
        UnsubscribeScreens();
        UnsubscribeSession();
        UnsubscribeMachine();
        if (GodotObject.IsInstanceValid(_sessionController))
        {
            _sessionController.Shutdown();
        }

        if (_hardwarePort is not null)
        {
            _peripheryController?.Dispose();
            _peripheryController = null;
            _hardwarePort.Disconnect();
            if (_hardwarePort is IDisposable disposablePort)
            {
                disposablePort.Dispose();
            }

            _hardwarePort = null;
        }

        _controllerCipher?.Dispose();
        _controllerCipher = null;

        OperationJournal.Shutdown();
        ConfigurationAuditJournal.Shutdown();
        AppLogger.Shutdown();
    }

    private void UnsubscribeScreens()
    {
        if (!GodotObject.IsInstanceValid(_homeScreen))
        {
            return;
        }

        _homeScreen.CashRequested -= OnCashRequested;
        _homeScreen.CardRequested -= OnCardRequested;
        _cashPaymentScreen.BackRequested -= OnCancelRequested;
        _cashPaymentScreen.HomeRequested -= OnCancelRequested;
        _cashPaymentScreen.MockCashRequested -= OnMockCashRequested;
        _cashPaymentScreen.DispenseRequested -= OnCashDispenseRequested;
        _cardAmountScreen.BackRequested -= OnCancelRequested;
        _cardAmountScreen.HomeRequested -= OnCancelRequested;
        _cardAmountScreen.AmountSelected -= OnCardAmountSelected;
        _cardAmountScreen.OtherAmountRequested -= OnOtherCardAmountRequested;
        _cardCustomAmountScreen.BackRequested -= OnCustomAmountBackRequested;
        _cardCustomAmountScreen.HomeRequested -= OnCancelRequested;
        _cardCustomAmountScreen.AmountConfirmed -= OnCardAmountSelected;
        _cardTerminalScreen.BackRequested -= OnTerminalBackRequested;
        _cardTerminalScreen.HomeRequested -= OnCancelRequested;
        _cardTerminalScreen.CancelRequested -= OnCancelRequested;
        _cardTerminalScreen.DebugApproveRequested -= OnDebugCardApproved;
        _cardTerminalScreen.DebugDeclineRequested -= OnDebugCardDeclined;
        _cardTerminalScreen.DebugDispenseFailureRequested -= OnDebugDispenseFailure;
        _cardTerminalScreen.DebugPartialDispenseRequested -= OnDebugPartialDispense;
        _cardTerminalScreen.DebugConnectionLossRequested -= OnDebugConnectionLoss;
        _successScreen.HomeRequested -= OnReturnHomeRequested;
        _errorScreen.HomeRequested -= OnReturnHomeRequested;
        _serviceAccessScreen.Authorized -= OnServiceAuthorized;
        _serviceAccessScreen.PinCreated -= OnServicePinCreated;
        _serviceAccessScreen.Cancelled -= OnServiceAccessCancelled;
        _settingsScreen.Saved -= OnSettingsSaved;
        _settingsScreen.SaveAndExitRequested -= OnSettingsSaveAndExitRequested;
        _settingsScreen.TestDispenseRequested -= OnServiceTestDispenseRequested;
        _settingsScreen.TokenRecountRequested -= OnTokenRecountRequested;
        _settingsScreen.ManualTokenAdditionConfirmed -= OnManualTokenAdditionConfirmed;
        if (GodotObject.IsInstanceValid(_interactivePetHost))
        {
            _interactivePetHost.PetInteracted -= OnInteractivePetInteracted;
            _interactivePetHost.PetDragStarted -= OnInteractivePetDragStarted;
            _interactivePetHost.PetDragReleased -= OnInteractivePetDragReleased;
        }
    }

    private void UnsubscribeSession()
    {
        if (!GodotObject.IsInstanceValid(_sessionController))
        {
            return;
        }

        _sessionController.StateChanged -= OnSessionStateChanged;
        _sessionController.CashBalanceChanged -= OnCashBalanceChanged;
        _sessionController.StockChanged -= OnStockChanged;
        _sessionController.CountdownChanged -= OnCountdownChanged;
    }

    private void UnsubscribeMachine()
    {
        if (_hardwarePort is not null)
        {
            _hardwarePort.ConnectionChanged -= OnHardwareTransportConnectionChanged;
        }

        if (_machineController is not null)
        {
            _machineController.ConnectionStateChanged -= OnConnectionStateChanged;
            _machineController.DispenseFinished -= OnMachineDispenseFinished;
        }

        if (_dualHopperController is not null)
        {
            _dualHopperController.HopperStockChanged -= OnHopperStockChanged;
            _dualHopperController.HopperDispenseAccounted -= OnHopperDispenseAccounted;
            _dualHopperController.DispenseProgressChanged -= OnDispenseProgressChanged;
        }

        if (_dualHopperRecountController is not null)
        {
            _dualHopperRecountController.HopperTokenRecountProgressChanged -= OnHopperTokenRecountProgressChanged;
            _dualHopperRecountController.HopperTokenRecountFinished -= OnHopperTokenRecountFinished;
        }
        else if (_tokenRecountController is not null)
        {
            _tokenRecountController.TokenRecountProgressChanged -= OnTokenRecountProgressChanged;
            _tokenRecountController.TokenRecountFinished -= OnTokenRecountFinished;
        }

        if (_peripheryController is not null)
        {
            _peripheryController.ServiceRequested -= OnPhysicalServiceRequested;
        }
    }

    private void ConfigureHardware()
    {
        if (_settings.Hardware.UseMock)
        {
            _hardwarePort = new MockHardwarePort();
            _mockMachine = new MockMachineController { Name = "MockMachineController" };
            AddChild(_mockMachine);
            MachineStockLevel initialStockLevel = Enum.TryParse(
                _settings.Hardware.MockStockLevel,
                ignoreCase: true,
                out MachineStockLevel parsedStockLevel)
                ? parsedStockLevel
                : MachineStockLevel.Enough;
            _mockMachine.Configure(initialStockLevel);
            _machineController = _mockMachine;
            return;
        }

        _hardwarePort = new SerialPortHardware(
            _settings.Hardware.MaximumFrameLength,
            _settings.Hardware.FrameTimeoutMilliseconds);
        _hardwarePort.ConnectionChanged += OnHardwareTransportConnectionChanged;
        AppLogger.Debug(
            "Application",
            $"Создаётся реальный serial-транспорт: frameTimeoutMs={_settings.Hardware.FrameTimeoutMilliseconds}, "
            + $"maxFrameLength={_settings.Hardware.MaximumFrameLength}.");
        IControllerBlockCipher? cipher = CreateControllerCipher(_settings.Hardware);
        _controllerCipher = cipher as IDisposable;
        var authenticator = new ControllerAuthenticator(cipher);
        PeripheryDispenseMode dispenseMode = Enum.TryParse(
            _settings.Hardware.DispenseMode,
            ignoreCase: true,
            out PeripheryDispenseMode parsedMode)
            ? parsedMode
            : PeripheryDispenseMode.Unsupported;
        _peripheryController = new PeripheryMachineController(
            _hardwarePort,
            authenticator,
            dispenseMode,
            _settings.Hardware.CashlessKeepAliveSeconds,
            _settings.Hardware.StockSensorInputIndex,
            _settings.Hardware.StockLowWhenInputHigh,
            _settings.Hardware.StockPollSeconds,
            _settings.Hardware.HandshakeTimeoutMilliseconds,
            _settings.Hardware.ServiceButtonInputIndex,
            _settings.Hardware.ServiceButtonActiveHigh,
            _settings.Hardware.Hopper1DispenseSensorInputIndex,
            _settings.Hardware.Hopper1DispenseSensorActiveHigh,
            _settings.Hardware.Hopper2DispenseSensorInputIndex,
            _settings.Hardware.Hopper2DispenseSensorActiveHigh,
            _settings.Hardware.InputPollMilliseconds,
            _settings.Hardware.Hopper2StockSensorInputIndex,
            _settings.Hardware.Hopper2StockLowWhenInputHigh);
        _machineController = _peripheryController;
    }

    private void ConnectConfiguredHardwarePort(bool refreshAutomaticCandidates, string attemptKind)
    {
        if (_hardwarePort is null) return;
        if (_settings.Hardware.UseMock || !IsAutomaticPortSearch())
        {
            _activeHardwarePortName = SerialPortCandidateResolver.NormalizePortName(
                _settings.Hardware.PortName,
                SerialPortNameValidator.CurrentPlatform);
            AppLogger.Debug("Application", $"Запущено {attemptKind}: port={_activeHardwarePortName}, nextAttemptInSeconds={_settings.Hardware.ReconnectIntervalSeconds}.");
            _hardwarePort.Connect(_activeHardwarePortName, _settings.Hardware.BaudRate);
            return;
        }
        if (refreshAutomaticCandidates)
        {
            _automaticPortCandidates = DiscoverAutomaticPortCandidates();
            _nextAutomaticPortCandidateIndex = 0;
            AppLogger.Debug("Application", $"Запущен {attemptKind} автоматического поиска: prefix={_settings.Hardware.PortName}, candidates={_automaticPortCandidates.Count}, nextAttemptInSeconds={_settings.Hardware.ReconnectIntervalSeconds}.");
        }
        TryConnectNextAutomaticPortCandidate();
    }

    private void OnHardwareTransportConnectionChanged(bool connected)
    {
        if (_shutdownCompleted || _settings.Hardware.UseMock || !IsAutomaticPortSearch()) return;
        if (connected) return;
        bool authenticationFailed = _peripheryController?.ConnectionState == MachineConnectionState.Faulted;
        if (!_automaticPortAttemptInProgress && !authenticationFailed) return;
        _automaticPortAttemptInProgress = false;
        AppLogger.Debug("Application", authenticationFailed ? "Порт не прошёл авторизацию; продолжается автоматический поиск контроллера." : "Порт не удалось открыть; продолжается автоматический поиск контроллера.");
        TryConnectNextAutomaticPortCandidate();
    }

    private IReadOnlyList<string> DiscoverAutomaticPortCandidates()
    {
        try
        {
            return SerialPortCandidateResolver.ResolveCandidates(_settings.Hardware.PortName, SerialPortNameValidator.CurrentPlatform, System.IO.Ports.SerialPort.GetPortNames());
        }
        catch (Exception exception) when (exception is System.IO.IOException or UnauthorizedAccessException)
        {
            AppLogger.Warning("Application", $"Не удалось получить список последовательных портов для автоматического поиска: {exception.GetType().Name}.");
            return Array.Empty<string>();
        }
    }

    private void TryConnectNextAutomaticPortCandidate()
    {
        if (_hardwarePort is null) return;
        if (_nextAutomaticPortCandidateIndex >= _automaticPortCandidates.Count)
        {
            AppLogger.Debug("Application", "Автоматический поиск не нашёл подходящих последовательных портов в этом цикле.");
            return;
        }
        _activeHardwarePortName = _automaticPortCandidates[_nextAutomaticPortCandidateIndex++];
        _automaticPortAttemptInProgress = true;
        AppLogger.Debug("Application", $"Автоматический поиск пробует {_activeHardwarePortName}: candidate={_nextAutomaticPortCandidateIndex}/{_automaticPortCandidates.Count}.");
        _hardwarePort.Connect(_activeHardwarePortName, _settings.Hardware.BaudRate);
    }

    private bool IsAutomaticPortSearch() =>
        SerialPortCandidateResolver.IsAutomaticSearchPrefix(_settings.Hardware.PortName, SerialPortNameValidator.CurrentPlatform);

    private static IControllerBlockCipher? CreateControllerCipher(HardwareSettings settings)
    {
        if (!settings.ControllerAesMode.Equals("ECB", StringComparison.OrdinalIgnoreCase))
        {
            AppLogger.Warning("Application", "Настроен неподдерживаемый AES-режим; handshake останется заблокированным.");
            return null;
        }

        try
        {
            string? keyHex = System.Environment.GetEnvironmentVariable(settings.ControllerAesKeyEnvironmentVariable);
            byte[] key = string.IsNullOrWhiteSpace(keyHex)
                ? PeripheryControllerSecurity.Key.ToArray()
                : Convert.FromHexString(keyHex.Trim());
            return new Aes256EcbBlockCipher(key);
        }
        catch (FormatException)
        {
            AppLogger.Error("Application", "Ключ контроллера имеет неверный HEX-формат.");
            return null;
        }
        catch (ArgumentException)
        {
            AppLogger.Error("Application", "Ключ контроллера должен содержать 32 байта.");
            return null;
        }
    }
}
