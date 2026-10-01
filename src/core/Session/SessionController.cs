using Exchanger.Core.Configuration;
using Exchanger.Core.Logging;
using Exchanger.Hardware.Abstractions;
using Godot;
using System;

namespace Exchanger.Core.Session;

/// <summary>
/// Godot-адаптер чистого SessionFlow. Сохраняет публичный API сцены,
/// а обновление таймаутов привязывает к процессу движка.
/// </summary>
public partial class SessionController : Node
{
    private SessionFlow? _flow;

    public SessionState State => _flow?.State ?? SessionState.Home;

    public PaymentMethod PaymentMethod => _flow?.PaymentMethod ?? PaymentMethod.None;

    public string CurrentOperationId => _flow?.CurrentOperationId ?? string.Empty;

    public int SelectedAmountRubles => _flow?.SelectedAmountRubles ?? 0;

    public int CashBalanceRubles => _flow?.CashBalanceRubles ?? 0;

    public int BaseTokens => _flow?.BaseTokens ?? 0;

    public int BonusTokens => _flow?.BonusTokens ?? 0;

    public int TotalTokens => _flow?.TotalTokens ?? 0;

    public MachineStockLevel StockLevel => _flow?.StockLevel ?? MachineStockLevel.Unknown;

    public SessionErrorCode ErrorCode => _flow?.ErrorCode ?? SessionErrorCode.None;

    public SessionErrorAction ErrorAction => _flow?.ErrorAction ?? SessionErrorAction.ReturnHome;

    public string ErrorTitle => _flow?.ErrorTitle ?? string.Empty;

    public string ErrorMessage => _flow?.ErrorMessage ?? string.Empty;

    public event Action<SessionState>? StateChanged;

    public event Action<int>? CashBalanceChanged;

    public event Action<MachineStockLevel>? StockChanged;

    public event Action<int>? CountdownChanged;

    public void Configure(
        AppSettings settings,
        IMachineController machine,
        int? maximumAvailableTokens = null)
    {
        DetachFlow();
        _flow = new SessionFlow(
            settings,
            machine,
            new GodotMonotonicClock(),
            maximumAvailableTokens: maximumAvailableTokens);
        _flow.StateChanged += OnStateChanged;
        _flow.CashBalanceChanged += OnCashBalanceChanged;
        _flow.StockChanged += OnStockChanged;
        _flow.CountdownChanged += OnCountdownChanged;
        _flow.ActionRejected += OnActionRejected;
        _flow.OperationStarted += OnOperationStarted;
        _flow.OperationFinished += OnOperationFinished;
        SetProcess(true);
    }

    public override void _Process(double delta) => _flow?.Update();

    public override void _ExitTree() => DetachFlow();

    public void Shutdown() => DetachFlow();

    public bool BeginCash() => _flow?.BeginCash() ?? false;

    public bool BeginCard() => _flow?.BeginCard() ?? false;

    public bool OpenCustomCardAmount() => _flow?.OpenCustomCardAmount() ?? false;

    public bool ReturnToCardAmounts() => _flow?.ReturnToCardAmounts() ?? false;

    public bool SelectCardAmount(int amountRubles) => _flow?.SelectCardAmount(amountRubles) ?? false;

    public bool ConfirmCashDispense() => _flow?.ConfirmCashDispense() ?? false;

    public void NotifyUserActivity() => _flow?.NotifyUserActivity();

    public void Cancel() => _flow?.Cancel();

    public void ReturnHome() => _flow?.ReturnHome();

    public bool IsAmountValid(int amountRubles) => _flow?.IsAmountValid(amountRubles) ?? false;

    public void SetMaximumAvailableTokens(int? maximumAvailableTokens) =>
        _flow?.SetMaximumAvailableTokens(maximumAvailableTokens);

    private void OnStateChanged(SessionState state)
    {
        AppLogger.Info("Session", $"Состояние сессии: {state}.");
        StateChanged?.Invoke(state);
    }

    private void OnCashBalanceChanged(int balance) => CashBalanceChanged?.Invoke(balance);

    private void OnStockChanged(MachineStockLevel stockLevel) => StockChanged?.Invoke(stockLevel);

    private void OnCountdownChanged(int seconds) => CountdownChanged?.Invoke(seconds);

    private static void OnActionRejected(string message) => AppLogger.Warning("Session", message);

    private static void OnOperationStarted(SessionOperationSnapshot snapshot)
    {
        OperationJournal.RecordStarted(snapshot);
        AppLogger.Info("Operation", $"event=start operation_id={snapshot.OperationId} payment={snapshot.PaymentMethod}");
    }

    private static void OnOperationFinished(SessionOperationSnapshot snapshot)
    {
        OperationJournal.RecordFinished(snapshot);
        AppLogger.Info(
            "Operation",
            $"event=finish operation_id={snapshot.OperationId} payment={snapshot.PaymentMethod} amount={snapshot.AmountRubles} tokens={snapshot.BaseTokens + snapshot.BonusTokens} outcome={snapshot.Outcome} error={snapshot.ErrorCode}");
    }

    private void DetachFlow()
    {
        if (_flow is null)
        {
            return;
        }

        _flow.Dispose();
        _flow.StateChanged -= OnStateChanged;
        _flow.CashBalanceChanged -= OnCashBalanceChanged;
        _flow.StockChanged -= OnStockChanged;
        _flow.CountdownChanged -= OnCountdownChanged;
        _flow.ActionRejected -= OnActionRejected;
        _flow.OperationStarted -= OnOperationStarted;
        _flow.OperationFinished -= OnOperationFinished;
        _flow = null;
    }
}
