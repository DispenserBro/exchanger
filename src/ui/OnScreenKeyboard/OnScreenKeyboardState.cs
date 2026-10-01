using System;
using System.Collections.Generic;

namespace Exchanger.UI.OnScreenKeyboard;

public enum OnScreenKeyboardLayout
{
    Russian,
    English,
    Symbols,
    Numeric,
}

/// <summary>
/// Независимое от Godot состояние экранной клавиатуры. Визуальное дерево всегда
/// предоставляет тема, а этот класс хранит только выбранную раскладку и регистр.
/// </summary>
public sealed class OnScreenKeyboardState
{
    public const int TextKeyCount = 33;

    private static readonly string[] RussianLower =
    [
        "й", "ц", "у", "к", "е", "н", "г", "ш", "щ", "з", "х", "ъ",
        "ф", "ы", "в", "а", "п", "р", "о", "л", "д", "ж", "э",
        "я", "ч", "с", "м", "и", "т", "ь", "б", "ю", "ё",
    ];

    private static readonly string[] EnglishLower =
    [
        "q", "w", "e", "r", "t", "y", "u", "i", "o", "p",
        "a", "s", "d", "f", "g", "h", "j", "k", "l",
        "z", "x", "c", "v", "b", "n", "m",
    ];

    private static readonly string[] Symbols =
    [
        "1", "2", "3", "4", "5", "6", "7", "8", "9", "0",
        "!", "?", ".", ",", ":", ";", "\"", "'",
        "@", "#", "₽", "%", "&", "*", "+", "-", "_", "=", "/", "\\",
        "(", ")", "№",
    ];

    private OnScreenKeyboardLayout _lastAlphabeticLayout = OnScreenKeyboardLayout.Russian;

    public OnScreenKeyboardLayout Layout { get; private set; } = OnScreenKeyboardLayout.Russian;

    public OnScreenKeyboardLayout AlphabeticLayout => _lastAlphabeticLayout;

    public bool Shift { get; private set; }

    public IReadOnlyList<string> Keys
    {
        get
        {
            IReadOnlyList<string> source = Layout switch
            {
                OnScreenKeyboardLayout.English => EnglishLower,
                OnScreenKeyboardLayout.Symbols => Symbols,
                _ => RussianLower,
            };

            if (!Shift || Layout is OnScreenKeyboardLayout.Symbols or OnScreenKeyboardLayout.Numeric)
            {
                return source;
            }

            var upper = new string[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                upper[index] = source[index].ToUpperInvariant();
            }

            return upper;
        }
    }

    public void UseTextLayout()
    {
        Layout = _lastAlphabeticLayout;
        Shift = false;
    }

    public void UseNumericLayout()
    {
        Layout = OnScreenKeyboardLayout.Numeric;
        Shift = false;
    }

    public void ToggleLanguage()
    {
        _lastAlphabeticLayout = _lastAlphabeticLayout == OnScreenKeyboardLayout.Russian
            ? OnScreenKeyboardLayout.English
            : OnScreenKeyboardLayout.Russian;
        Layout = _lastAlphabeticLayout;
        Shift = false;
    }

    public void ToggleSymbols()
    {
        Layout = Layout == OnScreenKeyboardLayout.Symbols
            ? _lastAlphabeticLayout
            : OnScreenKeyboardLayout.Symbols;
        Shift = false;
    }

    public void ToggleShift()
    {
        if (Layout is OnScreenKeyboardLayout.Russian or OnScreenKeyboardLayout.English)
        {
            Shift = !Shift;
        }
    }
}
