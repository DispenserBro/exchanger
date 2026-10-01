using System;

namespace Exchanger.Hardware.Abstractions;

/// <summary>Раздельные датчики, маршрутизация выдачи и учёт подтверждений двух хопперов.</summary>
public interface IDualHopperMachineController
{
    MachineStockLevel Hopper1StockLevel { get; }

    MachineStockLevel Hopper2StockLevel { get; }

    event Action<HopperChannel, MachineStockLevel>? HopperStockChanged;

    /// <summary>Сообщает контроллеру точный учётный остаток для безопасного выбора хоппера.</summary>
    void SetHopperInventory(int hopper1Count, int hopper2Count);

    /// <summary>
    /// При отключённом учёте точный остаток не ограничивает выдачу; DualHopperAuto
    /// использует приоритетный первый хоппер без небезопасного автоматического failover.
    /// </summary>
    void SetInventoryAccountingEnabled(bool enabled);

    void SetPurchaseLimitEnabled(bool enabled);

    /// <summary>Фактически подтверждённое распределение завершившейся выдачи.</summary>
    event Action<int, int>? HopperDispenseAccounted;

    /// <summary>Прогресс текущей выдачи: подтверждённое и запрошенное количество жетонов.</summary>
    event Action<int, int>? DispenseProgressChanged;
}

/// <summary>Раздельный сервисный пересчёт каждого хоппера.</summary>
public interface IDualHopperTokenRecountController
{
    bool CanStartHopperTokenRecount(HopperChannel hopper);

    event Action<HopperChannel, int>? HopperTokenRecountProgressChanged;

    event Action<HopperChannel, bool, int, string?>? HopperTokenRecountFinished;

    bool TryStartTokenRecount(HopperChannel hopper, ulong nowMilliseconds);
}
