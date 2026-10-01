namespace Exchanger.Core.Session;

public enum OperationOutcome
{
    InProgress,
    Completed,
    Cancelled,
    IdleTimeout,
    PaymentDeclined,
    PaymentTimeout,
    InsufficientTokenInventory,
    StockStatusUnknown,
    DispenseFailed,
    PartialDispense,
    ConnectionLost,
    DispenseStatusUnknown,
    Interrupted,
    UnknownAfterRestart,
}

public readonly record struct SessionOperationSnapshot(
    string OperationId,
    PaymentMethod PaymentMethod,
    int AmountRubles,
    int BaseTokens,
    int BonusTokens,
    OperationOutcome Outcome,
    SessionErrorCode ErrorCode);
