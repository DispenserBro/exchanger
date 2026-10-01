using Exchanger.Core.Logging;
using Exchanger.Hardware.Abstractions;
using System;

namespace Exchanger.Hardware.MockHardware;

/// <summary>
/// Детерминированная заглушка контроллера для разработки без физического COM-порта.
/// </summary>
public sealed class MockHardwarePort : IHardwarePort
{
    public bool IsConnected { get; private set; }

    public event Action<string>? OnDataReceived;

    public event Action<bool>? ConnectionChanged;

    public void Connect(string portName, int baudRate)
    {
        IsConnected = true;
        ConnectionChanged?.Invoke(true);
        AppLogger.Info("MockHardware", $"Подключена имитация порта {portName} @ {baudRate}.");
    }

    public void Disconnect()
    {
        if (!IsConnected)
        {
            return;
        }

        IsConnected = false;
        ConnectionChanged?.Invoke(false);
        AppLogger.Info("MockHardware", "Имитация порта отключена.");
    }

    public void SendCommand(string command)
    {
        string normalized = command.Trim();
        AppLogger.Info("MockHardware", $"Команда: {normalized}");

        if (!IsConnected)
        {
            OnDataReceived?.Invoke("ERROR:NOT_CONNECTED");
            return;
        }

        OnDataReceived?.Invoke(normalized.StartsWith("DISPENSE:", StringComparison.OrdinalIgnoreCase)
            ? "OK"
            : "OK");
    }

    public void ProcessPendingEvents()
    {
    }
}
