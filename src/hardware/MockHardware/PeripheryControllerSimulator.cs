using Exchanger.Hardware.Abstractions;
using Exchanger.Hardware.PeripheryController;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Exchanger.Hardware.MockHardware;

/// <summary>
/// Детерминированный wire-симулятор документированной части протокола 2.3.5.
/// Прогоняет настоящий handshake и ASCII parser, но не предполагает форматы,
/// отсутствующие в спецификации производителя.
/// </summary>
public sealed class PeripheryControllerSimulator : IHardwarePort
{
    private static readonly byte[] ControllerChallenge =
        Convert.FromHexString("FFEEDDCCBBAA99887766554433221100");

    private readonly IControllerBlockCipher _cipher;
    private readonly Queue<string> _incomingFrames = new();
    private string _expectedChallengeResponse = string.Empty;
    private string _controllerProof = string.Empty;
    private bool _failNextDispense;
    private int _confirmedPulsesBeforeFailure;

    public PeripheryControllerSimulator(IControllerBlockCipher cipher)
    {
        _cipher = cipher ?? throw new ArgumentNullException(nameof(cipher));
    }

    public bool IsConnected { get; private set; }

    public int DigitalInputMask { get; set; } = 67_108_863;

    public List<string> SentCommands { get; } = new();

    public event Action<string>? OnDataReceived;

    public event Action<bool>? ConnectionChanged;

    public void Connect(string portName, int baudRate)
    {
        if (IsConnected)
        {
            return;
        }

        IsConnected = true;
        ConnectionChanged?.Invoke(true);
        QueueFrame("periphery controller ver_simulator");
    }

    public void Disconnect()
    {
        if (!IsConnected)
        {
            return;
        }

        IsConnected = false;
        _incomingFrames.Clear();
        _expectedChallengeResponse = string.Empty;
        _controllerProof = string.Empty;
        ConnectionChanged?.Invoke(false);
    }

