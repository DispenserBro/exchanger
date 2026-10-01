using System;

namespace Exchanger.Core.Session;

public static class TokenCountFormatter
{
    public static string Format(int count)
    {
        int safeCount = Math.Max(0, count);
        int lastTwoDigits = safeCount % 100;
        int lastDigit = safeCount % 10;
        string noun = lastTwoDigits is >= 11 and <= 14
            ? "жетонов"
            : lastDigit switch
            {
                1 => "жетон",
                2 or 3 or 4 => "жетона",
                _ => "жетонов",
            };

        return $"{safeCount} {noun}";
    }

    public static string FormatUpper(int count) => Format(count).ToUpperInvariant();

    public static string FormatGiftUpper(int count) => count > 0
        ? $"+ {FormatUpper(count)}\nВ ПОДАРОК"
        : "БЕЗ БОНУСА";
}
