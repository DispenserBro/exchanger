using Godot;

namespace Exchanger.Tests.Runtime;

/// <summary>
/// Даёт GDScript smoke-тесту типобезопасный доступ к C#-методам с nullable-параметрами,
/// которые Godot не публикует в Variant API. Рабочую конфигурацию тест не изменяет.
/// </summary>
public partial class PaymentRewardFormattingSmokeBridge : Node
{
    public void ClearPurchaseLimits(Node cash, Node card, Node custom)
    {
        ((global::Exchanger.Scenes.CashPaymentScreen.CashPaymentScreen)cash).SetTokenPurchaseLimit(null);
        ((global::Exchanger.Scenes.CardAmountScreen.CardAmountScreen)card).SetTokenPurchaseLimit(null);
        ((global::Exchanger.Scenes.CardCustomAmountScreen.CardCustomAmountScreen)custom).SetTokenPurchaseLimit(null);
    }

    public void SetCashBalance(Node cash, int amountRubles) =>
        ((global::Exchanger.Scenes.CashPaymentScreen.CashPaymentScreen)cash).SetBalance(amountRubles);
}
