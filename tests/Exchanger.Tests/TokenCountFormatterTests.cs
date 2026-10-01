using Exchanger.Core.Session;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class TokenCountFormatterTests
{
    [TestCase(0, "0 жетонов")]
    [TestCase(1, "1 жетон")]
    [TestCase(2, "2 жетона")]
    [TestCase(4, "4 жетона")]
    [TestCase(5, "5 жетонов")]
    [TestCase(11, "11 жетонов")]
    [TestCase(14, "14 жетонов")]
    [TestCase(21, "21 жетон")]
    [TestCase(22, "22 жетона")]
    [TestCase(25, "25 жетонов")]
    [TestCase(101, "101 жетон")]
    [TestCase(111, "111 жетонов")]
    [TestCase(124, "124 жетона")]
    public void Format_UsesRussianTokenDeclension(int count, string expected)
    {
        Assert.That(TokenCountFormatter.Format(count), Is.EqualTo(expected));
    }

    [Test]
    public void FormatUpper_ReturnsDisplayReadyUppercaseText()
    {
        Assert.That(TokenCountFormatter.FormatUpper(2), Is.EqualTo("2 ЖЕТОНА"));
    }

    [TestCase(0, "БЕЗ БОНУСА")]
    [TestCase(2, "+ 2 ЖЕТОНА\nВ ПОДАРОК")]
    [TestCase(15, "+ 15 ЖЕТОНОВ\nВ ПОДАРОК")]
    public void FormatGiftUpper_UsesSharedTwoLinePaymentFormat(int count, string expected)
    {
        Assert.That(TokenCountFormatter.FormatGiftUpper(count), Is.EqualTo(expected));
    }
}
