using Exchanger.Core.Configuration;
using Exchanger.Hardware.Abstractions;
using System;

namespace Exchanger.Core.Session;

/// <summary>
/// Чистое, не зависящее от Godot ядро пользовательской операции.
/// Внутренний OperationId предназначен только для локальной диагностики и
/// не является идентификатором будущего протокола контроллера.
/// </summary>
public sealed class SessionFlow : IDisposable
{
    private readonly AppSettings _settings;
    private readonly IMachineController _machine;
    private readonly IMonotonicClock _clock;
    private readonly ISessionIdGenerator _idGenerator;
    private int? _maximumAvailableTokens;
    private ulong _deadlineMs;
    private int _lastCountdown = -1;
    private bool _operationFinished;
    private bool _disposed;

    public SessionFlow(
        AppSettings settings,
        IMachineController machine,
        IMonotonicClock clock,
        ISessionIdGenerator? idGenerator = null,
        int? maximumAvailableTokens = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _machine = machine ?? throw new ArgumentNullException(nameof(machine));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _idGenerator = idGenerator ?? new GuidSessionIdGenerator();
        SetMaximumAvailableTokens(maximumAvailableTokens);

        _machine.StockChanged += OnStockChanged;
        _machine.CashBalanceChanged += OnCashBalanceChanged;
        _machine.CardPaymentFinished += OnCardPaymentFinished;
        _machine.DispenseFinished += OnDispenseFinished;
        _machine.FaultOccurred += OnMachineFault;
        StockLevel = machine.StockLevel;
    }

    public SessionState State { get; private set; } = SessionState.Home;

    public PaymentMethod PaymentMethod { get; private set; } = PaymentMethod.None;

    public string CurrentOperationId { get; private set; } = string.Empty;

    public int SelectedAmountRubles { get; private set; }

    public int CashBalanceRubles { get; private set; }

    public int BaseTokens { get; private set; }

    public int BonusTokens { get; private set; }

    public int TotalTokens => BaseTokens + BonusTokens;

    public MachineStockLevel StockLevel { get; private set; }

    public SessionErrorCode ErrorCode { get; private set; }

    public SessionErrorAction ErrorAction { get; private set; } = SessionErrorAction.ReturnHome;

    public string ErrorTitle { get; private set; } = string.Empty;

    public string ErrorMessage { get; private set; } = string.Empty;

    public event Action<SessionState>? StateChanged;

    public event Action<int>? CashBalanceChanged;

    public event Action<MachineStockLevel>? StockChanged;

    public event Action<int>? CountdownChanged;

    public event Action<string>? ActionRejected;

    public event Action<SessionOperationSnapshot>? OperationStarted;

    public event Action<SessionOperationSnapshot>? OperationFinished;

    public void Update()
    {
        if (_disposed || _deadlineMs == 0)
        {
            return;
        }

        ulong now = _clock.NowMilliseconds;
        int remaining = now >= _deadlineMs
            ? 0
            : (int)Math.Ceiling((_deadlineMs - now) / 1000d);

        if (remaining != _lastCountdown)
        {
            _lastCountdown = remaining;
            CountdownChanged?.Invoke(remaining);
        }

        if (now >= _deadlineMs)
        {
            _deadlineMs = 0;
            HandleTimeout();
        }
    }

    public bool BeginCash()
    {
        if (!RequireState(SessionState.Home))
        {
            return false;
        }

        if (!_settings.Payments.CashEnabled)
        {
            return RejectAction("Оплата наличными отключена в настройках.");
        }

        if (!CanStartPayment())
        {
            return false;
        }

        BeginOperation(PaymentMethod.Cash);
        SetState(SessionState.CashAccepting);
        SetDeadline(_settings.Timeouts.CashPaymentSeconds);
        _machine.BeginCashAcceptance();
        return true;
    }

    public bool BeginCard()
    {
        if (!RequireState(SessionState.Home))
        {
            return false;
        }

        if (!_settings.Payments.CardEnabled)
        {
            return RejectAction("Оплата картой отключена в настройках.");
        }

        if (!CanStartPayment())
        {
            return false;
        }

        BeginOperation(PaymentMethod.Card);
        SetState(SessionState.CardAmountSelection);
        SetDeadline(_settings.Timeouts.SessionIdleSeconds);
        return true;
    }

