using Exchanger.Core.Logging;
using Exchanger.Hardware.Abstractions;
using Godot;
using System;

namespace Exchanger.Hardware.MockMachine;

/// <summary>
/// Детерминированный симулятор типизированных событий автомата.
/// Не имитирует и не предполагает формат будущего wire-протокола.
/// </summary>
public partial class MockMachineController : Node, IMachineController, ITokenRecountController,
    IDualHopperMachineController, IDualHopperTokenRecountController
{
    private int _cashBalance;
    private bool _cashAcceptanceActive;
    private bool _cardPaymentActive;
    private int _operationGeneration;
    private string? _nextDispenseError;
    private int? _nextPartialDispenseCount;
    private bool _tokenRecountInProgress;
    private int _tokenRecountCount;
    private HopperChannel _tokenRecountHopper = HopperChannel.Hopper1;
    private int _hopper1Inventory;
    private int _hopper2Inventory;
    private bool _inventoryAccountingEnabled = true;
    private bool _purchaseLimitEnabled = true;

    [Export(PropertyHint.Range, "0.05,10,0.05")]
    public double DispenseDelaySeconds { get; set; } = 0.7;

    [Export(PropertyHint.Range, "0,1000,1")]
    public int MockRecountTokenCount { get; set; } = 12;

    public MachineStockLevel StockLevel { get; private set; } = MachineStockLevel.Enough;

    public MachineStockLevel Hopper1StockLevel { get; private set; } = MachineStockLevel.Enough;

    public MachineStockLevel Hopper2StockLevel { get; private set; } = MachineStockLevel.Enough;

    public MachineConnectionState ConnectionState { get; private set; } = MachineConnectionState.Ready;

    public bool IsTokenRecountInProgress => _tokenRecountInProgress;

    public bool CanStartTokenRecount =>
        ConnectionState == MachineConnectionState.Ready
        && !_cashAcceptanceActive
        && !_cardPaymentActive
        && !_tokenRecountInProgress;

    public event Action<MachineConnectionState>? ConnectionStateChanged;

    public event Action<MachineStockLevel>? StockChanged;

    public event Action<int>? CashBalanceChanged;

    public event Action<MachineCardResult>? CardPaymentFinished;

    public event Action<bool, int, string?>? DispenseFinished;

    public event Action<MachineFault>? FaultOccurred;

    public event Action<int>? TokenRecountProgressChanged;

    public event Action<bool, int, string?>? TokenRecountFinished;

    public event Action<HopperChannel, MachineStockLevel>? HopperStockChanged;

    public event Action<int, int>? HopperDispenseAccounted;

    public event Action<int, int>? DispenseProgressChanged;

    public event Action<HopperChannel, int>? HopperTokenRecountProgressChanged;

    public event Action<HopperChannel, bool, int, string?>? HopperTokenRecountFinished;

    public void Configure(MachineStockLevel initialStockLevel)
    {
        SetConnectionState(MachineConnectionState.Ready);
        SetStockLevel(initialStockLevel);
    }

    public void SetStockLevel(MachineStockLevel level)
    {
        StockLevel = level is MachineStockLevel.Enough or MachineStockLevel.Low
            ? level
            : MachineStockLevel.Unknown;
        Hopper1StockLevel = StockLevel;
        Hopper2StockLevel = StockLevel;
        HopperStockChanged?.Invoke(HopperChannel.Hopper1, Hopper1StockLevel);
        HopperStockChanged?.Invoke(HopperChannel.Hopper2, Hopper2StockLevel);
        StockChanged?.Invoke(StockLevel);
    }

    public void SetHopperInventory(int hopper1Count, int hopper2Count)
    {
        _hopper1Inventory = Math.Max(0, hopper1Count);
        _hopper2Inventory = Math.Max(0, hopper2Count);
    }

    public void SetInventoryAccountingEnabled(bool enabled) =>
        _inventoryAccountingEnabled = enabled;

    public void SetPurchaseLimitEnabled(bool enabled) =>
        _purchaseLimitEnabled = enabled;

    public bool CanStartHopperTokenRecount(HopperChannel hopper) => CanStartTokenRecount;

    public void BeginCashAcceptance()
    {
        if (_tokenRecountInProgress)
        {
            return;
        }

        _operationGeneration++;
        _cashBalance = 0;
        _cashAcceptanceActive = true;
        _cardPaymentActive = false;
        CashBalanceChanged?.Invoke(_cashBalance);
        AppLogger.Info("MockMachine", "Начат mock-приём наличных.");
    }

    public void EndCashAcceptance()
    {
        _cashAcceptanceActive = false;
        AppLogger.Info("MockMachine", "Mock-приём наличных остановлен.");
    }

    public void AddCash(int amountRubles)
    {
        if (!_cashAcceptanceActive || amountRubles <= 0)
        {
            return;
        }

        _cashBalance += amountRubles;
        CashBalanceChanged?.Invoke(_cashBalance);
        AppLogger.Info("MockMachine", $"Принято {amountRubles} ₽, баланс {_cashBalance} ₽.");
    }

    public void BeginCardPayment(int amountRubles)
    {
        if (_tokenRecountInProgress)
        {
            CardPaymentFinished?.Invoke(MachineCardResult.Declined);
            return;
        }

        _operationGeneration++;
        _cashAcceptanceActive = false;
        _cardPaymentActive = true;
        AppLogger.Info("MockMachine", $"Начато mock-ожидание карточной оплаты {amountRubles} ₽.");
    }

    public void SimulateCardResult(MachineCardResult result)
    {
        if (!_cardPaymentActive)
        {
            return;
        }

        _cardPaymentActive = false;
        CardPaymentFinished?.Invoke(result);
        AppLogger.Info("MockMachine", $"Mock-результат карточной оплаты: {result}.");
    }

    public void SimulateNextDispenseFailure(string message = "Смоделированная ошибка выдачи")
    {
        _nextDispenseError = string.IsNullOrWhiteSpace(message)
            ? "Смоделированная ошибка выдачи"
            : message.Trim();
        _nextPartialDispenseCount = null;
    }

    public void SimulateNextPartialDispense(int dispensedTokens, string message = "Смоделирована частичная выдача")
    {
        _nextPartialDispenseCount = Math.Max(0, dispensedTokens);
        _nextDispenseError = string.IsNullOrWhiteSpace(message)
            ? "Смоделирована частичная выдача"
            : message.Trim();
    }

    public void SimulateConnectionLoss()
    {
        CancelTokenRecount("CONNECTION_LOST");
        _operationGeneration++;
        _cashAcceptanceActive = false;
        _cardPaymentActive = false;
        SetConnectionState(MachineConnectionState.Disconnected);
        SetStockLevel(MachineStockLevel.Unknown);
        FaultOccurred?.Invoke(MachineFault.ConnectionLost);
        AppLogger.Warning("MockMachine", "Смоделирована потеря связи с автоматом.");
    }

    public void RestoreConnection(MachineStockLevel stockLevel = MachineStockLevel.Enough)
    {
        SetConnectionState(MachineConnectionState.Ready);
        SetStockLevel(stockLevel);
        AppLogger.Info("MockMachine", "Mock-связь с автоматом восстановлена.");
    }

    public void CancelActiveOperation()
    {
        CancelTokenRecount();
        _operationGeneration++;
        _cashAcceptanceActive = false;
        _cardPaymentActive = false;
        AppLogger.Info("MockMachine", "Активная mock-операция отменена.");
    }

    public async void DispenseTokens(int tokenCount)
    {
        if (_tokenRecountInProgress)
        {
            DispenseFinished?.Invoke(false, 0, "TOKEN_RECOUNT_IN_PROGRESS");
            return;
        }

        if (tokenCount <= 0)
        {
            DispenseFinished?.Invoke(false, 0, "Некорректное количество жетонов");
            return;
        }

        if (_inventoryAccountingEnabled
            && _purchaseLimitEnabled
            && !DualHopperDispensePlanner.TryCreate(
                tokenCount,
                _hopper1Inventory,
                _hopper2Inventory,
                out _))
        {
            DispenseFinished?.Invoke(false, 0, "INSUFFICIENT_ACCOUNTED_TOKENS");
            return;
        }

        int generation = ++_operationGeneration;
        string? forcedError = _nextDispenseError;
        int? forcedPartialCount = _nextPartialDispenseCount;
        _nextDispenseError = null;
        _nextPartialDispenseCount = null;

        double delay = Math.Clamp(DispenseDelaySeconds, 0.05, 10.0);
        await ToSignal(GetTree().CreateTimer(delay), SceneTreeTimer.SignalName.Timeout);
        if (generation != _operationGeneration)
        {
            return;
        }

        if (forcedError is not null && forcedPartialCount is null)
        {
            DispenseFinished?.Invoke(false, 0, forcedError);
            AppLogger.Warning("MockMachine", $"Mock-выдача завершена ошибкой: {forcedError}.");
            return;
        }

        int dispensedTokens = Math.Min(tokenCount, forcedPartialCount ?? tokenCount);
        bool success = dispensedTokens == tokenCount && forcedError is null;
        string? error = success ? null : forcedError ?? "Выдано не всё запрошенное количество жетонов";
        (int hopper1, int hopper2) = AllocateDispensedTokens(dispensedTokens);
        if (hopper1 > 0 || hopper2 > 0)
        {
            DispenseProgressChanged?.Invoke(dispensedTokens, tokenCount);
            HopperDispenseAccounted?.Invoke(hopper1, hopper2);
        }

        DispenseFinished?.Invoke(success, dispensedTokens, error);
        AppLogger.Info("MockMachine", $"Mock-выдача завершена: {dispensedTokens} из {tokenCount} жетонов, success={success}.");
    }

    public bool TryStartTokenRecount(ulong nowMilliseconds) =>
        TryStartTokenRecount(HopperChannel.Hopper1, nowMilliseconds);

    public bool TryStartTokenRecount(HopperChannel hopper, ulong nowMilliseconds)
    {
        if (!CanStartHopperTokenRecount(hopper))
        {
            return false;
        }

        int generation = ++_operationGeneration;
        _tokenRecountInProgress = true;
        _tokenRecountHopper = hopper;
        _tokenRecountCount = 0;
        TokenRecountProgressChanged?.Invoke(0);
        HopperTokenRecountProgressChanged?.Invoke(hopper, 0);
        RunTokenRecountAsync(generation);
        return true;
    }

    public void CancelTokenRecount() => CancelTokenRecount("TOKEN_RECOUNT_CANCELLED");

    private async void RunTokenRecountAsync(int generation)
    {
        int target = Math.Clamp(MockRecountTokenCount, 0, 1000);
        for (int index = 0; index < target; index++)
        {
            await ToSignal(GetTree().CreateTimer(0.12), SceneTreeTimer.SignalName.Timeout);
            if (generation != _operationGeneration || !_tokenRecountInProgress)
            {
                return;
            }

            _tokenRecountCount++;
            TokenRecountProgressChanged?.Invoke(_tokenRecountCount);
            HopperTokenRecountProgressChanged?.Invoke(_tokenRecountHopper, _tokenRecountCount);
        }

        await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
        if (generation != _operationGeneration || !_tokenRecountInProgress)
        {
            return;
        }

        int counted = _tokenRecountCount;
        _tokenRecountInProgress = false;
        _tokenRecountCount = 0;
        TokenRecountFinished?.Invoke(true, counted, null);
        HopperTokenRecountFinished?.Invoke(_tokenRecountHopper, true, counted, null);
    }

    private void CancelTokenRecount(string errorCode)
    {
        if (!_tokenRecountInProgress)
        {
            return;
        }

        int counted = _tokenRecountCount;
        _tokenRecountInProgress = false;
        _tokenRecountCount = 0;
        _operationGeneration++;
        TokenRecountFinished?.Invoke(false, counted, errorCode);
        HopperTokenRecountFinished?.Invoke(_tokenRecountHopper, false, counted, errorCode);
    }

    private (int Hopper1, int Hopper2) AllocateDispensedTokens(int count)
    {
        if (!_inventoryAccountingEnabled)
        {
            return (count, 0);
        }

        if (!_purchaseLimitEnabled)
        {
            int hopper1 = Math.Min(count, _hopper1Inventory);
            int hopper2 = count - hopper1;
            _hopper1Inventory = Math.Max(0, _hopper1Inventory - hopper1);
            _hopper2Inventory = Math.Max(0, _hopper2Inventory - hopper2);
            return (hopper1, hopper2);
        }

        if (!DualHopperDispensePlanner.TryCreate(
                count,
                _hopper1Inventory,
                _hopper2Inventory,
                out DualHopperDispensePlan plan))
        {
            return (0, 0);
        }

        _hopper1Inventory -= plan.Hopper1Count;
        _hopper2Inventory -= plan.Hopper2Count;
        return (plan.Hopper1Count, plan.Hopper2Count);
    }

    private void SetConnectionState(MachineConnectionState state)
    {
        if (ConnectionState == state)
        {
            return;
        }

        ConnectionState = state;
        ConnectionStateChanged?.Invoke(state);
    }
}
