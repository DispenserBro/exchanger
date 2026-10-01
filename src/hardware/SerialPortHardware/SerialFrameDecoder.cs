using System;
using System.Collections.Generic;
using System.Text;

namespace Exchanger.Hardware.SerialPortHardware;

/// <summary>
/// Потоковый ASCII-фреймер протокола 2.3.5. CR и LF независимо завершают
/// кадр; пустая половина CRLF игнорируется.
/// </summary>
public sealed class SerialFrameDecoder
{
    private readonly int _maximumLength;
    private readonly int _timeoutMilliseconds;
    private readonly List<byte> _buffer = new();
    private long _lastByteAtMilliseconds = -1;
    private bool _discardUntilTerminator;

    public SerialFrameDecoder(int maximumLength = 1024, int timeoutMilliseconds = 100)
    {
        _maximumLength = Math.Clamp(maximumLength, 2, 65536);
        _timeoutMilliseconds = Math.Clamp(timeoutMilliseconds, 10, 5000);
    }

    public int DroppedFrameCount { get; private set; }

    public IReadOnlyList<string> Push(ReadOnlySpan<byte> bytes, long nowMilliseconds)
    {
        Expire(nowMilliseconds);
        var frames = new List<string>();

        foreach (byte value in bytes)
        {
            if (value is 0x0D or 0x0A)
            {
                if (_discardUntilTerminator)
                {
                    Reset();
                }
                else
                {
                    CompleteFrame(frames);
                }

                continue;
            }

            _lastByteAtMilliseconds = nowMilliseconds;
            if (_discardUntilTerminator)
            {
                continue;
            }

            if (value > 0x7F || _buffer.Count >= _maximumLength)
            {
                if (_buffer.Count == 0)
                {
                    DroppedFrameCount++;
                }
                else
                {
                    DropCurrentFrame();
                }

                _discardUntilTerminator = true;
                _lastByteAtMilliseconds = nowMilliseconds;
                continue;
            }

            _buffer.Add(value);
        }

        return frames;
    }

    public void Expire(long nowMilliseconds)
    {
        if ((_buffer.Count == 0 && !_discardUntilTerminator) || _lastByteAtMilliseconds < 0)
        {
            return;
        }

        if (nowMilliseconds - _lastByteAtMilliseconds >= _timeoutMilliseconds)
        {
            if (_discardUntilTerminator)
            {
                Reset();
            }
            else
            {
                DropCurrentFrame();
            }
        }
    }

    public void Reset()
    {
        _buffer.Clear();
        _lastByteAtMilliseconds = -1;
        _discardUntilTerminator = false;
    }

    private void CompleteFrame(List<string> frames)
    {
        if (_buffer.Count >= 2)
        {
            frames.Add(Encoding.ASCII.GetString(_buffer.ToArray()));
        }
        else if (_buffer.Count > 0)
        {
            DroppedFrameCount++;
        }

        _buffer.Clear();
        _lastByteAtMilliseconds = -1;
    }

    private void DropCurrentFrame()
    {
        if (_buffer.Count > 0)
        {
            DroppedFrameCount++;
        }

        _buffer.Clear();
        _lastByteAtMilliseconds = -1;
    }
}