    public bool OpenCustomCardAmount()
    {
        if (!RequireState(SessionState.CardAmountSelection))
        {
            return false;
        }

        SetState(SessionState.CardCustomAmount);
        SetDeadline(_settings.Timeouts.SessionIdleSeconds);
        return true;
    }

    public bool ReturnToCardAmounts()
    {
        if (State == SessionState.CardTerminalWait)
        {
            _machine.CancelActiveOperation();
        }
        else if (State != SessionState.CardCustomAmount)
        {
            return RejectAction(
                $"Возврат к выбору суммы недоступен в состоянии {State}.");
        }

        SetState(SessionState.CardAmountSelection);
        SetDeadline(_settings.Timeouts.SessionIdleSeconds);
        return true;
    }

    public bool SelectCardAmount(int amountRubles)
    {
        if (State is not SessionState.CardAmountSelection and not SessionState.CardCustomAmount)
        {
            return RejectAction($"Выбор суммы недоступен в состоянии {State}.");
        }

        if (!IsAmountValid(amountRubles))
        {
            return RejectAction($"Сумма {amountRubles} ₽ не соответствует ограничениям конфигурации.");
        }

        SelectedAmountRubles = amountRubles;
        CalculateTokens(amountRubles);
        if (!EnsureTokenInventoryLimit())
        {
            return false;
        }

        if (!EnsureStockBeforePayment())
        {
            return false;
        }

        SetState(SessionState.CardTerminalWait);
        SetDeadline(_settings.Timeouts.CashlessPaymentSeconds);
        _machine.BeginCardPayment(amountRubles);
        return true;
    }

    public bool ConfirmCashDispense()
    {
        if (!RequireState(SessionState.CashAccepting))
        {
            return false;
        }

        if (CashBalanceRubles < _settings.Pricing.TokenPriceRubles)
        {
            return RejectAction("Недостаточный наличный баланс для одного жетона.");
        }

        SelectedAmountRubles = CashBalanceRubles;
        CalculateTokens(SelectedAmountRubles);
        if (!EnsureTokenInventoryLimit())
        {
            _machine.EndCashAcceptance();
            return false;
        }

        _machine.EndCashAcceptance();
        BeginDispense();
        return State == SessionState.Dispensing;
    }

    public void NotifyUserActivity()
    {
        switch (State)
        {
            case SessionState.CashAccepting:
                SetDeadline(_settings.Timeouts.CashPaymentSeconds);
                break;
            case SessionState.CardAmountSelection:
            case SessionState.CardCustomAmount:
                SetDeadline(_settings.Timeouts.SessionIdleSeconds);
                break;
        }
    }

    public void Cancel() => CancelInternal(OperationOutcome.Cancelled);

    public void ReturnHome()
    {
        if (State == SessionState.Dispensing)
        {
            return;
        }

        ResetToHome();
    }

    public bool IsAmountValid(int amountRubles) => PricingPolicy.IsAmountValid(_settings.Pricing, amountRubles);

