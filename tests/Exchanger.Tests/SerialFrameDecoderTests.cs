using Exchanger.Hardware.SerialPortHardware;
using NUnit.Framework;
using System.Text;

namespace Exchanger.Tests;

[TestFixture]
public sealed class SerialFrameDecoderTests
{
    [Test]
    public void CrLfPair_CompletesOneFrame()
    {
        var decoder = new SerialFrameDecoder();

        IReadOnlyList<string> frames = decoder.Push(Encoding.ASCII.GetBytes("READY\r\n"), 0);

        Assert.That(frames, Is.EqualTo(new[] { "READY" }));
    }

    [Test]
    public void SplitAndMultipleFrames_AreDecodedInOrder()
    {
        var decoder = new SerialFrameDecoder();

        Assert.That(decoder.Push(Encoding.ASCII.GetBytes("MONEY:BI"), 0), Is.Empty);
        IReadOnlyList<string> frames = decoder.Push(Encoding.ASCII.GetBytes("LL:50\nSERVICE\r"), 50);

        Assert.That(frames, Is.EqualTo(new[] { "MONEY:BILL:50", "SERVICE" }));
    }

    [Test]
    public void UnterminatedFrame_ExpiresAfterProtocolTimeout()
    {
        var decoder = new SerialFrameDecoder(timeoutMilliseconds: 100);
        decoder.Push(Encoding.ASCII.GetBytes("OLD"), 0);

        decoder.Expire(100);
        IReadOnlyList<string> frames = decoder.Push(Encoding.ASCII.GetBytes("READY\n"), 101);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(decoder.DroppedFrameCount, Is.EqualTo(1));
            Assert.That(frames, Is.EqualTo(new[] { "READY" }));
        }
    }

    [Test]
    public void OneCharacterAndOversizedFrames_AreRejected()
    {
        var decoder = new SerialFrameDecoder(maximumLength: 4);

        IReadOnlyList<string> frames = decoder.Push(Encoding.ASCII.GetBytes("0\n1234567\nOK\n"), 0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(frames, Is.EqualTo(new[] { "OK" }));
            Assert.That(decoder.DroppedFrameCount, Is.GreaterThanOrEqualTo(2));
        }
    }

    [Test]
    public void InvalidFrameWithoutTerminator_RecoversAfterTimeout()
    {
        var decoder = new SerialFrameDecoder(timeoutMilliseconds: 100);
        decoder.Push(new byte[] { 0xFF, (byte)'X' }, 0);

        decoder.Expire(100);
        IReadOnlyList<string> frames = decoder.Push(Encoding.ASCII.GetBytes("READY\n"), 101);

        Assert.That(frames, Is.EqualTo(new[] { "READY" }));
    }
}
