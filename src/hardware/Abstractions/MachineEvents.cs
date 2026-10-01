namespace Exchanger.Hardware.Abstractions;

public enum MachineCardResult
{
    Approved,
    Declined,
    Cancelled,
    Timeout,
}

public enum MachineFault
{
    ConnectionLost,
    Unavailable,
}

/// <summary>
/// Безопасное сервисное состояние канала связи с автоматом. Не содержит
/// имени COM-порта, сырых ответов контроллера или деталей handshake.
/// </summary>
public enum MachineConnectionState
{
    Disconnected,
    Authenticating,
    Ready,
    Faulted,
}
