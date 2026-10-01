using System;

namespace Exchanger.Hardware.Abstractions;

/// <summary>
/// Типизированная граница бизнес-логики и автомата. Будущий wire-протокол
/// преобразуется в эти события отдельным адаптером.
/// </summary>
public interface IMachineController
{
    MachineStockLevel StockLevel { get; }

    MachineConnectionState ConnectionState { get; }

    event Action<MachineConnectionState>? ConnectionStateChanged;

    event Action<MachineStockLevel>? StockChanged;

    event Action<int>? CashBalanceChanged;

    event Action<MachineCardResult>? CardPaymentFinished;

    event Action<bool, int, string?>? DispenseFinished;

    event Action<MachineFault>? FaultOccurred;

    void BeginCashAcceptance();

    void EndCashAcceptance();

    void BeginCardPayment(int amountRubles);

    void CancelActiveOperation();

    void DispenseTokens(int tokenCount);
}
