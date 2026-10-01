using Exchanger.Core.Logging;
using Exchanger.Hardware.Abstractions;
using System;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Threading;

namespace Exchanger.Hardware.SerialPortHardware;

/// <summary>
/// Desktop serial-транспорт Windows/Linux: 8N1, без flow control, один worker для
/// последовательного чтения/записи и очередь доставки событий в главный поток.
/// </summary>
public sealed class SerialPortHardware : IHardwarePort, IDisposable
{
    private readonly ConcurrentQueue<string> _outgoing = new();
    private readonly ConcurrentQueue<PortEvent> _pendingEvents = new();
    private readonly object _lifecycleLock = new();
    private readonly int _maximumFrameLength;
    private readonly int _frameTimeoutMilliseconds;
    private SerialPort? _serialPort;
    private CancellationTokenSource? _cancellation;
    private Thread? _worker;
    private volatile bool _isConnected;
    private bool _disposed;

    public SerialPortHardware(int maximumFrameLength = 1024, int frameTimeoutMilliseconds = 100)
    {
        _maximumFrameLength = Math.Clamp(maximumFrameLength, 2, 65536);
        _frameTimeoutMilliseconds = Math.Clamp(frameTimeoutMilliseconds, 10, 5000);
    }

    public bool IsConnected => _isConnected;

    public event Action<string>? OnDataReceived;

    public event Action<bool>? ConnectionChanged;

