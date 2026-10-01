using Exchanger.Core.Logging;
using Exchanger.Hardware.Abstractions;
using System;

namespace Exchanger.Hardware.PeripheryController;

public enum PeripheryDispenseMode
{
    Unsupported,
    Hopper1Auto,
    Hopper2Auto,
    DualHopperAuto,
}

/// <summary>
/// Типизированный адаптер универсального контроллера 2.3.5. Все обработчики
/// вызываются из главного потока через IHardwarePort.ProcessPendingEvents().
/// </summary>
public sealed class PeripheryMachineController : IMachineController, ITokenRecountController,
    IDualHopperMachineController, IDualHopperTokenRecountController, IDisposable
{
    private const ulong TokenRecountSilenceMilliseconds = 10_000;
    private const ulong HopperCreditTimeoutMilliseconds = 3000;
    private const ulong StartupBannerTimeoutMilliseconds = 20_000;
    private const ulong StartupDelayMilliseconds = 2_000;
    private const ulong ReadyTimeoutMilliseconds = 5_000;
    private const ulong AuxiliaryDispenseLightingDurationMilliseconds = 10_000;
    private const int AuxiliaryDispenseLightingMinimumIntervalMilliseconds = 200;
    private const int AuxiliaryDispenseLightingMaximumIntervalMilliseconds = 500;

    private readonly IHardwarePort _port;
    private readonly ControllerAuthenticator _authenticator;
    private readonly PeripheryDispenseMode _dispenseMode;
    private readonly ulong _cashlessKeepAliveMilliseconds;
    private readonly int _serviceButtonInputIndex;
    private readonly bool _serviceButtonActiveHigh;
    private readonly int _stockSensorInputIndex;
    private readonly bool _stockLowWhenInputHigh;
    private readonly int _hopper2StockSensorInputIndex;
    private readonly bool _hopper2StockLowWhenInputHigh;
    private readonly int _hopper1DispenseSensorInputIndex;
    private readonly int _hopper2DispenseSensorInputIndex;
    private readonly ulong _digitalInputPollMilliseconds;
    private readonly ulong _handshakeTimeoutMilliseconds;
    private bool _ready;
    private bool _cashActive;
    private bool _cardActive;
    private bool _cashlessEnabled;
    private int _cashBalance;
    private int _requestedCardAmount;
    private int _requestedDispenseCount;
    private int _confirmedDispenseCount;
    private int _remainingDispenseCount;
    private int _currentDispenseChunkCount;
    private int _currentDispenseChunkConfirmed;
    private int _configuredHopper1Inventory;
    private int _configuredHopper2Inventory;
    private bool _inventoryAccountingEnabled = true;
    private bool _purchaseLimitEnabled = true;
    private int _plannedHopper1Remaining;
    private int _plannedHopper2Remaining;
    private int _confirmedHopper1Count;
    private int _confirmedHopper2Count;
    private HopperChannel _activeDispenseHopper = HopperChannel.Hopper1;
    private bool _hopperErrorAwaitingCredit;
    private ulong _hopperCreditDeadlineAt = ulong.MaxValue;
    private ulong _nextCashlessKeepAliveAt;
    private ulong _nextDigitalInputPollAt = ulong.MaxValue;
    private ulong _authenticationStageDeadlineAt = ulong.MaxValue;
    private string _lastHandshakeResponseKind = "None";
    private int? _lastDigitalInputMask;
    private bool _disconnectingAfterHandshakeFailure;
    private bool _disposed;
    private bool _tokenRecountInProgress;
    private int _tokenRecountCount;
    private HopperChannel _tokenRecountHopper = HopperChannel.Hopper1;
    private ulong _lastTokenRecountPulseAt;
    private ulong _lastUpdateMilliseconds;
    private ulong _auxiliaryDispenseLightingUntilAt = ulong.MaxValue;
    private ulong _nextAuxiliaryDispenseLightingAt = ulong.MaxValue;

    public PeripheryMachineController(
        IHardwarePort port,
        ControllerAuthenticator authenticator,
        PeripheryDispenseMode dispenseMode = PeripheryDispenseMode.Unsupported,
        int cashlessKeepAliveSeconds = 120,
        int stockSensorInputIndex = -1,
        bool stockLowWhenInputHigh = false,
        int stockPollSeconds = 5,
        int handshakeTimeoutMilliseconds = 2000,
        int serviceButtonInputIndex = -1,
        bool serviceButtonActiveHigh = true,
        int hopper1DispenseSensorInputIndex = -1,
        bool hopper1DispenseSensorActiveHigh = false,
        int hopper2DispenseSensorInputIndex = -1,
        bool hopper2DispenseSensorActiveHigh = false,
        int inputPollMilliseconds = 100,
        int hopper2StockSensorInputIndex = -1,
        bool hopper2StockLowWhenInputHigh = false)
    {
        _port = port ?? throw new ArgumentNullException(nameof(port));
        _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
        _dispenseMode = dispenseMode;
        _cashlessKeepAliveMilliseconds = (ulong)Math.Clamp(cashlessKeepAliveSeconds, 30, 170) * 1000UL;
        _serviceButtonInputIndex = Math.Clamp(serviceButtonInputIndex, -1, 25);
        _serviceButtonActiveHigh = serviceButtonActiveHigh;
        _stockSensorInputIndex = Math.Clamp(stockSensorInputIndex, -1, 25);
        _stockLowWhenInputHigh = stockLowWhenInputHigh;
        _hopper2StockSensorInputIndex = Math.Clamp(hopper2StockSensorInputIndex, -1, 25);
        _hopper2StockLowWhenInputHigh = hopper2StockLowWhenInputHigh;
        _hopper1DispenseSensorInputIndex = Math.Clamp(hopper1DispenseSensorInputIndex, -1, 25);
        _hopper2DispenseSensorInputIndex = Math.Clamp(hopper2DispenseSensorInputIndex, -1, 25);
        _digitalInputPollMilliseconds = HasFastDigitalInput
            ? (ulong)Math.Clamp(inputPollMilliseconds, 50, 1000)
            : (ulong)Math.Clamp(stockPollSeconds, 1, 60) * 1000UL;
        int normalizedHandshakeTimeout = Math.Clamp(handshakeTimeoutMilliseconds, 1000, 60000);
        _handshakeTimeoutMilliseconds = (ulong)normalizedHandshakeTimeout;
        _port.OnDataReceived += OnDataReceived;
        _port.ConnectionChanged += OnConnectionChanged;
        AppLogger.Debug(
            "PeripheryController",
            $"Создан адаптер: dispenseMode={_dispenseMode}, handshakeTimeoutMs={_handshakeTimeoutMilliseconds}, "
            + $"inputPollMs={_digitalInputPollMilliseconds}, configuredInputs={HasConfiguredDigitalInput}.");
    }

    public MachineStockLevel StockLevel { get; private set; } = MachineStockLevel.Unknown;

    public MachineStockLevel Hopper1StockLevel { get; private set; } = MachineStockLevel.Unknown;

    public MachineStockLevel Hopper2StockLevel { get; private set; } = MachineStockLevel.Unknown;

    public MachineConnectionState ConnectionState { get; private set; } = MachineConnectionState.Disconnected;

    public bool IsReady => _ready;

    public bool IsTokenRecountInProgress => _tokenRecountInProgress;

    public bool CanStartTokenRecount =>
        _ready
        && _port.IsConnected
        && _dispenseMode is PeripheryDispenseMode.Hopper1Auto or PeripheryDispenseMode.DualHopperAuto
        && !_cashActive
        && !_cardActive
        && !_cashlessEnabled
        && _requestedDispenseCount == 0
        && !_tokenRecountInProgress;

    public ControllerAuthenticationState AuthenticationState => _authenticator.State;

    public event Action<MachineConnectionState>? ConnectionStateChanged;

    public event Action<MachineStockLevel>? StockChanged;

    public event Action<int>? CashBalanceChanged;

    public event Action<MachineCardResult>? CardPaymentFinished;

    public event Action<bool, int, string?>? DispenseFinished;

    public event Action<MachineFault>? FaultOccurred;

    public event Action? ServiceRequested;

    public event Action<int>? TokenRecountProgressChanged;

    public event Action<bool, int, string?>? TokenRecountFinished;

    public event Action<HopperChannel, MachineStockLevel>? HopperStockChanged;

    public event Action<int, int>? HopperDispenseAccounted;

    public event Action<int, int>? DispenseProgressChanged;

    public event Action<HopperChannel, int>? HopperTokenRecountProgressChanged;

    public event Action<HopperChannel, bool, int, string?>? HopperTokenRecountFinished;

    private int ActiveDispenseSensorInputIndex => _dispenseMode switch
    {
        PeripheryDispenseMode.Hopper1Auto => _hopper1DispenseSensorInputIndex,
        PeripheryDispenseMode.Hopper2Auto => _hopper2DispenseSensorInputIndex,
        _ => -1,
    };

    private bool HasFastDigitalInput =>
        _serviceButtonInputIndex >= 0 || ActiveDispenseSensorInputIndex >= 0;

    private bool HasConfiguredDigitalInput =>
        HasFastDigitalInput || _stockSensorInputIndex >= 0 || _hopper2StockSensorInputIndex >= 0;

    public void Update(ulong nowMilliseconds)
    {
        _lastUpdateMilliseconds = nowMilliseconds;
        UpdateHandshakeTimeout(nowMilliseconds);
        UpdateDigitalInputPolling(nowMilliseconds);
        UpdateTokenRecountTimeout(nowMilliseconds);
        UpdateHopperCreditTimeout(nowMilliseconds);
        UpdateAuxiliaryDispenseLighting(nowMilliseconds);
        if (!_ready
            || _cashActive
            || _tokenRecountInProgress
            || _requestedDispenseCount > 0)
        {
            return;
        }

        if (_nextCashlessKeepAliveAt == ulong.MaxValue)
        {
            _nextCashlessKeepAliveAt = nowMilliseconds + _cashlessKeepAliveMilliseconds;
            return;
        }

        if (nowMilliseconds < _nextCashlessKeepAliveAt)
        {
            return;
        }

        Send(PeripheryProtocolCommands.EnableCashless);
        _nextCashlessKeepAliveAt = nowMilliseconds + _cashlessKeepAliveMilliseconds;
    }

    public void BeginCashAcceptance()
    {
        if (_tokenRecountInProgress)
        {
            FaultOccurred?.Invoke(MachineFault.Unavailable);
            return;
        }

        _cashBalance = 0;
        CashBalanceChanged?.Invoke(0);
        if (!RequireOperational())
        {
            return;
        }

        _cardActive = false;
        _cashlessEnabled = false;
        _requestedCardAmount = 0;
        _nextCashlessKeepAliveAt = ulong.MaxValue;
        Send(PeripheryProtocolCommands.DisableCashless);
        Send(PeripheryProtocolCommands.EnableCoinSmartMode);
        Send(PeripheryProtocolCommands.EnableBillAcceptor);
        _cashActive = true;
    }

    public void EndCashAcceptance()
    {
        bool wasActive = _cashActive;
        _cashActive = false;
        if (_ready && wasActive)
        {
            Send(PeripheryProtocolCommands.DisableCoinAcceptor);
            Send(PeripheryProtocolCommands.DisableBillAcceptor);
            _nextCashlessKeepAliveAt = 0;
        }
    }

    public void BeginCardPayment(int amountRubles)
    {
        if (_tokenRecountInProgress)
        {
            CardPaymentFinished?.Invoke(MachineCardResult.Declined);
            return;
        }

        if (!RequireOperational())
        {
            return;
        }

        string paymentCommand;
        try
        {
            paymentCommand = PeripheryProtocolCommands.RequestCashlessPayment(amountRubles);
        }
        catch (ArgumentOutOfRangeException)
        {
            CardPaymentFinished?.Invoke(MachineCardResult.Declined);
            return;
        }

        _cashActive = false;
        _cardActive = true;
        _cashlessEnabled = true;
        _requestedCardAmount = amountRubles;
        Send(PeripheryProtocolCommands.DisableCoinAcceptor);
        Send(PeripheryProtocolCommands.DisableBillAcceptor);
        Send(PeripheryProtocolCommands.EnableCashless);
        Send(paymentCommand);

        _nextCashlessKeepAliveAt = ulong.MaxValue;
    }

    public void CancelActiveOperation()
    {
        CancelTokenRecount();
        bool cashWasActive = _cashActive;
        bool cardWasActive = _cardActive || _cashlessEnabled;
        _cashActive = false;
        _cardActive = false;
        _cashlessEnabled = false;
        _requestedCardAmount = 0;
        _nextCashlessKeepAliveAt = ulong.MaxValue;
        if (!_ready)
        {
            return;
        }

        if (cashWasActive)
        {
            Send(PeripheryProtocolCommands.DisableCoinAcceptor);
            Send(PeripheryProtocolCommands.DisableBillAcceptor);
        }

        if (cardWasActive)
        {
            Send(PeripheryProtocolCommands.DisableCashless);
        }
    }

    public void SetHopperInventory(int hopper1Count, int hopper2Count)
    {
        _configuredHopper1Inventory = Math.Max(0, hopper1Count);
        _configuredHopper2Inventory = Math.Max(0, hopper2Count);
    }

    public void SetInventoryAccountingEnabled(bool enabled) =>
        _inventoryAccountingEnabled = enabled;

    public void SetPurchaseLimitEnabled(bool enabled) =>
        _purchaseLimitEnabled = enabled;

    public void DispenseTokens(int tokenCount)
    {
        if (_tokenRecountInProgress)
        {
            DispenseFinished?.Invoke(false, 0, "TOKEN_RECOUNT_IN_PROGRESS");
            return;
        }

        if (!RequireReady())
        {
            DispenseFinished?.Invoke(false, 0, "CONTROLLER_NOT_READY");
            return;
        }

        if (_dispenseMode == PeripheryDispenseMode.Unsupported)
        {
            AppLogger.Error("PeripheryController", "Выдача заблокирована: команды хопперов не настроены.");
            DispenseFinished?.Invoke(false, 0, "DISPENSE_COMMAND_NOT_CONFIGURED");
            return;
        }

        if (_requestedDispenseCount > 0)
        {
            AppLogger.Warning("PeripheryController", "Повторная команда выдачи проигнорирована: предыдущая выдача ещё не завершена.");
            return;
        }

        if (tokenCount <= 0)
        {
            DispenseFinished?.Invoke(false, 0, "DISPENSE_COUNT_OUT_OF_RANGE");
            return;
        }

        if (!PrepareDispensePlan(tokenCount))
        {
            DispenseFinished?.Invoke(false, 0, "INSUFFICIENT_ACCOUNTED_TOKENS");
            return;
        }

        _requestedDispenseCount = tokenCount;
        _confirmedDispenseCount = 0;
        _remainingDispenseCount = tokenCount;
        _confirmedHopper1Count = 0;
        _confirmedHopper2Count = 0;
        _currentDispenseChunkCount = 0;
        _currentDispenseChunkConfirmed = 0;
        Send(PeripheryProtocolCommands.EnableDispenseLighting);
        StartAuxiliaryDispenseLighting();
        Send(PeripheryProtocolCommands.DisableCashless);
        _nextCashlessKeepAliveAt = ulong.MaxValue;
        SendNextDispenseChunk();
    }

    public bool CanStartHopperTokenRecount(HopperChannel hopper) =>
        _ready
        && _port.IsConnected
        && (_dispenseMode == PeripheryDispenseMode.DualHopperAuto
            || hopper == HopperChannel.Hopper1 && _dispenseMode == PeripheryDispenseMode.Hopper1Auto
            || hopper == HopperChannel.Hopper2 && _dispenseMode == PeripheryDispenseMode.Hopper2Auto)
        && !_cashActive
        && !_cardActive
        && !_cashlessEnabled
        && _requestedDispenseCount == 0
        && !_tokenRecountInProgress;

    public bool TryStartTokenRecount(ulong nowMilliseconds) =>
        TryStartTokenRecount(HopperChannel.Hopper1, nowMilliseconds);

    public bool TryStartTokenRecount(HopperChannel hopper, ulong nowMilliseconds)
    {
        if (!CanStartHopperTokenRecount(hopper))
        {
            return false;
        }

        _tokenRecountInProgress = true;
        _tokenRecountHopper = hopper;
        _tokenRecountCount = 0;
        _lastTokenRecountPulseAt = nowMilliseconds;
        _lastUpdateMilliseconds = nowMilliseconds;
        Send(PeripheryProtocolCommands.DisableCashless);
        _nextCashlessKeepAliveAt = ulong.MaxValue;
        Send(hopper == HopperChannel.Hopper1
            ? PeripheryProtocolCommands.EnableHopper1Motor
            : PeripheryProtocolCommands.EnableHopper2Motor);
        TokenRecountProgressChanged?.Invoke(0);
        HopperTokenRecountProgressChanged?.Invoke(hopper, 0);
        return true;
    }

    public void CancelTokenRecount()
    {
        if (!_tokenRecountInProgress)
        {
            return;
        }

        CompleteTokenRecount(false, "TOKEN_RECOUNT_CANCELLED", stopMotor: true);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CancelTokenRecount();
        if (_ready && _port.IsConnected)
        {
            Send(PeripheryProtocolCommands.DisableLighting);
            Send(PeripheryProtocolCommands.DisableAuxiliaryLighting);
        }

        _port.OnDataReceived -= OnDataReceived;
        _port.ConnectionChanged -= OnConnectionChanged;
        _disposed = true;
    }

    private void OnConnectionChanged(bool connected)
    {
        AppLogger.Debug(
            "PeripheryController",
            $"Событие транспорта: connected={connected}, state={ConnectionState}, "
            + $"authState={_authenticator.State}, ready={_ready}.");
        if (!connected)
        {
            bool handshakeFailureDisconnect = _disconnectingAfterHandshakeFailure;
            _disconnectingAfterHandshakeFailure = false;
            bool activeOperation = _cashActive || _cardActive || _requestedDispenseCount > 0 || _tokenRecountInProgress;
            bool recountWasActive = _tokenRecountInProgress;
            int recountCount = _tokenRecountCount;
            HopperChannel recountHopper = _tokenRecountHopper;
            int dispensedFromHopper1 = _confirmedHopper1Count;
            int dispensedFromHopper2 = _confirmedHopper2Count;
            ResetTokenRecountState();
            ResetAuxiliaryDispenseLighting();
            _ready = false;
            _cashActive = false;
            _cardActive = false;
            _cashlessEnabled = false;
            ResetDispenseState();
            if (dispensedFromHopper1 > 0 || dispensedFromHopper2 > 0)
            {
                HopperDispenseAccounted?.Invoke(dispensedFromHopper1, dispensedFromHopper2);
            }

            _lastDigitalInputMask = null;
            _nextDigitalInputPollAt = ulong.MaxValue;
            ResetHandshakeTiming();
            Hopper1StockLevel = MachineStockLevel.Unknown;
            Hopper2StockLevel = MachineStockLevel.Unknown;
            StockLevel = MachineStockLevel.Unknown;
            HopperStockChanged?.Invoke(HopperChannel.Hopper1, Hopper1StockLevel);
            HopperStockChanged?.Invoke(HopperChannel.Hopper2, Hopper2StockLevel);
            StockChanged?.Invoke(StockLevel);
            if (recountWasActive)
            {
                TokenRecountFinished?.Invoke(false, recountCount, "CONNECTION_LOST");
                HopperTokenRecountFinished?.Invoke(recountHopper, false, recountCount, "CONNECTION_LOST");
            }
            SetConnectionState(handshakeFailureDisconnect
                ? MachineConnectionState.Faulted
                : MachineConnectionState.Disconnected);
            AppLogger.Debug(
                "PeripheryController",
                $"Состояние после отключения сброшено: handshakeFailure={handshakeFailureDisconnect}, "
                + $"activeOperation={activeOperation}, confirmedH1={dispensedFromHopper1}, "
                + $"confirmedH2={dispensedFromHopper2}.");
            if (!handshakeFailureDisconnect)
            {
                _authenticator.Reset();
                FaultOccurred?.Invoke(activeOperation ? MachineFault.ConnectionLost : MachineFault.Unavailable);
            }
            return;
        }

        _disconnectingAfterHandshakeFailure = false;
        _lastDigitalInputMask = null;
        SetConnectionState(MachineConnectionState.Authenticating);
        if (!_authenticator.BeginStartup())
        {
            SetConnectionState(MachineConnectionState.Faulted);
            AppLogger.Error("PeripheryController", "Авторизация не настроена: отсутствует AES-ключ или выбран неподдерживаемый режим.");
            FaultOccurred?.Invoke(MachineFault.Unavailable);
            return;
        }

        ResetHandshakeTiming();
        _lastHandshakeResponseKind = "None";
        AppLogger.Debug(
            "ControllerAuth",
            $"Авторизация запущена: state={_authenticator.State}; ожидание startup-banner до {StartupBannerTimeoutMilliseconds} мс.");
    }

    private void OnDataReceived(string frame)
    {
        PeripheryMessage message = PeripheryProtocolParser.Parse(frame);
        if (message.Kind is PeripheryMessageKind.StartupBanner
            or PeripheryMessageKind.EncryptedResponse
            or PeripheryMessageKind.Challenge
            or PeripheryMessageKind.HostAuthorizationAccepted
            or PeripheryMessageKind.AuthorizationConfirmed
            or PeripheryMessageKind.Ready)
        {
            _lastHandshakeResponseKind = message.Kind.ToString();
            ControllerAuthenticationState previousState = _authenticator.State;
            AppLogger.Debug(
                "ControllerAuth",
                $"Получен этап {message.Kind}: frameLength={frame.Length}, stateBefore={previousState}; payload скрыт.");
            string? response = _authenticator.Handle(message);
            if (response is not null)
            {
                AppLogger.Debug(
                    "ControllerAuth",
                    $"Сформирован ответ на этап {message.Kind}: responseLength={response.Length}; payload скрыт.");
                Send(response);
            }

            if (_authenticator.State != previousState)
            {
                _authenticationStageDeadlineAt = ulong.MaxValue;
                AppLogger.Debug(
                    "ControllerAuth",
                    $"Переход авторизации: {previousState} -> {_authenticator.State}.");
            }

            if (_authenticator.State == ControllerAuthenticationState.Ready && !_ready)
            {
                CompleteInitialization();
            }
            else if (_authenticator.State == ControllerAuthenticationState.Failed)
            {
                FailHandshake("Контроллер не прошёл криптографическую проверку.");
            }

            return;
        }

        if (!_ready)
        {
            if (message.Kind == PeripheryMessageKind.TransportError)
            {
                FaultOccurred?.Invoke(MachineFault.Unavailable);
            }
            else
            {
                AppLogger.Warning("PeripheryController", "Неавторизованный протокольный кадр проигнорирован; содержимое скрыто.");
            }

            return;
        }

        switch (message.Kind)
        {
            case PeripheryMessageKind.MoneyCoins:
                Send(PeripheryProtocolCommands.AcknowledgeCoins(message.NumericValue));
                AcceptCash(message.NumericValue);
                break;
            case PeripheryMessageKind.MoneyBill:
                Send(PeripheryProtocolCommands.AcknowledgeBill(message.NumericValue));
                AcceptCash(message.NumericValue);
                break;
            case PeripheryMessageKind.MoneyCashless:
                Send(PeripheryProtocolCommands.AcknowledgeCashless(message.NumericValue));
                AcceptCashless(message.NumericValue);
                break;
            case PeripheryMessageKind.CoinPulse:
                Send(PeripheryProtocolCommands.AcknowledgeCoinPulse());
                AppLogger.Warning("PeripheryController", "Получен COIN без номинала; событие подтверждено, но не зачислено. Ожидался smart-режим.");
                break;
            case PeripheryMessageKind.Hopper1Ok:
                if (_activeDispenseHopper == HopperChannel.Hopper1
                    && !_tokenRecountInProgress
                    && !_hopperErrorAwaitingCredit)
                {
                    ConfirmHopperPulse();
                }
                break;
            case PeripheryMessageKind.Hopper1Error:
                if (_activeDispenseHopper == HopperChannel.Hopper1)
                {
                    BeginHopperFailure();
                }
                break;
            case PeripheryMessageKind.Hopper1Append:
                break;
            case PeripheryMessageKind.Hopper1Credit:
                if (_activeDispenseHopper == HopperChannel.Hopper1)
                {
                    CompleteHopperFailureFromCredit(message.NumericValue);
                }
                break;
            case PeripheryMessageKind.Hopper1Pulse:
                if (_tokenRecountInProgress && _tokenRecountHopper == HopperChannel.Hopper1)
                {
                    CountTokenRecountPulse();
                }
                break;
            case PeripheryMessageKind.Hopper2Ok:
                if (_activeDispenseHopper == HopperChannel.Hopper2
                    && !_tokenRecountInProgress
                    && !_hopperErrorAwaitingCredit)
                {
                    ConfirmHopperPulse();
                }
                break;
            case PeripheryMessageKind.Hopper2Queued:
            case PeripheryMessageKind.Hopper2Append:
                break;
            case PeripheryMessageKind.Hopper2Error:
                if (_activeDispenseHopper == HopperChannel.Hopper2)
                {
                    BeginHopperFailure();
                }
                break;
            case PeripheryMessageKind.Hopper2Credit:
                if (_activeDispenseHopper == HopperChannel.Hopper2)
                {
                    CompleteHopperFailureFromCredit(message.NumericValue);
                }
                break;
            case PeripheryMessageKind.Hopper2Pulse:
                if (_tokenRecountInProgress && _tokenRecountHopper == HopperChannel.Hopper2)
                {
                    CountTokenRecountPulse();
                }
                break;
            case PeripheryMessageKind.Service:
                if (_serviceButtonInputIndex < 0)
                {
                    ServiceRequested?.Invoke();
                }
                break;
            case PeripheryMessageKind.DigitalInputs:
                ApplyDigitalInputs(message.NumericValue);
                break;
            case PeripheryMessageKind.ControllerError:
            case PeripheryMessageKind.TransportError:
                FaultOccurred?.Invoke(MachineFault.Unavailable);
                break;
            case PeripheryMessageKind.Unknown:
                AppLogger.Warning("PeripheryController", "Получен неизвестный или developer-кадр; содержимое скрыто.");
                break;
        }
    }

    private void CompleteInitialization()
    {
        _ready = true;
        SetConnectionState(MachineConnectionState.Ready);
        ResetHandshakeTiming();
        Send(PeripheryProtocolCommands.EnableCoinSmartMode);
        Send(PeripheryProtocolCommands.DisableCoinAcceptor);
        Send(PeripheryProtocolCommands.DisableBillAcceptor);
        Send(PeripheryProtocolCommands.DisableCashless);
        Send(PeripheryProtocolCommands.EnableDefaultLighting);
        Send(PeripheryProtocolCommands.EnableAuxiliaryDefaultLighting);
        Hopper1StockLevel = MachineStockLevel.Unknown;
        Hopper2StockLevel = MachineStockLevel.Unknown;
        StockLevel = MachineStockLevel.Unknown;
        HopperStockChanged?.Invoke(HopperChannel.Hopper1, Hopper1StockLevel);
        HopperStockChanged?.Invoke(HopperChannel.Hopper2, Hopper2StockLevel);
        StockChanged?.Invoke(StockLevel);
        if (HasConfiguredDigitalInput)
        {
            Send(PeripheryProtocolCommands.QueryDigitalInputs);
            _nextDigitalInputPollAt = ulong.MaxValue;
        }
        AppLogger.Info("PeripheryController", "Контроллер авторизован и переведён в безопасное исходное состояние.");
        AppLogger.Debug(
            "ControllerAuth",
            $"Инициализация завершена: state={_authenticator.State}, digitalInputPolling={HasConfiguredDigitalInput}, "
            + $"connectionState={ConnectionState}.");
    }

    private void StartAuxiliaryDispenseLighting()
    {
        _auxiliaryDispenseLightingUntilAt = _lastUpdateMilliseconds + AuxiliaryDispenseLightingDurationMilliseconds;
        Send(PeripheryProtocolCommands.EnableAuxiliaryDispenseLighting);
        _nextAuxiliaryDispenseLightingAt = _lastUpdateMilliseconds + NextAuxiliaryDispenseLightingInterval();
    }

    private void UpdateAuxiliaryDispenseLighting(ulong nowMilliseconds)
    {
        if (_auxiliaryDispenseLightingUntilAt == ulong.MaxValue
            || !_ready
            || !_port.IsConnected)
        {
            return;
        }

        if (nowMilliseconds >= _auxiliaryDispenseLightingUntilAt)
        {
            ResetAuxiliaryDispenseLighting();
            Send(PeripheryProtocolCommands.EnableAuxiliaryDefaultLighting);
            return;
        }

        if (nowMilliseconds < _nextAuxiliaryDispenseLightingAt)
        {
            return;
        }

        Send(PeripheryProtocolCommands.EnableAuxiliaryDispenseLighting);
        _nextAuxiliaryDispenseLightingAt = nowMilliseconds + NextAuxiliaryDispenseLightingInterval();
    }

    private static ulong NextAuxiliaryDispenseLightingInterval() =>
        (ulong)Random.Shared.Next(
            AuxiliaryDispenseLightingMinimumIntervalMilliseconds,
            AuxiliaryDispenseLightingMaximumIntervalMilliseconds + 1);

    private void ResetAuxiliaryDispenseLighting()
    {
        _auxiliaryDispenseLightingUntilAt = ulong.MaxValue;
        _nextAuxiliaryDispenseLightingAt = ulong.MaxValue;
    }

    private void UpdateHandshakeTimeout(ulong nowMilliseconds)
    {
        ControllerAuthenticationState state = _authenticator.State;
        if (_ready
            || state is ControllerAuthenticationState.NotStarted
                or ControllerAuthenticationState.ConfigurationMissing
                or ControllerAuthenticationState.Ready
                or ControllerAuthenticationState.Failed)
        {
            return;
        }

        if (_authenticationStageDeadlineAt == ulong.MaxValue)
        {
            ulong timeout = state switch
            {
                ControllerAuthenticationState.AwaitingStartupBanner => StartupBannerTimeoutMilliseconds,
                ControllerAuthenticationState.StartupDelay => StartupDelayMilliseconds,
                ControllerAuthenticationState.AwaitingReady => ReadyTimeoutMilliseconds,
                _ => _handshakeTimeoutMilliseconds,
            };
            _authenticationStageDeadlineAt = nowMilliseconds + timeout;
            AppLogger.Debug(
                "ControllerAuth",
                $"Установлен таймаут этапа: state={state}, timeoutMs={timeout}.");
            return;
        }

        if (nowMilliseconds < _authenticationStageDeadlineAt)
        {
            return;
        }

        if (state == ControllerAuthenticationState.StartupDelay)
        {
            string? command = _authenticator.StartHandshake();
            if (command is null)
            {
                FailHandshake("Не удалось начать криптографическую авторизацию после стартовой паузы.");
                return;
            }

            _authenticationStageDeadlineAt = ulong.MaxValue;
            AppLogger.Debug(
                "ControllerAuth",
                "Стартовая пауза завершена; сформирован host challenge длиной 16 байт, содержимое скрыто.");
            Send(command);
            return;
        }

        string reason = state switch
        {
            ControllerAuthenticationState.AwaitingStartupBanner =>
                "Контроллер не прислал загрузочную строку в пределах таймаута.",
            ControllerAuthenticationState.AwaitingChallenge =>
                "Контроллер не прислал CHAL в пределах таймаута.",
            ControllerAuthenticationState.AwaitingControllerProof =>
                "Контроллер не прислал RAES в пределах таймаута.",
            ControllerAuthenticationState.AwaitingReady =>
                "Контроллер не прислал точную строку READY! после RAES.",
            _ => "Истёк таймаут авторизации контроллера.",
        };
        FailHandshake(reason);
    }

    private void FailHandshake(string reason)
    {
        if (_disconnectingAfterHandshakeFailure)
        {
            return;
        }

        if (_authenticator.State != ControllerAuthenticationState.Failed)
        {
            _authenticator.Fail();
        }

        ResetHandshakeTiming();
        SetConnectionState(MachineConnectionState.Faulted);
        AppLogger.Error(
            "PeripheryController",
            $"{reason} Последний тип ответа платы: {_lastHandshakeResponseKind}.");
        AppLogger.Debug(
            "ControllerAuth",
            $"Авторизация аварийно завершена: state={_authenticator.State}, lastKind={_lastHandshakeResponseKind}.");
        FaultOccurred?.Invoke(MachineFault.Unavailable);
        _disconnectingAfterHandshakeFailure = true;
        _port.Disconnect();
    }

    private void ResetHandshakeTiming()
    {
        _authenticationStageDeadlineAt = ulong.MaxValue;
    }

    private void AcceptCash(int amountRubles)
    {
        if (!_cashActive)
        {
            AppLogger.Warning("PeripheryController", "Платёж подтверждён контроллеру, но проигнорирован вне cash-сессии.");
            return;
        }

        _cashBalance = (int)Math.Min(int.MaxValue, (long)_cashBalance + amountRubles);
        CashBalanceChanged?.Invoke(_cashBalance);
    }

    private void AcceptCashless(int amountRubles)
    {
        if (!_cardActive)
        {
            AppLogger.Warning("PeripheryController", "Безналичный платёж подтверждён контроллеру, но проигнорирован вне card-сессии.");
            return;
        }

        _cardActive = false;
        _cashlessEnabled = false;
        Send(PeripheryProtocolCommands.DisableCashless);
        CardPaymentFinished?.Invoke(amountRubles == _requestedCardAmount
            ? MachineCardResult.Approved
            : MachineCardResult.Declined);
    }

    private bool PrepareDispensePlan(int tokenCount)
    {
        if (_dispenseMode == PeripheryDispenseMode.Hopper1Auto)
        {
            _plannedHopper1Remaining = tokenCount;
            _plannedHopper2Remaining = 0;
            return true;
        }

        if (_dispenseMode == PeripheryDispenseMode.Hopper2Auto)
        {
            _plannedHopper1Remaining = 0;
            _plannedHopper2Remaining = tokenCount;
            return true;
        }

        if (!_inventoryAccountingEnabled)
        {
            _plannedHopper1Remaining = tokenCount;
            _plannedHopper2Remaining = 0;
            return true;
        }

        if (!_purchaseLimitEnabled)
        {
            _plannedHopper1Remaining = Math.Min(tokenCount, _configuredHopper1Inventory);
            _plannedHopper2Remaining = tokenCount - _plannedHopper1Remaining;
            return true;
        }

        if (!DualHopperDispensePlanner.TryCreate(
                tokenCount,
                _configuredHopper1Inventory,
                _configuredHopper2Inventory,
                out DualHopperDispensePlan plan))
        {
            return false;
        }

        _plannedHopper1Remaining = plan.Hopper1Count;
        _plannedHopper2Remaining = plan.Hopper2Count;

        return true;
    }

    private void ConfirmHopperPulse()
    {
        if (_requestedDispenseCount <= 0
            || _currentDispenseChunkCount <= 0
            || _hopperErrorAwaitingCredit)
        {
            return;
        }

        _currentDispenseChunkConfirmed++;
        _confirmedDispenseCount++;
        AddConfirmedForActiveHopper(1);
        DispenseProgressChanged?.Invoke(_confirmedDispenseCount, _requestedDispenseCount);
        if (_currentDispenseChunkConfirmed < _currentDispenseChunkCount)
        {
            return;
        }

        if (_remainingDispenseCount > 0)
        {
            SendNextDispenseChunk();
            return;
        }

        FinishDispense(true, _confirmedDispenseCount, null);
    }

    private void BeginHopperFailure()
    {
        if (_requestedDispenseCount <= 0 || _hopperErrorAwaitingCredit)
        {
            return;
        }

        _hopperErrorAwaitingCredit = true;
        _hopperCreditDeadlineAt = _lastUpdateMilliseconds + HopperCreditTimeoutMilliseconds;
    }

    private void CompleteHopperFailureFromCredit(int remainingCredit)
    {
        if (!_hopperErrorAwaitingCredit || _requestedDispenseCount <= 0)
        {
            return;
        }

        int currentChunkDispensed = Math.Clamp(
            _currentDispenseChunkCount - remainingCredit,
            0,
            _currentDispenseChunkCount);
        currentChunkDispensed = Math.Max(currentChunkDispensed, _currentDispenseChunkConfirmed);
        int additionalConfirmed = currentChunkDispensed - _currentDispenseChunkConfirmed;
        if (additionalConfirmed > 0)
        {
            _confirmedDispenseCount += additionalConfirmed;
            AddConfirmedForActiveHopper(additionalConfirmed);
        }

        string error = _activeDispenseHopper == HopperChannel.Hopper1
            ? "HOPPER1_ERROR"
            : "HOPPER2_ERROR";
        FinishDispense(false, _confirmedDispenseCount, error);
    }

    private void SendNextDispenseChunk()
    {
        int available = _plannedHopper1Remaining > 0
            ? _plannedHopper1Remaining
            : _plannedHopper2Remaining;
        _activeDispenseHopper = _plannedHopper1Remaining > 0
            ? HopperChannel.Hopper1
            : HopperChannel.Hopper2;

        int chunkCount = Math.Min(
            PeripheryProtocolCommands.MaxHopperDispenseBatch,
            available);
        if (_activeDispenseHopper == HopperChannel.Hopper1)
        {
            _plannedHopper1Remaining -= chunkCount;
        }
        else
        {
            _plannedHopper2Remaining -= chunkCount;
        }

        _remainingDispenseCount -= chunkCount;
        _currentDispenseChunkCount = chunkCount;
        _currentDispenseChunkConfirmed = 0;
        Send(_activeDispenseHopper == HopperChannel.Hopper1
            ? PeripheryProtocolCommands.DispenseFromHopper1(chunkCount)
            : PeripheryProtocolCommands.DispenseFromHopper2(chunkCount));
    }

    private void AddConfirmedForActiveHopper(int count)
    {
        if (_activeDispenseHopper == HopperChannel.Hopper1)
        {
            _confirmedHopper1Count += count;
        }
        else
        {
            _confirmedHopper2Count += count;
        }
    }

    private void FinishDispense(bool success, int dispensed, string? error)
    {
        int hopper1 = _confirmedHopper1Count;
        int hopper2 = _confirmedHopper2Count;
        ResetDispenseState();
        if (hopper1 > 0 || hopper2 > 0)
        {
            HopperDispenseAccounted?.Invoke(hopper1, hopper2);
        }

        DispenseFinished?.Invoke(success, dispensed, error);
    }

    private void ResetDispenseState()
    {
        _requestedDispenseCount = 0;
        _confirmedDispenseCount = 0;
        _remainingDispenseCount = 0;
        _plannedHopper1Remaining = 0;
        _plannedHopper2Remaining = 0;
        _confirmedHopper1Count = 0;
        _confirmedHopper2Count = 0;
        _currentDispenseChunkCount = 0;
        _currentDispenseChunkConfirmed = 0;
        _hopperErrorAwaitingCredit = false;
        _hopperCreditDeadlineAt = ulong.MaxValue;
        if (_ready)
        {
            _nextCashlessKeepAliveAt = 0;
        }
    }

    private void UpdateDigitalInputPolling(ulong nowMilliseconds)
    {
        if (!_ready || !HasConfiguredDigitalInput)
        {
            return;
        }

        if (_nextDigitalInputPollAt == ulong.MaxValue)
        {
            _nextDigitalInputPollAt = nowMilliseconds + _digitalInputPollMilliseconds;
            return;
        }

        if (nowMilliseconds < _nextDigitalInputPollAt)
        {
            return;
        }

        Send(PeripheryProtocolCommands.QueryDigitalInputs);
        _nextDigitalInputPollAt = nowMilliseconds + _digitalInputPollMilliseconds;
    }

    private void ApplyDigitalInputs(int inputMask)
    {
        int? previousMask = _lastDigitalInputMask;
        bool serviceActivated = _serviceButtonInputIndex >= 0
            && previousMask.HasValue
            && IsInputActive(inputMask, _serviceButtonInputIndex, _serviceButtonActiveHigh)
            && !IsInputActive(previousMask.Value, _serviceButtonInputIndex, _serviceButtonActiveHigh);
        _lastDigitalInputMask = inputMask;

        if (_stockSensorInputIndex >= 0)
        {
            bool inputHigh = IsInputHigh(inputMask, _stockSensorInputIndex);
            bool isLow = inputHigh == _stockLowWhenInputHigh;
            SetHopperStockLevel(
                HopperChannel.Hopper1,
                isLow ? MachineStockLevel.Low : MachineStockLevel.Enough);
        }

        if (_hopper2StockSensorInputIndex >= 0)
        {
            bool inputHigh = IsInputHigh(inputMask, _hopper2StockSensorInputIndex);
            bool isLow = inputHigh == _hopper2StockLowWhenInputHigh;
            SetHopperStockLevel(
                HopperChannel.Hopper2,
                isLow ? MachineStockLevel.Low : MachineStockLevel.Enough);
        }

        if (serviceActivated)
        {
            ServiceRequested?.Invoke();
        }

        // IN24 остаётся документированным физическим датчиком первого хоппера,
        // но новая прошивка сама передаёт надёжные HOPPER1 PULSE=1 при прямом моторе.
        // Поэтому INPUT-фронты не участвуют ни в выдаче, ни в пересчёте и не дублируют строки платы.
    }

    private void SetHopperStockLevel(HopperChannel hopper, MachineStockLevel level)
    {
        MachineStockLevel current = hopper == HopperChannel.Hopper1
            ? Hopper1StockLevel
            : Hopper2StockLevel;
        if (current == level)
        {
            return;
        }

        if (hopper == HopperChannel.Hopper1)
        {
            Hopper1StockLevel = level;
        }
        else
        {
            Hopper2StockLevel = level;
        }

        HopperStockChanged?.Invoke(hopper, level);
        MachineStockLevel aggregate = _hopper2StockSensorInputIndex < 0
            ? Hopper1StockLevel
            : _stockSensorInputIndex < 0
                ? Hopper2StockLevel
                : Hopper1StockLevel == MachineStockLevel.Enough
                  || Hopper2StockLevel == MachineStockLevel.Enough
                    ? MachineStockLevel.Enough
                    : Hopper1StockLevel == MachineStockLevel.Low
                      && Hopper2StockLevel == MachineStockLevel.Low
                        ? MachineStockLevel.Low
                        : MachineStockLevel.Unknown;
        if (aggregate != StockLevel)
        {
            StockLevel = aggregate;
            StockChanged?.Invoke(aggregate);
        }
    }

    private void UpdateTokenRecountTimeout(ulong nowMilliseconds)
    {
        if (!_tokenRecountInProgress
            || nowMilliseconds < _lastTokenRecountPulseAt
            || nowMilliseconds - _lastTokenRecountPulseAt < TokenRecountSilenceMilliseconds)
        {
            return;
        }

        CompleteTokenRecount(true, null, stopMotor: true);
    }

    private void CountTokenRecountPulse()
    {
        _tokenRecountCount++;
        _lastTokenRecountPulseAt = _lastUpdateMilliseconds;
        TokenRecountProgressChanged?.Invoke(_tokenRecountCount);
        HopperTokenRecountProgressChanged?.Invoke(_tokenRecountHopper, _tokenRecountCount);
    }

    private void UpdateHopperCreditTimeout(ulong nowMilliseconds)
    {
        if (!_hopperErrorAwaitingCredit
            || nowMilliseconds < _hopperCreditDeadlineAt)
        {
            return;
        }

        int dispensed = _confirmedDispenseCount;
        string error = _activeDispenseHopper == HopperChannel.Hopper1
            ? "HOPPER1_CREDIT_TIMEOUT"
            : "HOPPER2_CREDIT_TIMEOUT";
        FinishDispense(false, dispensed, error);
    }

    private void CompleteTokenRecount(bool success, string? errorCode, bool stopMotor)
    {
        if (!_tokenRecountInProgress)
        {
            return;
        }

        int counted = _tokenRecountCount;
        HopperChannel hopper = _tokenRecountHopper;
        ResetTokenRecountState();
        if (stopMotor && _ready && _port.IsConnected)
        {
            Send(hopper == HopperChannel.Hopper1
                ? PeripheryProtocolCommands.DisableHopper1Motor
                : PeripheryProtocolCommands.DisableHopper2Motor);
            _nextCashlessKeepAliveAt = 0;
        }

        TokenRecountFinished?.Invoke(success, counted, errorCode);
        HopperTokenRecountFinished?.Invoke(hopper, success, counted, errorCode);
    }

    private void ResetTokenRecountState()
    {
        _tokenRecountInProgress = false;
        _tokenRecountCount = 0;
        _lastTokenRecountPulseAt = 0;
    }

    private static bool IsInputActive(int inputMask, int inputIndex, bool activeHigh) =>
        IsInputHigh(inputMask, inputIndex) == activeHigh;

    private static bool IsInputHigh(int inputMask, int inputIndex) =>
        (inputMask & (1 << inputIndex)) != 0;

    private bool RequireReady()
    {
        if (_ready && _port.IsConnected)
        {
            return true;
        }

        FaultOccurred?.Invoke(MachineFault.Unavailable);
        return false;
    }

    private bool RequireOperational()
    {
        if (!RequireReady())
        {
            return false;
        }

        if (_dispenseMode != PeripheryDispenseMode.Unsupported)
        {
            return true;
        }

        AppLogger.Error("PeripheryController", "Приём оплаты заблокирован: безопасная команда выдачи не настроена.");
        FaultOccurred?.Invoke(MachineFault.Unavailable);
        return false;
    }

    private void Send(string command)
    {
        try
        {
            _port.SendCommand(command);
        }
        catch (InvalidOperationException)
        {
            AppLogger.Debug(
                "PeripheryController",
                $"Команда не поставлена в транспорт: connected={_port.IsConnected}, state={ConnectionState}, "
                + $"authState={_authenticator.State}.");
            SetConnectionState(MachineConnectionState.Disconnected);
            FaultOccurred?.Invoke(MachineFault.ConnectionLost);
        }
    }

    private void SetConnectionState(MachineConnectionState state)
    {
        if (ConnectionState == state)
        {
            return;
        }

        MachineConnectionState previous = ConnectionState;
        ConnectionState = state;
        AppLogger.Debug(
            "PeripheryController",
            $"Переход соединения: {previous} -> {state}; authState={_authenticator.State}, transportConnected={_port.IsConnected}.");
        ConnectionStateChanged?.Invoke(state);
    }
}
