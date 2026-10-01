using Exchanger.Hardware.Abstractions;
using Exchanger.Hardware.PeripheryController;
using Exchanger.Hardware.SerialPortHardware;
using NUnit.Framework;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Exchanger.HardwareTests;

internal sealed class BoardTestSession : IDisposable
{
    private readonly BoardTestConfiguration _configuration;
    private readonly Aes256EcbBlockCipher _cipher;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ConcurrentQueue<ObservedBoardMessage> _messages = new();
    private bool _disposed;

    public BoardTestSession(
        BoardTestConfiguration configuration,
        PeripheryDispenseMode dispenseMode = PeripheryDispenseMode.Unsupported)
    {
        _configuration = configuration;
        Port = new SerialPortHardware();
        _cipher = new Aes256EcbBlockCipher(PeripheryControllerSecurity.Key.ToArray());
        var authenticator = new ControllerAuthenticator(_cipher);
        Controller = new PeripheryMachineController(
            Port,
            authenticator,
            dispenseMode,
            stockSensorInputIndex: configuration.StockSensorInputIndex,
            stockLowWhenInputHigh: configuration.StockLowWhenInputHigh,
            handshakeTimeoutMilliseconds: (int)configuration.AuthorizationTimeout.TotalMilliseconds,
            serviceButtonInputIndex: PeripheryControllerInputs.ServiceButton,
            serviceButtonActiveHigh: false,
            hopper1DispenseSensorInputIndex: PeripheryControllerInputs.Hopper1DispensePulse,
            hopper1DispenseSensorActiveHigh: false,
            hopper2DispenseSensorInputIndex: PeripheryControllerInputs.Hopper2DispensePulse,
            hopper2DispenseSensorActiveHigh: false,
            inputPollMilliseconds: 100);
        Controller.ConnectionStateChanged += OnConnectionStateChanged;
        Port.OnDataReceived += OnDataReceived;
    }

    public SerialPortHardware Port { get; }

    public PeripheryMachineController Controller { get; }

    public List<MachineConnectionState> ConnectionStates { get; } = new();

    public void ConnectAndAuthorize()
    {
        TestContext.Progress.WriteLine($"Открытие {_configuration.PortName} @ {_configuration.BaudRate} и запуск handshake.");
        long openDeadline = _clock.ElapsedMilliseconds + 3_000;
        do
        {
            Port.Connect(_configuration.PortName, _configuration.BaudRate);
            if (Port.IsConnected)
            {
                break;
            }

            Thread.Sleep(100);
        }
        while (_clock.ElapsedMilliseconds < openDeadline);

        Assert.That(Port.IsConnected, Is.True, $"Не удалось открыть {_configuration.PortName} за 3 секунды.");
        WaitUntil(
            () => Controller.IsReady,
            _configuration.AuthorizationTimeout,
            "Контроллер не перешёл в Ready после полного handshake.");
        DrainAndClear(TimeSpan.FromMilliseconds(250));
    }

    public void DisconnectAndWait()
    {
        Port.Disconnect();
        WaitUntil(
            () => Controller.ConnectionState == MachineConnectionState.Disconnected,
            TimeSpan.FromSeconds(3),
            "Контроллер не опубликовал Disconnected после закрытия порта.");
    }

    public ObservedBoardMessage SendAndWaitForMessage(
        string command,
        Func<ObservedBoardMessage, bool> predicate,
        string failureMessage)
    {
        DrainAndClear(TimeSpan.FromMilliseconds(150));
        Port.SendCommand(command);
        return WaitForMessage(predicate, _configuration.ResponseTimeout, failureMessage);
    }

    public ObservedBoardMessage WaitForMessage(
        Func<ObservedBoardMessage, bool> predicate,
        TimeSpan timeout,
        string failureMessage)
    {
        ObservedBoardMessage matched = default;
        WaitUntil(
            () => TryTakeMessage(predicate, out matched),
            timeout,
            failureMessage);
        return matched;
    }

    public void WaitUntil(Func<bool> condition, TimeSpan timeout, string failureMessage)
    {
        long deadline = _clock.ElapsedMilliseconds + (long)timeout.TotalMilliseconds;
        while (_clock.ElapsedMilliseconds < deadline)
        {
            PumpOnce();
            if (condition())
            {
                return;
            }

            Thread.Sleep(10);
        }

        PumpOnce();
        Assert.That(condition(), Is.True, failureMessage);
    }

    public void DrainAndClear(TimeSpan duration)
    {
        long deadline = _clock.ElapsedMilliseconds + (long)duration.TotalMilliseconds;
        while (_clock.ElapsedMilliseconds < deadline)
        {
            PumpOnce();
            Thread.Sleep(10);
        }

        while (_messages.TryDequeue(out _))
        {
        }
    }

    public void PumpOnce()
    {
        Port.ProcessPendingEvents();
        Controller.Update((ulong)_clock.ElapsedMilliseconds);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Controller.ConnectionStateChanged -= OnConnectionStateChanged;
        Port.OnDataReceived -= OnDataReceived;
        Controller.Dispose();
        Port.Disconnect();
        Port.Dispose();
        _cipher.Dispose();
        _disposed = true;
    }

    private bool TryTakeMessage(
        Func<ObservedBoardMessage, bool> predicate,
        out ObservedBoardMessage matched)
    {
        while (_messages.TryDequeue(out ObservedBoardMessage candidate))
        {
            if (predicate(candidate))
            {
                matched = candidate;
                return true;
            }
        }

        matched = default;
        return false;
    }

    private void OnDataReceived(string frame)
    {
        PeripheryMessage parsed = PeripheryProtocolParser.Parse(frame);
        _messages.Enqueue(new ObservedBoardMessage(parsed.Kind, parsed.NumericValue, frame?.Length ?? 0));
    }

    private void OnConnectionStateChanged(MachineConnectionState state)
    {
        ConnectionStates.Add(state);
        TestContext.Progress.WriteLine($"Сервисное состояние контроллера: {state}.");
    }
}

internal readonly record struct ObservedBoardMessage(
    PeripheryMessageKind Kind,
    int NumericValue,
    int FrameLength);
