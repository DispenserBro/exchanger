namespace Exchanger.Core.Session;

public enum SessionErrorCode
{
    None,
    InvalidTokenCalculation,
    InsufficientTokenInventory,
    StockStatusUnknown,
    PaymentDeclined,
    PaymentTimeout,
    DispenseFailed,
    PartialDispense,
    ConnectionLost,
    DispenseStatusUnknown,
}

public enum SessionErrorAction
{
    ReturnHome,
    TryAnotherPaymentMethod,
    ContactSupport,
}

public readonly record struct SessionErrorDetails(
    SessionErrorCode Code,
    string Title,
    string UserMessage,
    SessionErrorAction RecommendedAction);

public static class SessionErrorCatalog
{
    public static SessionErrorDetails Get(SessionErrorCode code) => code switch
    {
        SessionErrorCode.InvalidTokenCalculation => new(code, "ОШИБКА ВЫДАЧИ", "Не удалось рассчитать количество жетонов.", SessionErrorAction.ReturnHome),
        SessionErrorCode.InsufficientTokenInventory => new(code, "НЕДОСТАТОЧНО ЖЕТОНОВ", "В автомате недостаточно жетонов для выбранной суммы. Выберите меньшую сумму.", SessionErrorAction.ReturnHome),
        SessionErrorCode.StockStatusUnknown => new(code, "НЕТ ДАННЫХ О ЖЕТОНАХ", "Автомат не подтвердил состояние запаса. Обратитесь в техподдержку.", SessionErrorAction.ContactSupport),
        SessionErrorCode.PaymentDeclined => new(code, "ОПЛАТА НЕ ПРОШЛА", "Банк отклонил операцию. Попробуйте другой способ оплаты.", SessionErrorAction.TryAnotherPaymentMethod),
        SessionErrorCode.PaymentTimeout => new(code, "ВРЕМЯ ОПЛАТЫ ИСТЕКЛО", "Карта не была подтверждена вовремя.", SessionErrorAction.TryAnotherPaymentMethod),
        SessionErrorCode.PartialDispense => new(code, "ВЫДАНЫ НЕ ВСЕ ЖЕТОНЫ", "Обратитесь в техподдержку и сообщите время операции.", SessionErrorAction.ContactSupport),
        SessionErrorCode.ConnectionLost => new(code, "НЕТ СВЯЗИ С АВТОМАТОМ", "Операция остановлена. Попробуйте позже или обратитесь в техподдержку.", SessionErrorAction.ContactSupport),
        SessionErrorCode.DispenseStatusUnknown => new(code, "СТАТУС ВЫДАЧИ НЕИЗВЕСТЕН", "Не повторяйте оплату. Обратитесь в техподдержку и сообщите время операции.", SessionErrorAction.ContactSupport),
        _ => new(SessionErrorCode.DispenseFailed, "ОШИБКА ВЫДАЧИ", "Контроллер не подтвердил полную выдачу жетонов.", SessionErrorAction.ContactSupport),
    };
}
