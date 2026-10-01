namespace Exchanger.Core.Session;

/// <summary>
/// Монотонный источник времени. Абстракция позволяет тестировать таймауты
/// сессии без ожидания реальных секунд.
/// </summary>
public interface IMonotonicClock
{
    ulong NowMilliseconds { get; }
}
