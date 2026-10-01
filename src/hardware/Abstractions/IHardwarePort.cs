using System;

namespace Exchanger.Hardware.Abstractions;

/// <summary>
/// Платформонезависимый транспорт контроллера вендингового аппарата.
/// </summary>
public interface IHardwarePort
{
    bool IsConnected { get; }

    void Connect(string portName, int baudRate);

    void Disconnect();

    void SendCommand(string command);

    /// <summary>
    /// Доставляет накопленные фоновым транспортом события в вызывающий поток.
    /// В Godot должен вызываться только из главного потока.
    /// </summary>
    void ProcessPendingEvents();

    event Action<string>? OnDataReceived;

    event Action<bool>? ConnectionChanged;
}
