using Exchanger.Core.Configuration;
using Exchanger.Core.Session;
using Exchanger.Hardware.Abstractions;

namespace Exchanger.Tests;

internal sealed class ManualClock : IMonotonicClock
{
    public ulong NowMilliseconds { get; private set; }

    public void AdvanceSeconds(int seconds) => NowMilliseconds += (ulong)seconds * 1000UL;

    public void AdvanceMilliseconds(ulong milliseconds) => NowMilliseconds += milliseconds;
}

internal sealed class SequenceSessionIdGenerator : ISessionIdGenerator
{
    private int _next;

    public string CreateId() => $"operation-{++_next:D4}";
}

internal sealed class FakeMachineController : IMachineController
{
    public FakeMachineController(MachineStockLevel stockLevel = MachineStockLevel.Enough)
    {
        StockLevel = stockLevel;
    }

    public MachineStockLevel StockLevel { get; private set; }

    public MachineConnectionState ConnectionState { get; private set; } = MachineConnectionState.Ready;

    public int BeginCashCalls { get; private set; }

    public int EndCashCalls { get; private set; }

    public int BeginCardCalls { get; private set; }

    public int LastCardAmount { get; private set; }

    public int CancelCalls { get; private set; }

    public int DispenseCalls { get; private set; }

    public int LastDispenseCount { get; private set; }

    public int TotalSubscriberCount =>
        (StockChanged?.GetInvocationList().Length ?? 0)
        + (CashBalanceChanged?.GetInvocationList().Length ?? 0)
        + (CardPaymentFinished?.GetInvocationList().Length ?? 0)
        + (DispenseFinished?.GetInvocationList().Length ?? 0)
        + (FaultOccurred?.GetInvocationList().Length ?? 0)
        + (ConnectionStateChanged?.GetInvocationList().Length ?? 0);

    public event Action<MachineConnectionState>? ConnectionStateChanged;

    public event Action<MachineStockLevel>? StockChanged;

    public event Action<int>? CashBalanceChanged;

    public event Action<MachineCardResult>? CardPaymentFinished;

    public event Action<bool, int, string?>? DispenseFinished;

    public event Action<MachineFault>? FaultOccurred;

    public void BeginCashAcceptance() => BeginCashCalls++;

    public void EndCashAcceptance() => EndCashCalls++;

    public void BeginCardPayment(int amountRubles)
    {
        BeginCardCalls++;
        LastCardAmount = amountRubles;
    }

    public void CancelActiveOperation() => CancelCalls++;

    public void DispenseTokens(int tokenCount)
    {
        DispenseCalls++;
        LastDispenseCount = tokenCount;
    }

    public void RaiseCashBalance(int balanceRubles) => CashBalanceChanged?.Invoke(balanceRubles);

    public void RaiseCardResult(MachineCardResult result) => CardPaymentFinished?.Invoke(result);

    public void RaiseFault(MachineFault fault) => FaultOccurred?.Invoke(fault);

    public void RaiseConnectionState(MachineConnectionState state)
    {
        ConnectionState = state;
        ConnectionStateChanged?.Invoke(state);
    }

    public void RaiseStockLevel(MachineStockLevel level)
    {
        StockLevel = level;
        StockChanged?.Invoke(StockLevel);
    }

    public void CompleteDispense(bool success, int? dispensedTokens = null, string? error = null)
    {
        int count = dispensedTokens ?? LastDispenseCount;
        DispenseFinished?.Invoke(success, count, error);
    }
}

internal static class TestSettings
{
    public static AppSettings Create()
    {
        return new AppSettings
        {
            Timeouts = new TimeoutSettings
            {
                SessionIdleSeconds = 45,
                CashPaymentSeconds = 45,
                CashlessPaymentSeconds = 120,
                SuccessSeconds = 10,
            },
            Pricing = new PricingSettings
            {
                TokenPriceRubles = 10,
                MaxCardAmountRubles = 5000,
                CustomAmountStepRubles = 10,
                BonusRules =
                [
                    new BonusRule { MinimumAmountRubles = 100, BonusTokens = 2 },
                    new BonusRule { MinimumAmountRubles = 500, BonusTokens = 15 },
                ],
            },
        };
    }
}
