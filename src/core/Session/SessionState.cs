namespace Exchanger.Core.Session;

public enum SessionState
{
    Home,
    CashAccepting,
    CardAmountSelection,
    CardCustomAmount,
    CardTerminalWait,
    Dispensing,
    Completed,
    PaymentDeclined,
    Error,
    Cancelled,
}
