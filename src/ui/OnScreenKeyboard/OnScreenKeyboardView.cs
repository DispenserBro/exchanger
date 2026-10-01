using Godot;
using System;
using System.Collections.Generic;

namespace Exchanger.UI.OnScreenKeyboard;

/// <summary>Ссылки на заранее созданные темой элементы клавиатур.</summary>
public sealed class OnScreenKeyboardView
{
    public required Control TextKeyboard { get; init; }
    public required Button TextHide { get; init; }
    public required IReadOnlyList<Button> TextKeys { get; init; }
    public required Button Shift { get; init; }
    public required Button Language { get; init; }
    public required Button Symbols { get; init; }
    public required Button TextBackspace { get; init; }
    public required Button TextClear { get; init; }
    public required Button Space { get; init; }
    public required Button CaretLeft { get; init; }
    public required Button CaretRight { get; init; }
    public required Control NumericKeyboard { get; init; }
    public required Button NumericHide { get; init; }
    public required IReadOnlyList<Button> Digits { get; init; }
    public required Button NumericBackspace { get; init; }
    public required Button NumericClear { get; init; }

    public void Validate()
    {
        if (TextKeys.Count != OnScreenKeyboardState.TextKeyCount)
        {
            throw new InvalidOperationException(
                $"Тема должна предоставить {OnScreenKeyboardState.TextKeyCount} текстовых клавиш.");
        }

        if (Digits.Count != 10)
        {
            throw new InvalidOperationException("Тема должна предоставить 10 цифровых клавиш.");
        }
    }
}
