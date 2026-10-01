using System;

namespace Exchanger.Hardware.Abstractions;

/// <summary>
/// Сервисный прямой пересчёт жетонов. В отличие от обычной выдачи включает
/// двигатель хоппера напрямую и считает фронты аппаратного датчика выдачи.
/// </summary>
public interface ITokenRecountController
{
    bool IsTokenRecountInProgress { get; }

    bool CanStartTokenRecount { get; }

    event Action<int>? TokenRecountProgressChanged;

    event Action<bool, int, string?>? TokenRecountFinished;

    bool TryStartTokenRecount(ulong nowMilliseconds);

    void CancelTokenRecount();
}