    public void Connect(string portName, int baudRate)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(portName))
        {
            throw new ArgumentException("Имя последовательного порта не задано.", nameof(portName));
        }

        lock (_lifecycleLock)
        {
            AppLogger.Debug(
                "SerialPort",
                $"Запрошено открытие: platform={GetPlatformName()}, port={portName.Trim()}, "
                + $"baud={baudRate}, frameTimeoutMs={_frameTimeoutMilliseconds}, "
                + $"maxFrameLength={_maximumFrameLength}.");
            DisconnectUnsafe();

            var port = new SerialPort(portName.Trim(), baudRate, Parity.None, 8, StopBits.One)
            {
                Handshake = Handshake.None,
                Encoding = System.Text.Encoding.ASCII,
                NewLine = "\r\n",
                ReadTimeout = 25,
                WriteTimeout = 500,
            };

            try
            {
                AppLogger.Debug("SerialPort", "Вызвано системное открытие последовательного устройства.");
                port.Open();
                AppLogger.Debug("SerialPort", "Устройство открыто; начинается аппаратный reset контроллера.");
                ResetControllerForStartup(port);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.IO.IOException)
            {
                port.Dispose();
                _pendingEvents.Enqueue(PortEvent.Disconnected);
                AppLogger.Debug(
                    "SerialPort",
                    $"Открытие завершилось ошибкой: reason={DescribeConnectionFailure(exception)}, "
                    + $"exception={exception.GetType().Name}, hresult=0x{exception.HResult:X8}.");
                AppLogger.Error("SerialPort", $"Не удалось открыть {portName}: {exception.GetType().Name}.");
                return;
            }

            _serialPort = port;
            _cancellation = new CancellationTokenSource();
            _isConnected = true;
            _pendingEvents.Enqueue(PortEvent.Connected);
            _worker = new Thread(() => WorkerLoop(port, _cancellation.Token))
            {
                IsBackground = true,
                Name = "Exchanger.SerialPort",
            };
            _worker.Start();
            AppLogger.Debug(
                "SerialPort",
                $"Worker запущен: managedThreadId={_worker.ManagedThreadId}, background={_worker.IsBackground}.");
            AppLogger.Info("SerialPort", $"Открыт {port.PortName} @ {port.BaudRate}, 8N1, flow control None.");
        }
    }

    public void Disconnect()
    {
        lock (_lifecycleLock)
        {
            DisconnectUnsafe();
        }
    }

    public void SendCommand(string command)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        string normalized = (command ?? string.Empty).TrimEnd('\r', '\n');
        if (normalized.Length < 2)
        {
            throw new ArgumentException("Команда контроллера должна содержать минимум два символа.", nameof(command));
        }

        if (!_isConnected)
        {
            throw new InvalidOperationException("Последовательный порт не подключён.");
        }

        _outgoing.Enqueue(normalized);
    }

    public void ProcessPendingEvents()
    {
        while (_pendingEvents.TryDequeue(out PortEvent portEvent))
        {
            if (portEvent.ConnectionState.HasValue)
            {
                ConnectionChanged?.Invoke(portEvent.ConnectionState.Value);
            }
            else if (portEvent.Frame is not null)
            {
                OnDataReceived?.Invoke(portEvent.Frame);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Disconnect();
        _disposed = true;
    }

    private void WorkerLoop(SerialPort port, CancellationToken cancellationToken)
    {
        var decoder = new SerialFrameDecoder(_maximumFrameLength, _frameTimeoutMilliseconds);
        var buffer = new byte[256];
        long receivedBytes = 0;
        long decodedFrames = 0;
        long sentCommands = 0;

        try
        {
            AppLogger.Debug("SerialPort", "Worker вошёл в цикл чтения и записи.");
            while (!cancellationToken.IsCancellationRequested)
            {
                while (_outgoing.TryDequeue(out string? command))
                {
                    port.Write(command + "\r\n");
                    sentCommands++;
                    if (command.StartsWith("07", StringComparison.Ordinal)
                        || command.StartsWith("08", StringComparison.Ordinal))
                    {
                        AppLogger.Debug(
                            "SerialPort",
                            $"Отправлен скрытый кадр авторизации: kind={DescribeAuthenticationCommand(command)}, "
                            + $"length={command.Length}.");
                    }
                }

                try
                {
                    int read = port.Read(buffer, 0, buffer.Length);
                    receivedBytes += read;
                    long now = Environment.TickCount64;
                    foreach (string frame in decoder.Push(buffer.AsSpan(0, read), now))
                    {
                        decodedFrames++;
                        _pendingEvents.Enqueue(PortEvent.Data(frame));
                    }
                }
                catch (TimeoutException)
                {
                    decoder.Expire(Environment.TickCount64);
                }
            }
        }
        catch (Exception exception) when (IsWorkerTerminationException(exception))
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                AppLogger.Debug(
                    "SerialPort",
                    $"Worker остановлен транспортной ошибкой: reason={DescribeConnectionFailure(exception)}, "
                    + $"exception={exception.GetType().Name}, hresult=0x{exception.HResult:X8}.");
            }
        }
        finally
        {
            AppLogger.Debug(
                "SerialPort",
                $"Worker завершён: cancelled={cancellationToken.IsCancellationRequested}, "
                + $"sentCommands={sentCommands}, receivedBytes={receivedBytes}, decodedFrames={decodedFrames}.");
            _isConnected = false;
            _pendingEvents.Enqueue(PortEvent.Disconnected);
        }
    }

    internal static bool IsWorkerTerminationException(Exception exception) =>
        exception is OperationCanceledException
            or InvalidOperationException
            or UnauthorizedAccessException
            or System.IO.IOException;

    private static void ResetControllerForStartup(SerialPort port)
    {
        // Актуальная прошивка публикует обязательный startup-banner только после
        // перезагрузки. Последовательность совпадает с Plate Config App:
        // очистить старый ввод, DTR=0, импульс RTS и затем начать чтение.
        port.DiscardInBuffer();
        port.DtrEnable = false;
        port.RtsEnable = true;
        AppLogger.Debug("SerialPort", "Reset: входной буфер очищен, DTR=0, RTS=1 на 100 мс.");
        try
        {
            Thread.Sleep(100);
        }
        finally
        {
            port.RtsEnable = false;
            AppLogger.Debug("SerialPort", "Reset: RTS=0; транспорт готов принимать startup-banner.");
        }
    }

    private void DisconnectUnsafe()
    {
        CancellationTokenSource? cancellation = _cancellation;
        Thread? worker = _worker;
        SerialPort? port = _serialPort;
        bool hadTransport = cancellation is not null || worker is not null || port is not null;

        if (hadTransport)
        {
            AppLogger.Debug(
                "SerialPort",
                $"Начато отключение: isOpen={port?.IsOpen == true}, workerAlive={worker?.IsAlive == true}.");
        }

        _cancellation = null;
        _worker = null;
        _serialPort = null;
        _isConnected = false;
        cancellation?.Cancel();

        try
        {
            port?.Close();
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.IO.IOException)
        {
            AppLogger.Warning("SerialPort", $"Ошибка закрытия последовательного порта: {exception.GetType().Name}.");
        }

        if (worker is not null && worker.IsAlive && !worker.Join(2000))
        {
            AppLogger.Warning("SerialPort", "Worker последовательного порта не завершился за 2 секунды.");
        }

        port?.Dispose();
        cancellation?.Dispose();
        while (_outgoing.TryDequeue(out _))
        {
        }

        if (hadTransport)
        {
            AppLogger.Debug("SerialPort", "Отключение завершено; очередь исходящих команд очищена.");
        }
    }

    private static string GetPlatformName() =>
        OperatingSystem.IsWindows()
            ? "Windows"
            : OperatingSystem.IsLinux()
                ? "Linux"
                : "Unsupported";

    private static string DescribeAuthenticationCommand(string command) =>
        command.StartsWith("07", StringComparison.Ordinal)
            ? "HostChallenge"
            : "ControllerChallengeResponse";

    private static string DescribeConnectionFailure(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "AccessDeniedOrPortBusy",
        ArgumentException => "InvalidPortOrParameters",
        InvalidOperationException => "InvalidTransportState",
        System.IO.IOException => "DeviceMissingOrIoFailure",
        OperationCanceledException => "Cancelled",
        _ => "Unknown",
    };

    private readonly record struct PortEvent(string? Frame, bool? ConnectionState)
    {
        public static PortEvent Connected { get; } = new(null, true);

        public static PortEvent Disconnected { get; } = new(null, false);

        public static PortEvent Data(string frame) => new(frame, null);

    }
}
