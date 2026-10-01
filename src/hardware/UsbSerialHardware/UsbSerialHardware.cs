using Exchanger.Core.Logging;
using Exchanger.Hardware.Abstractions;
using System;

namespace Exchanger.Hardware.UsbSerialHardware;

/// <summary>
/// Android-заглушка, сохраняющая платформенную границу до выбора USB-Serial плагина.
/// </summary>
public sealed class UsbSerialHardware : IHardwarePort
{
    public bool IsConnected => false;

    public event Action<string>? OnDataReceived;

    public event Action<bool>? ConnectionChanged;

    public void Connect(string portName, int baudRate)
    {
        AppLogger.Warning("UsbSerial", "Android USB-Serial пока не поддерживается.");
        ConnectionChanged?.Invoke(false);
    }

    public void Disconnect()
    {
        AppLogger.Info("UsbSerial", "Запрошено отключение Android-заглушки.");
    }

    public void SendCommand(string command)
    {
        AppLogger.Warning("UsbSerial", $"Команда не отправлена Android-заглушкой: {command.Trim()}");
        OnDataReceived?.Invoke("ERROR:USB_SERIAL_NOT_IMPLEMENTED");
    }

    public void ProcessPendingEvents()
    {
    }
}
