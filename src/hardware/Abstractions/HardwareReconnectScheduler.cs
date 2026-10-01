using System;

namespace Exchanger.Hardware.Abstractions;

/// <summary>
/// Планирует повторные попытки подключения по монотонному времени.
/// После каждого обрыва и каждой попытки выдерживается полный настроенный интервал.
/// </summary>
internal sealed class HardwareReconnectScheduler
{
    private readonly ulong _intervalMilliseconds;
    private ulong _nextAttemptAt;
    private bool _connectionObserved;
    private bool _wasConnected;

    public HardwareReconnectScheduler(int intervalSeconds)
    {
        if (intervalSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
        }

        _intervalMilliseconds = checked((ulong)intervalSeconds * 1000UL);
    }

    public void ScheduleFrom(ulong nowMilliseconds)
    {
        _nextAttemptAt = ulong.MaxValue - nowMilliseconds < _intervalMilliseconds
            ? ulong.MaxValue
            : nowMilliseconds + _intervalMilliseconds;
    }

    public bool ObserveConnection(bool isConnected, ulong nowMilliseconds)
    {
        bool disconnectedNow = _connectionObserved && _wasConnected && !isConnected;
        _connectionObserved = true;
        _wasConnected = isConnected;
        if (disconnectedNow)
        {
            ScheduleFrom(nowMilliseconds);
        }

        return disconnectedNow;
    }

    public bool TryBeginAttempt(ulong nowMilliseconds)
    {
        if (nowMilliseconds < _nextAttemptAt)
        {
            return false;
        }

        ScheduleFrom(nowMilliseconds);
        return true;
    }
}