    public void SendCommand(string command)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Wire-симулятор не подключён.");
        }

        string normalized = (command ?? string.Empty).Trim();
        SentCommands.Add(normalized);

        if (TryHandleAuthenticationRequest(normalized)
            || TryHandleAuthenticationResponse(normalized))
        {
            return;
        }

        if (normalized == PeripheryProtocolCommands.QueryDigitalInputs)
        {
            QueueFrame($"INPUT {DigitalInputMask.ToString(CultureInfo.InvariantCulture)}");
            return;
        }

        if (normalized == PeripheryProtocolCommands.QueryHopper1Credit)
        {
            QueueFrame("HOPPER1 CREDIT=0");
            return;
        }

        if (normalized.StartsWith("309", StringComparison.Ordinal)
            && int.TryParse(normalized[3..], NumberStyles.None, CultureInfo.InvariantCulture, out int count))
        {
            if (count is >= 1 and <= PeripheryProtocolCommands.MaxHopperDispenseBatch)
            {
                QueueDispenseResult(count);
            }
            else
            {
                QueueFrame("ERROR:30:invalid count (0-99)");
            }

            return;
        }

        if (normalized == PeripheryProtocolCommands.QueryHopper2Credit)
        {
            QueueFrame("HOPPER2 CREDIT=0");
            return;
        }

        if (normalized.StartsWith("409", StringComparison.Ordinal)
            && int.TryParse(normalized[3..], NumberStyles.None, CultureInfo.InvariantCulture, out int hopper2Count))
        {
            if (hopper2Count is >= 1 and <= PeripheryProtocolCommands.MaxHopperDispenseBatch)
            {
                QueueHopper2DispenseResult(hopper2Count);
            }
            else
            {
                QueueFrame("ERROR:40:invalid count (0-99)");
            }
        }
    }

    public void ProcessPendingEvents()
    {
        while (_incomingFrames.Count > 0 && IsConnected)
        {
            string frame = _incomingFrames.Dequeue();
            OnDataReceived?.Invoke(frame);
        }
    }

    public void ReportCoins(int amountRubles) => QueueMoney("COINS", amountRubles);

    public void ReportBill(int amountRubles) => QueueMoney("BILL", amountRubles);

    public void ReportCashless(int amountRubles) => QueueMoney("CASHLESS", amountRubles);

    public void ReportServiceRequest() => QueueFrame("SERVICE");

    public void InjectFrame(string frame) => QueueFrame(frame);

    public void FailNextDispenseAfter(int confirmedPulses = 0)
    {
        _failNextDispense = true;
        _confirmedPulsesBeforeFailure = Math.Max(0, confirmedPulses);
    }

    private bool TryHandleAuthenticationRequest(string command)
    {
        if (command.Length != 34 || !command.StartsWith("07", StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryParseHexBlock(command[2..], out byte[] hostChallenge))
        {
            QueueFrame("ERROR:AUTH_FORMAT");
            return true;
        }

        QueueFrame("CHAL" + Convert.ToHexString(ControllerChallenge));
        _expectedChallengeResponse = "08" + Convert.ToHexString(_cipher.EncryptBlock(ControllerChallenge));
        _controllerProof = "RAES" + Convert.ToHexString(_cipher.EncryptBlock(hostChallenge));
        return true;
    }

    private bool TryHandleAuthenticationResponse(string command)
    {
        if (!command.StartsWith("08", StringComparison.Ordinal))
        {
            return false;
        }

        if (_expectedChallengeResponse.Length > 0
            && command.Equals(_expectedChallengeResponse, StringComparison.Ordinal))
        {
            _expectedChallengeResponse = string.Empty;
            QueueFrame(_controllerProof);
            QueueFrame("AUTH_PC_OK");
            QueueFrame("READY!");
            _controllerProof = string.Empty;
        }
        else
        {
            QueueFrame("ERROR:AUTH_PROOF");
        }

        return true;
    }

    private void QueueDispenseResult(int requestedCount)
    {
        QueueFrame($"HOPPER1 APPEND={requestedCount.ToString(CultureInfo.InvariantCulture)}");
        if (!_failNextDispense)
        {
            for (int index = 0; index < requestedCount; index++)
            {
                QueueFrame("HOPPER1 OK");
            }

            return;
        }

        int confirmed = Math.Min(requestedCount, _confirmedPulsesBeforeFailure);
        for (int index = 0; index < confirmed; index++)
        {
            QueueFrame("HOPPER1 OK");
        }

        QueueFrame("HOPPER1 ERROR");
        QueueFrame($"HOPPER1 CREDIT={(requestedCount - confirmed).ToString(CultureInfo.InvariantCulture)}");
        _failNextDispense = false;
        _confirmedPulsesBeforeFailure = 0;
    }

    private void QueueHopper2DispenseResult(int requestedCount)
    {
        QueueFrame($"HOPPER2 APPEND={requestedCount.ToString(CultureInfo.InvariantCulture)}");
        if (!_failNextDispense)
        {
            for (int index = 0; index < requestedCount; index++)
            {
                QueueFrame("HOPPER2 OK");
            }

            return;
        }

        int confirmed = Math.Min(requestedCount, _confirmedPulsesBeforeFailure);
        for (int index = 0; index < confirmed; index++)
        {
            QueueFrame("HOPPER2 OK");
        }

        QueueFrame("HOPPER2 ERROR");
        QueueFrame($"HOPPER2 CREDIT={(requestedCount - confirmed).ToString(CultureInfo.InvariantCulture)}");
        _failNextDispense = false;
        _confirmedPulsesBeforeFailure = 0;
    }

    private void QueueMoney(string source, int amountRubles)
    {
        if (amountRubles <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amountRubles));
        }

        QueueFrame($"MONEY:{source}:{amountRubles.ToString(CultureInfo.InvariantCulture)}");
    }

    private void QueueFrame(string frame)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Wire-симулятор не подключён.");
        }

        _incomingFrames.Enqueue(frame ?? string.Empty);
    }

    private static bool TryParseHexBlock(string value, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromHexString(value);
            return bytes.Length == 16;
        }
        catch (FormatException)
        {
            bytes = Array.Empty<byte>();
            return false;
        }
    }
}
