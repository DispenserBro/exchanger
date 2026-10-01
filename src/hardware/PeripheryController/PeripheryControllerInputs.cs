namespace Exchanger.Hardware.PeripheryController;

/// <summary>
/// Подтверждённая владельцем разводка цифровых входов контроллера.
/// Значения служат production-default и могут быть переопределены внешней конфигурацией.
/// </summary>
public static class PeripheryControllerInputs
{
    public const int ServiceButton = 8;
    public const int Hopper1StockLow = 1;
    public const int Hopper2StockLow = 4;
    public const int Hopper1DispensePulse = 24;

    /// <summary>IN25 — физический датчик выдачи второго хоппера.</summary>
    public const int Hopper2DispensePulse = 25;
}