    public void SetMaximumAvailableTokens(int? maximumAvailableTokens) =>
        _maximumAvailableTokens = maximumAvailableTokens is null
            ? null
            : Math.Max(0, maximumAvailableTokens.Value);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CompleteOperation(OperationOutcome.Interrupted);
        _machine.StockChanged -= OnStockChanged;
        _machine.CashBalanceChanged -= OnCashBalanceChanged;
        _machine.CardPaymentFinished -= OnCardPaymentFinished;
        _machine.DispenseFinished -= OnDispenseFinished;
        _machine.FaultOccurred -= OnMachineFault;
        _disposed = true;
    }

    private void BeginOperation(PaymentMethod paymentMethod)
    {
        ResetOperationData();
        PaymentMethod = paymentMethod;
        CurrentOperationId = _idGenerator.CreateId();
        if (string.IsNullOrWhiteSpace(CurrentOperationId))
        {
            throw new InvalidOperationException("Генератор вернул пустой идентификатор операции.");
        }

        _operationFinished = false;
        OperationStarted?.Invoke(CreateSnapshot(OperationOutcome.InProgress));
    }

    private bool EnsureStockBeforePayment()
    {
        if (StockLevel == MachineStockLevel.Unknown)
        {
            SetError(SessionErrorCode.StockStatusUnknown);
            return false;
        }

        return true;
    }

    private bool CanStartPayment()
    {
        return StockLevel switch
        {
            MachineStockLevel.Unknown => RejectAction("Состояние запаса жетонов ещё не подтверждено."),
            _ => true,
        };
    }

    private void BeginDispense()
    {
        if (TotalTokens <= 0)
        {
            SetError(SessionErrorCode.InvalidTokenCalculation);
            return;
        }

        if (!EnsureStockBeforePayment())
        {
            return;
        }

        if (!EnsureTokenInventoryLimit())
        {
            return;
        }

        SetState(SessionState.Dispensing);
        ClearDeadline();
        _machine.DispenseTokens(TotalTokens);
    }

    private void CalculateTokens(int amountRubles)
    {
        TokenCalculation result = PricingPolicy.Calculate(_settings.Pricing, amountRubles);
        BaseTokens = result.BaseTokens;
        BonusTokens = result.BonusTokens;
    }

    private bool EnsureTokenInventoryLimit()
    {
        if (_maximumAvailableTokens is null || TotalTokens <= _maximumAvailableTokens.Value)
        {
            return true;
        }

        SetError(SessionErrorCode.InsufficientTokenInventory);
        return false;
    }

    private void OnStockChanged(MachineStockLevel stockLevel)
    {
        StockLevel = stockLevel;
        StockChanged?.Invoke(StockLevel);
    }

    private void OnCashBalanceChanged(int balanceRubles)
    {
        if (State != SessionState.CashAccepting)
        {
            return;
        }

        CashBalanceRubles = Math.Max(0, balanceRubles);
        CashBalanceChanged?.Invoke(CashBalanceRubles);
        SetDeadline(_settings.Timeouts.CashPaymentSeconds);
    }

    private void OnCardPaymentFinished(MachineCardResult result)
    {
        if (State != SessionState.CardTerminalWait)
        {
            return;
        }

        switch (result)
        {
            case MachineCardResult.Approved:
                BeginDispense();
                break;
            case MachineCardResult.Declined:
                SetError(SessionErrorCode.PaymentDeclined, SessionState.PaymentDeclined);
                break;
            case MachineCardResult.Cancelled:
                CancelInternal(OperationOutcome.Cancelled);
                break;
            case MachineCardResult.Timeout:
                SetError(SessionErrorCode.PaymentTimeout, SessionState.PaymentDeclined);
                break;
        }
    }

    private void OnDispenseFinished(bool success, int dispensedTokens, string? error)
    {
        if (State != SessionState.Dispensing)
        {
            return;
        }

        if (!success || dispensedTokens != TotalTokens)
        {
            SessionErrorCode code = dispensedTokens > 0
                ? SessionErrorCode.PartialDispense
                : SessionErrorCode.DispenseFailed;
            SetError(code);
            return;
        }

        SetState(SessionState.Completed);
        CompleteOperation(OperationOutcome.Completed);
        SetDeadline(_settings.Timeouts.SuccessSeconds);
    }

    private void OnMachineFault(MachineFault fault)
    {
        if (State == SessionState.Home)
        {
            return;
        }

        bool dispenseWasInProgress = State == SessionState.Dispensing;
        _machine.CancelActiveOperation();
        _machine.EndCashAcceptance();
        SetError(dispenseWasInProgress
            ? SessionErrorCode.DispenseStatusUnknown
            : SessionErrorCode.ConnectionLost);
    }

    private void HandleTimeout()
    {
        switch (State)
        {
            case SessionState.CardTerminalWait:
                _machine.CancelActiveOperation();
                SetError(SessionErrorCode.PaymentTimeout, SessionState.PaymentDeclined);
                break;
            case SessionState.Completed:
            case SessionState.PaymentDeclined:
            case SessionState.Error:
                ResetToHome();
                break;
            case SessionState.CashAccepting:
            case SessionState.CardAmountSelection:
            case SessionState.CardCustomAmount:
                CancelInternal(OperationOutcome.IdleTimeout);
                break;
        }
    }

    private void SetError(SessionErrorCode code, SessionState state = SessionState.Error)
    {
        SessionErrorDetails details = SessionErrorCatalog.Get(code);
        ErrorCode = details.Code;
        ErrorAction = details.RecommendedAction;
        ErrorTitle = details.Title;
        ErrorMessage = details.UserMessage;
        SetState(state);
        CompleteOperation(ToOutcome(code));
        SetDeadline(_settings.Timeouts.SessionIdleSeconds);
    }

    private void CancelInternal(OperationOutcome outcome)
    {
        if (State == SessionState.Home || State == SessionState.Dispensing)
        {
            return;
        }

        _machine.CancelActiveOperation();
        _machine.EndCashAcceptance();
        CompleteOperation(outcome);
        SetState(SessionState.Cancelled);
        ResetToHome();
    }

    private void ResetToHome()
    {
        CompleteOperation(OperationOutcome.Cancelled);
        _machine.CancelActiveOperation();
        _machine.EndCashAcceptance();
        ResetOperationData();
        PaymentMethod = PaymentMethod.None;
        ClearDeadline();
        SetState(SessionState.Home);
    }

    private void ResetOperationData()
    {
        CurrentOperationId = string.Empty;
        SelectedAmountRubles = 0;
        CashBalanceRubles = 0;
        BaseTokens = 0;
        BonusTokens = 0;
        ErrorCode = SessionErrorCode.None;
        ErrorAction = SessionErrorAction.ReturnHome;
        ErrorTitle = string.Empty;
        ErrorMessage = string.Empty;
        _operationFinished = false;
    }

    private void CompleteOperation(OperationOutcome outcome)
    {
        if (_operationFinished || string.IsNullOrWhiteSpace(CurrentOperationId))
        {
            return;
        }

        _operationFinished = true;
        OperationFinished?.Invoke(CreateSnapshot(outcome));
    }

    private SessionOperationSnapshot CreateSnapshot(OperationOutcome outcome) => new(
        CurrentOperationId,
        PaymentMethod,
        SelectedAmountRubles,
        BaseTokens,
        BonusTokens,
        outcome,
        ErrorCode);

    private static OperationOutcome ToOutcome(SessionErrorCode code) => code switch
    {
        SessionErrorCode.StockStatusUnknown => OperationOutcome.StockStatusUnknown,
        SessionErrorCode.InsufficientTokenInventory => OperationOutcome.InsufficientTokenInventory,
        SessionErrorCode.PaymentDeclined => OperationOutcome.PaymentDeclined,
        SessionErrorCode.PaymentTimeout => OperationOutcome.PaymentTimeout,
        SessionErrorCode.PartialDispense => OperationOutcome.PartialDispense,
        SessionErrorCode.ConnectionLost => OperationOutcome.ConnectionLost,
        SessionErrorCode.DispenseStatusUnknown => OperationOutcome.DispenseStatusUnknown,
        _ => OperationOutcome.DispenseFailed,
    };

    private void SetState(SessionState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        StateChanged?.Invoke(state);
    }

    private bool RequireState(SessionState expected)
    {
        return State == expected || RejectAction($"Ожидалось состояние {expected}, текущее — {State}.");
    }

    private bool RejectAction(string message)
    {
        ActionRejected?.Invoke(message);
        return false;
    }

    private void SetDeadline(int seconds)
    {
        _deadlineMs = _clock.NowMilliseconds + (ulong)Math.Max(1, seconds) * 1000UL;
        _lastCountdown = -1;
    }

    private void ClearDeadline()
    {
        _deadlineMs = 0;
        _lastCountdown = -1;
        CountdownChanged?.Invoke(0);
    }
}
