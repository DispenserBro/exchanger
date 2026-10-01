using Exchanger.Hardware.Abstractions;
using Exchanger.Hardware.PeripheryController;

namespace Exchanger.Tests;

internal sealed class FakeHardwarePort : IHardwarePort
{
    public bool IsConnected { get; private set; }

    public List<string> SentCommands { get; } = new();

    public event Action<string>? OnDataReceived;

    public event Action<bool>? ConnectionChanged;

    public void Connect(string portName, int baudRate) => RaiseConnection(true);

    public void Disconnect() => RaiseConnection(false);

    public void SendCommand(string command)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException();
        }

        SentCommands.Add(command);
    }

    public void ProcessPendingEvents()
    {
    }

    public void RaiseConnection(bool connected)
    {
        IsConnected = connected;
        ConnectionChanged?.Invoke(connected);
    }

    public void RaiseData(string frame) => OnDataReceived?.Invoke(frame);
}

internal sealed class IdentityBlockCipher : IControllerBlockCipher
{
    public byte[] EncryptBlock(ReadOnlySpan<byte> plaintext) => plaintext.ToArray();
}
