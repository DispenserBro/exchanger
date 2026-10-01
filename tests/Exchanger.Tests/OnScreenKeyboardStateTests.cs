using Exchanger.UI.OnScreenKeyboard;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class OnScreenKeyboardStateTests
{
    [Test]
    public void StartsWithCompleteRussianLayout()
    {
        var state = new OnScreenKeyboardState();

        Assert.Multiple((System.Action)(() =>
        {
            Assert.That(state.Layout, Is.EqualTo(OnScreenKeyboardLayout.Russian));
            Assert.That(state.Keys, Has.Count.EqualTo(33));
            Assert.That(state.Keys[0], Is.EqualTo("й"));
            Assert.That(state.Keys[32], Is.EqualTo("ё"));
        }));
    }

    [Test]
    public void LanguageSwitchKeepsEnglishAfterSymbolsRoundTrip()
    {
        var state = new OnScreenKeyboardState();
        state.ToggleLanguage();
        state.ToggleSymbols();
        state.ToggleSymbols();

        Assert.Multiple((System.Action)(() =>
        {
            Assert.That(state.Layout, Is.EqualTo(OnScreenKeyboardLayout.English));
            Assert.That(state.AlphabeticLayout, Is.EqualTo(OnScreenKeyboardLayout.English));
            Assert.That(state.Keys, Has.Count.EqualTo(26));
            Assert.That(state.Keys[0], Is.EqualTo("q"));
        }));
    }

    [Test]
    public void SymbolsHaveExactlyAsManyEntriesAsThemeSlots()
    {
        var state = new OnScreenKeyboardState();
        state.ToggleSymbols();

        Assert.Multiple((System.Action)(() =>
        {
            Assert.That(state.Layout, Is.EqualTo(OnScreenKeyboardLayout.Symbols));
            Assert.That(state.Keys, Has.Count.EqualTo(OnScreenKeyboardState.TextKeyCount));
            Assert.That(state.Keys, Does.Contain("?"));
            Assert.That(state.Keys, Does.Contain("₽"));
            Assert.That(state.Keys, Does.Contain("\\"));
        }));
    }

    [Test]
    public void ShiftOnlyChangesAlphabeticLayout()
    {
        var state = new OnScreenKeyboardState();
        state.ToggleShift();
        Assert.That(state.Keys[0], Is.EqualTo("Й"));

        state.ToggleSymbols();
        state.ToggleShift();
        Assert.Multiple((System.Action)(() =>
        {
            Assert.That(state.Shift, Is.False);
            Assert.That(state.Keys[10], Is.EqualTo("!"));
        }));
    }

    [Test]
    public void NumericModeReturnsToLastAlphabeticLayout()
    {
        var state = new OnScreenKeyboardState();
        state.ToggleLanguage();
        state.UseNumericLayout();
        state.UseTextLayout();

        Assert.That(state.Layout, Is.EqualTo(OnScreenKeyboardLayout.English));
    }
}
