using Exchanger.Core.Session;
using Exchanger.Hardware.Abstractions;
using Exchanger.Hardware.PeripheryController;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class PeripheryMachineControllerTests
{
    private static readonly byte[] HostChallenge = Convert.FromHexString("00112233445566778899AABBCCDDEEFF");

    [Test]
    public void CashEvents_AreAcknowledgedAndAggregated()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.Hopper1Auto);
        var balances = new List<int>();
        controller.CashBalanceChanged += balances.Add;
        int commandCount = port.SentCommands.Count;

        controller.BeginCashAcceptance();
        port.RaiseData("MONEY:BILL:50");
        port.RaiseData("MONEY:COINS:10");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(balances, Is.EqualTo(new[] { 0, 50, 60 }));
            Assert.That(port.SentCommands, Does.Contain("ASK:BILL:50"));
            Assert.That(port.SentCommands, Does.Contain("ASK:COINS:10"));
            Assert.That(port.SentCommands, Does.Contain("151"));
            Assert.That(
                port.SentCommands.Skip(commandCount).Take(3),
                Is.EqualTo(new[] { "162", "141", "151" }));
        }
    }

    [Test]
    public void ReadyInitialization_EntersSmartModeDisablesPaymentChannelsAndSetsDefaultLighting()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.Hopper1Auto);

        Assert.That(
            port.SentCommands.TakeLast(6),
            Is.EqualTo(new[] { "141", "142", "152", "162", "856501", "2171" }));
    }

    [Test]
    public void Dispense_SetsDispenseLightingBeforeHopperCommand()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.Hopper1Auto);
        int commandCount = port.SentCommands.Count;

        controller.DispenseTokens(1);

        Assert.That(
            port.SentCommands.Skip(commandCount),
            Is.EqualTo(new[] { "856540", "2172", "162", "3091" }));
    }

    [Test]
    public void Dispense_AuxiliaryLightingPulsesWithinConfiguredIntervalAndReturnsToDefaultAfterTenSeconds()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.Hopper1Auto);
        controller.Update(0);
        controller.DispenseTokens(1);
        int initialPulseCount = port.SentCommands.Count(command => command == "2172");

        controller.Update(199);
        Assert.That(port.SentCommands.Count(command => command == "2172"), Is.EqualTo(initialPulseCount));

        controller.Update(500);
        Assert.That(port.SentCommands.Count(command => command == "2172"), Is.EqualTo(initialPulseCount + 1));

        controller.Update(10_000);
        Assert.That(port.SentCommands.Last(), Is.EqualTo("2171"));
    }

    [Test]
    public void Dispose_DisablesLightingOnceBeforeTransportDisconnect()
    {
        var port = new FakeHardwarePort();
        var controller = CreateReadyController(port, PeripheryDispenseMode.Hopper1Auto);

        controller.Dispose();
        controller.Dispose();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(port.SentCommands.TakeLast(2), Is.EqualTo(new[] { "856500", "2170" }));
            Assert.That(port.SentCommands.Count(command => command == "856500"), Is.EqualTo(1));
            Assert.That(port.SentCommands.Count(command => command == "2170"), Is.EqualTo(1));
        }
    }

    [Test]
    public void CashCancel_DisablesOnlyActiveCashChannelsOnce()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.Hopper1Auto);
        controller.BeginCashAcceptance();
        int commandCount = port.SentCommands.Count;

        controller.CancelActiveOperation();
        controller.EndCashAcceptance();
        controller.CancelActiveOperation();

        Assert.That(
            port.SentCommands.Skip(commandCount),
            Is.EqualTo(new[] { "142", "152" }));
    }

    [Test]
    public void CashlessEvent_IsAcknowledgedAndApprovesExactRequestedAmount()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.Hopper1Auto);
        MachineCardResult? result = null;
        controller.CardPaymentFinished += value => result = value;
        int commandCount = port.SentCommands.Count;

        controller.BeginCardPayment(150);
        port.RaiseData("MONEY:CASHLESS:150");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(MachineCardResult.Approved));
            Assert.That(port.SentCommands, Does.Contain("163150"));
            Assert.That(port.SentCommands, Does.Contain("ASK:CASHLESS:150"));
            Assert.That(port.SentCommands.Last(), Is.EqualTo("162"));
            Assert.That(
                port.SentCommands.Skip(commandCount).Take(4),
                Is.EqualTo(new[] { "142", "152", "161", "163150" }));
        }
    }

    [Test]
    public void CardCancel_DisablesCashlessOnlyOnce()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.Hopper1Auto);
        controller.BeginCardPayment(150);
        int commandCount = port.SentCommands.Count;

        controller.CancelActiveOperation();
        controller.CancelActiveOperation();

        Assert.That(port.SentCommands.Skip(commandCount), Is.EqualTo(new[] { "162" }));
    }

    [Test]
    public void CashlessAmountMismatch_IsAcknowledgedButDeclined()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.Hopper1Auto);
        MachineCardResult? result = null;
        controller.CardPaymentFinished += value => result = value;
        controller.BeginCardPayment(150);

        port.RaiseData("MONEY:CASHLESS:100");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(MachineCardResult.Declined));
            Assert.That(port.SentCommands, Does.Contain("ASK:CASHLESS:100"));
        }
    }

    [Test]
    public void CashlessKeepAlive_IsSentBeforeControllerDeadline()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.Hopper1Auto, keepAliveSeconds: 120);
        controller.BeginCardPayment(100);
        int initialEnableCount = port.SentCommands.Count(command => command == "161");

        controller.Update(1_000);
        controller.Update(120_999);
        controller.Update(121_000);

        Assert.That(port.SentCommands.Count(command => command == "161"), Is.EqualTo(initialEnableCount + 1));
    }

    [Test]
    public void CashlessKeepAlive_IsSentWhileIdleAndPausedDuringCashAcceptance()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.Hopper1Auto, keepAliveSeconds: 120);
        int initialEnableCount = port.SentCommands.Count(command => command == "161");

        controller.Update(1_000);
        controller.Update(121_000);
        Assert.That(port.SentCommands.Count(command => command == "161"), Is.EqualTo(initialEnableCount + 2));

        controller.BeginCashAcceptance();
        controller.Update(300_000);
        Assert.That(port.SentCommands.Count(command => command == "161"), Is.EqualTo(initialEnableCount + 2));

        controller.EndCashAcceptance();
        controller.Update(300_001);
        Assert.That(port.SentCommands.Count(command => command == "161"), Is.EqualTo(initialEnableCount + 3));
    }

    [Test]
    public void DisconnectDuringPayment_RaisesConnectionLost()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.Hopper1Auto);
        MachineFault? fault = null;
        controller.FaultOccurred += value => fault = value;
        controller.BeginCardPayment(100);

        port.RaiseConnection(false);

        Assert.That(fault, Is.EqualTo(MachineFault.ConnectionLost));
    }

    [Test]
    public void BusinessFramesBeforeReady_AreIgnoredWithoutAcknowledgement()
    {
        var port = new FakeHardwarePort();
        var authenticator = new ControllerAuthenticator(new IdentityBlockCipher(), () => HostChallenge);
        using var controller = new PeripheryMachineController(port, authenticator, PeripheryDispenseMode.Hopper1Auto);
        bool serviceRequested = false;
        controller.ServiceRequested += () => serviceRequested = true;
        port.RaiseConnection(true);
        int commandsAfterAuthRequest = port.SentCommands.Count;

        port.RaiseData("MONEY:BILL:50");
        port.RaiseData("SERVICE");
        port.RaiseData("READY");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(port.SentCommands.Count, Is.EqualTo(commandsAfterAuthRequest));
            Assert.That(port.SentCommands, Does.Not.Contain("ASK:BILL:50"));
            Assert.That(serviceRequested, Is.False);
            Assert.That(controller.IsReady, Is.False);
        }
    }

    [Test]
    public void ConnectionWaitsForStartupBannerAndFullTwoSecondDelayBeforeSending07()
    {
        var port = new FakeHardwarePort();
        var authenticator = new ControllerAuthenticator(new IdentityBlockCipher(), () => HostChallenge);
        using var controller = new PeripheryMachineController(port, authenticator);
        port.RaiseConnection(true);

        controller.Update(0);
        controller.Update(19_999);
        Assert.That(port.SentCommands, Is.Empty);

        port.RaiseData("periphery controller ver_test");
        controller.Update(20_000);
        controller.Update(21_999);
        Assert.That(port.SentCommands, Is.Empty);

        controller.Update(22_000);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(port.SentCommands, Has.Count.EqualTo(1));
            Assert.That(port.SentCommands[0], Does.StartWith("07"));
            Assert.That(port.SentCommands[0], Has.Length.EqualTo(34));
        }
    }

    [Test]
    public void MissingChallenge_FailsAfterStartupDelayAndHandshakeTimeout()
    {
        var port = new FakeHardwarePort();
        var authenticator = new ControllerAuthenticator(new IdentityBlockCipher(), () => HostChallenge);
        using var controller = new PeripheryMachineController(
            port,
            authenticator,
            PeripheryDispenseMode.Hopper1Auto,
            handshakeTimeoutMilliseconds: 5000);
        MachineFault? fault = null;
        controller.FaultOccurred += value => fault = value;
        port.RaiseConnection(true);
        port.RaiseData("periphery controller ver_test");
        controller.Update(1_000);
        controller.Update(3_000);
        controller.Update(3_001);
        controller.Update(8_000);
        Assert.That(fault, Is.Null);
        controller.Update(8_001);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fault, Is.EqualTo(MachineFault.Unavailable));
            Assert.That(controller.AuthenticationState, Is.EqualTo(ControllerAuthenticationState.Failed));
            Assert.That(controller.IsReady, Is.False);
            Assert.That(port.IsConnected, Is.False);
        }
    }

    [Test]
    public void MissingProof_FailsAtOverallHandshakeTimeout()
    {
        var port = new FakeHardwarePort();
        var authenticator = new ControllerAuthenticator(new IdentityBlockCipher(), () => HostChallenge);
        using var controller = new PeripheryMachineController(
            port,
            authenticator,
            PeripheryDispenseMode.Hopper1Auto,
            handshakeTimeoutMilliseconds: 5000);
        MachineFault? fault = null;
        controller.FaultOccurred += value => fault = value;
        port.RaiseConnection(true);
        port.RaiseData("periphery controller ver_test");
        controller.Update(1_000);
        controller.Update(3_000);
        port.RaiseData("CHALFFEEDDCCBBAA99887766554433221100");
        controller.Update(3_001);
        controller.Update(8_000);
        Assert.That(fault, Is.Null);
        controller.Update(8_001);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fault, Is.EqualTo(MachineFault.Unavailable));
            Assert.That(controller.AuthenticationState, Is.EqualTo(ControllerAuthenticationState.Failed));
        }
    }

    [Test]
    public void ValidControllerProof_RequiresExactReadyBang()
    {
        var port = new FakeHardwarePort();
        var authenticator = new ControllerAuthenticator(new IdentityBlockCipher(), () => HostChallenge);
        using var controller = new PeripheryMachineController(
            port,
            authenticator,
            PeripheryDispenseMode.Hopper1Auto,
            handshakeTimeoutMilliseconds: 5000);
        MachineFault? fault = null;
        controller.FaultOccurred += value => fault = value;
        port.RaiseConnection(true);
        port.RaiseData("periphery controller ver_test");
        controller.Update(1_000);
        controller.Update(3_000);
        port.RaiseData("CHALFFEEDDCCBBAA99887766554433221100");
        port.RaiseData("RAES00112233445566778899AABBCCDDEEFF");
        port.RaiseData("AUTH_PC_OK");
        port.RaiseData("AUTH_CNT_OK");
        port.RaiseData("READY");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fault, Is.Null);
            Assert.That(controller.AuthenticationState, Is.EqualTo(ControllerAuthenticationState.AwaitingReady));
            Assert.That(controller.IsReady, Is.False);
        }

        port.RaiseData("READY!");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fault, Is.Null);
            Assert.That(controller.AuthenticationState, Is.EqualTo(ControllerAuthenticationState.Ready));
            Assert.That(controller.IsReady, Is.True);
            Assert.That(port.IsConnected, Is.True);
        }
    }

    [Test]
    public void InvalidControllerProof_FailsAndClosesPortImmediately()
    {
        var port = new FakeHardwarePort();
        var authenticator = new ControllerAuthenticator(new IdentityBlockCipher(), () => HostChallenge);
        using var controller = new PeripheryMachineController(port, authenticator, PeripheryDispenseMode.Hopper1Auto);
        var faults = new List<MachineFault>();
        controller.FaultOccurred += faults.Add;
        port.RaiseConnection(true);
        port.RaiseData("periphery controller ver_test");
        controller.Update(1_000);
        controller.Update(3_000);
        port.RaiseData("CHALFFEEDDCCBBAA99887766554433221100");
        port.RaiseData("RAESFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(faults, Is.EqualTo(new[] { MachineFault.Unavailable }));
            Assert.That(controller.AuthenticationState, Is.EqualTo(ControllerAuthenticationState.Failed));
            Assert.That(controller.IsReady, Is.False);
            Assert.That(port.IsConnected, Is.False);
        }
    }

    [Test]
    public void Disconnect_CancelsPendingHandshakeTimeout()
    {
        var port = new FakeHardwarePort();
        var authenticator = new ControllerAuthenticator(new IdentityBlockCipher(), () => HostChallenge);
        using var controller = new PeripheryMachineController(
            port,
            authenticator,
            PeripheryDispenseMode.Hopper1Auto,
            handshakeTimeoutMilliseconds: 5000);
        var faults = new List<MachineFault>();
        controller.FaultOccurred += faults.Add;
        port.RaiseConnection(true);
        controller.Update(1_000);

        port.RaiseConnection(false);
        controller.Update(100_000);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(faults, Is.EqualTo(new[] { MachineFault.Unavailable }));
            Assert.That(controller.AuthenticationState, Is.EqualTo(ControllerAuthenticationState.NotStarted));
        }
    }

    [Test]
    public void ServiceAndGenericControllerError_AreTypedAfterReady()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.Hopper1Auto);
        bool serviceRequested = false;
        MachineFault? fault = null;
        controller.ServiceRequested += () => serviceRequested = true;
        controller.FaultOccurred += value => fault = value;

        port.RaiseData("SERVICE");
        port.RaiseData("ERROR:DEVICE_FAILURE");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(serviceRequested, Is.True);
            Assert.That(fault, Is.EqualTo(MachineFault.Unavailable));
        }
    }

    [Test]
    public void ServiceInput_RisingEdgePublishesOneRequestPerPress()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(
            port,
            PeripheryDispenseMode.Hopper1Auto,
            serviceButtonInputIndex: PeripheryControllerInputs.ServiceButton,
            serviceButtonActiveHigh: false);
        int requestCount = 0;
        controller.ServiceRequested += () => requestCount++;
        int inactiveMask = 67_108_863;
        int pressedMask = inactiveMask & ~(1 << PeripheryControllerInputs.ServiceButton);

        port.RaiseData($"INPUT{inactiveMask}");
        port.RaiseData($"INPUT{pressedMask}");
        port.RaiseData($"INPUT{pressedMask}");
        port.RaiseData($"INPUT{inactiveMask}");
        port.RaiseData($"INPUT{pressedMask}");

        Assert.That(requestCount, Is.EqualTo(2));
    }

    [Test]
    public void ServiceFrame_IsIgnoredWhenPhysicalServiceInputIsConfigured()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(
            port,
            PeripheryDispenseMode.Hopper1Auto,
            serviceButtonInputIndex: PeripheryControllerInputs.ServiceButton,
            serviceButtonActiveHigh: false);
        int requestCount = 0;
        controller.ServiceRequested += () => requestCount++;
        int inactiveMask = 67_108_863;
        int pressedMask = inactiveMask & ~(1 << PeripheryControllerInputs.ServiceButton);

        port.RaiseData($"INPUT{inactiveMask}");
        port.RaiseData($"INPUT{pressedMask}");
        port.RaiseData("SERVICE");

        Assert.That(requestCount, Is.EqualTo(1));
    }

    [Test]
    public void ServiceInput_RequiresInactiveBaselineBeforeFirstPress()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(
            port,
            PeripheryDispenseMode.Hopper1Auto,
            serviceButtonInputIndex: PeripheryControllerInputs.ServiceButton,
            serviceButtonActiveHigh: false);
        int requestCount = 0;
        controller.ServiceRequested += () => requestCount++;
        int inactiveMask = 67_108_863;
        int pressedMask = inactiveMask & ~(1 << PeripheryControllerInputs.ServiceButton);

        port.RaiseData("INPUT0");
        Assert.That(requestCount, Is.Zero);
        port.RaiseData($"INPUT{inactiveMask}");
        port.RaiseData($"INPUT{pressedMask}");

        Assert.That(requestCount, Is.EqualTo(1));
    }

    [Test]
    public void ServiceAndDispenseInputs_UseFastConfigurablePolling()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(
            port,
            PeripheryDispenseMode.Hopper1Auto,
            serviceButtonInputIndex: PeripheryControllerInputs.ServiceButton,
            serviceButtonActiveHigh: false,
            inputPollMilliseconds: 100);
        int initialQueries = port.SentCommands.Count(command => command == "91");

        controller.Update(1_000);
        controller.Update(1_099);
        Assert.That(port.SentCommands.Count(command => command == "91"), Is.EqualTo(initialQueries));
        controller.Update(1_100);

        Assert.That(port.SentCommands.Count(command => command == "91"), Is.EqualTo(initialQueries + 1));
    }

    [Test]
    public void Dispense_IsBlockedWhenPowerChannelCommandIsUnknown()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port);
        (bool Success, int Count)? result = null;
        controller.DispenseFinished += (success, count, _) => result = (success, count);

        controller.DispenseTokens(5);

        Assert.That(result, Is.EqualTo((false, 0)));
        Assert.That(port.SentCommands, Does.Not.Contain("3095"));
    }

    [Test]
    public void Payment_IsBlockedWhenDispenseCommandIsUnknown()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port);
        MachineFault? fault = null;
        controller.FaultOccurred += value => fault = value;
        int commandCount = port.SentCommands.Count;

        controller.BeginCardPayment(100);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fault, Is.EqualTo(MachineFault.Unavailable));
            Assert.That(port.SentCommands.Skip(commandCount), Does.Not.Contain("163100"));
        }
    }

    [Test]
    public void ProtocolRangeValidation_DoesNotLeavePaymentOrDispenseActive()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.Hopper1Auto);
        var cardResults = new List<MachineCardResult>();
        var dispenseResults = new List<(bool Success, int Count, string? Error)>();
        controller.CardPaymentFinished += cardResults.Add;
        controller.DispenseFinished += (success, count, error) => dispenseResults.Add((success, count, error));
        int commandCount = port.SentCommands.Count;

        controller.BeginCardPayment(10_000);
        controller.DispenseTokens(0);
        controller.DispenseTokens(1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cardResults, Is.EqualTo(new[] { MachineCardResult.Declined }));
            Assert.That(dispenseResults, Does.Contain((false, 0, "DISPENSE_COUNT_OUT_OF_RANGE")));
            Assert.That(port.SentCommands.Skip(commandCount), Does.Not.Contain("161"));
            Assert.That(port.SentCommands.Skip(commandCount), Does.Contain("3091"));
        }
    }

    [Test]
    public void LargeDispense_IsSplitIntoConfirmedChunksOfAtMostNinetyNine()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.Hopper1Auto);
        var results = new List<(bool Success, int Count, string? Error)>();
        controller.DispenseFinished += (success, count, error) => results.Add((success, count, error));

        controller.DispenseTokens(120);
        for (int index = 0; index < 98; index++)
        {
            port.RaiseData("HOPPER1 OK");
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(port.SentCommands.Count(command => command == "30999"), Is.EqualTo(1));
            Assert.That(port.SentCommands, Does.Not.Contain("30921"));
            Assert.That(results, Is.Empty);
        }

        port.RaiseData("HOPPER1 OK");
        Assert.That(port.SentCommands.Count(command => command == "30921"), Is.EqualTo(1));
        for (int index = 0; index < 21; index++)
        {
            port.RaiseData("HOPPER1 OK");
        }

        Assert.That(results, Is.EqualTo(new[] { (true, 120, (string?)null) }));
    }

    [Test]
    public void LargeDispenseFailure_ReportsConfirmedTotalAcrossChunks()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.Hopper1Auto);
        (bool Success, int Count, string? Error)? result = null;
        controller.DispenseFinished += (success, count, error) => result = (success, count, error);

        controller.DispenseTokens(120);
        for (int index = 0; index < 100; index++)
        {
            port.RaiseData("HOPPER1 OK");
        }
        port.RaiseData("HOPPER1 ERROR");
        port.RaiseData("HOPPER1 CREDIT=20");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(port.SentCommands.Count(command => command == "30999"), Is.EqualTo(1));
            Assert.That(port.SentCommands.Count(command => command == "30921"), Is.EqualTo(1));
            Assert.That(result, Is.EqualTo((false, 100, "HOPPER1_ERROR")));
        }
    }

    [Test]
    public void Hopper1Auto_CompletesOnlyAfterAllOkConfirmations()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.Hopper1Auto);
        var results = new List<(bool Success, int Count)>();
        var progress = new List<(int Confirmed, int Requested)>();
        controller.DispenseFinished += (success, count, _) => results.Add((success, count));
        controller.DispenseProgressChanged += (confirmed, requested) => progress.Add((confirmed, requested));

        controller.DispenseTokens(2);
        port.RaiseData("HOPPER1 OK");
        port.RaiseData("HOPPER1 OK");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(port.SentCommands, Does.Contain("3092"));
            Assert.That(results, Is.EqualTo(new[] { (true, 2) }));
            Assert.That(progress, Is.EqualTo(new[] { (1, 2), (2, 2) }));
            Assert.That(controller.StockLevel, Is.EqualTo(MachineStockLevel.Unknown));
        }
    }

    [Test]
    public void Hopper1Dispense_UsesOkFramesAndDoesNotDoubleCountInputEdges()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(
            port,
            PeripheryDispenseMode.Hopper1Auto,
            hopper1DispenseSensorInputIndex: PeripheryControllerInputs.Hopper1DispensePulse,
            hopper1DispenseSensorActiveHigh: false);
        var results = new List<(bool Success, int Count)>();
        controller.DispenseFinished += (success, count, _) => results.Add((success, count));
        string inactiveMask = (1 << PeripheryControllerInputs.Hopper1DispensePulse).ToString();

        port.RaiseData("INPUT" + inactiveMask);
        controller.DispenseTokens(2);
        port.RaiseData("HOPPER1 OK");
        port.RaiseData("INPUT0");
        port.RaiseData("INPUT0");
        port.RaiseData("INPUT" + inactiveMask);
        port.RaiseData("INPUT0");
        Assert.That(results, Is.Empty);
        port.RaiseData("HOPPER1 OK");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(port.SentCommands.Count(command => command == "3092"), Is.EqualTo(1));
            Assert.That(results, Is.EqualTo(new[] { (true, 2) }));
        }
    }

    [Test]
    public void Hopper2Auto_UsesConfirmed40XProtocol()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(
            port,
            PeripheryDispenseMode.Hopper2Auto,
            hopper2DispenseSensorInputIndex: PeripheryControllerInputs.Hopper2DispensePulse,
            hopper2DispenseSensorActiveHigh: false);
        var results = new List<(bool Success, int Count, string? Error)>();
        controller.DispenseFinished += (success, count, error) => results.Add((success, count, error));

        controller.DispenseTokens(3);
        port.RaiseData("HOPPER2 OK");
        port.RaiseData("HOPPER2 OK");
        port.RaiseData("HOPPER2 OK");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(port.SentCommands, Does.Contain("4093"));
            Assert.That(port.SentCommands, Does.Not.Contain("3093"));
            Assert.That(results, Is.EqualTo(new[] { (true, 3, (string?)null) }));
        }
    }

    [Test]
    public void Hopper1Auto_IgnoresAllSecondHopperFeedback()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.Hopper1Auto);
        var results = new List<(bool Success, int Count, string? Error)>();
        controller.DispenseFinished += (success, count, error) => results.Add((success, count, error));

        controller.DispenseTokens(1);
        port.RaiseData("HOPPER2 APPEND=1");
        port.RaiseData("HOPPER2 PULSE=1");
        port.RaiseData("HOPPER2 CREDIT=1");
        port.RaiseData("HOPPER2 ERROR");
        Assert.That(results, Is.Empty);
        port.RaiseData("HOPPER1 OK");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(port.SentCommands.Count(command => command == "3091"), Is.EqualTo(1));
            Assert.That(results, Is.EqualTo(new[] { (true, 1, (string?)null) }));
        }
    }

    [Test]
    public void Hopper1Dispense_DoesNotRequireInitialInputSnapshot()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(
            port,
            PeripheryDispenseMode.Hopper1Auto,
            hopper1DispenseSensorInputIndex: PeripheryControllerInputs.Hopper1DispensePulse,
            hopper1DispenseSensorActiveHigh: false);
        (bool Success, int Count, string? Error)? result = null;
        controller.DispenseFinished += (success, count, error) => result = (success, count, error);

        controller.DispenseTokens(1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Null);
            Assert.That(port.SentCommands, Does.Contain("3091"));
        }
        port.RaiseData("HOPPER1 OK");
        Assert.That(result, Is.EqualTo((true, 1, (string?)null)));
    }

    [Test]
    public void HopperError_UsesCreditAndAllowsNextDispenseWithoutRestart()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.Hopper1Auto);
        var results = new List<(bool Success, int Count, string? Error)>();
        controller.DispenseFinished += (success, count, error) => results.Add((success, count, error));

        controller.DispenseTokens(2);
        controller.DispenseTokens(2);
        port.RaiseData("HOPPER1 ERROR");
        controller.DispenseTokens(2);
        Assert.That(results, Is.Empty);
        port.RaiseData("HOPPER1 CREDIT=2");
        controller.DispenseTokens(2);
        port.RaiseData("HOPPER1 OK");
        port.RaiseData("HOPPER1 OK");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(port.SentCommands.Count(command => command == "3092"), Is.EqualTo(2));
            Assert.That(results, Is.EqualTo(new[]
            {
                (false, 0, "HOPPER1_ERROR"),
                (true, 2, (string?)null),
            }));
        }
    }

    [Test]
    public void HopperError_WithoutCreditFinishesAfterThreeSecondsWithConfirmedCount()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.Hopper1Auto);
        (bool Success, int Count, string? Error)? result = null;
        controller.DispenseFinished += (success, count, error) => result = (success, count, error);

        controller.DispenseTokens(3);
        port.RaiseData("HOPPER1 OK");
        port.RaiseData("HOPPER1 ERROR");
        controller.Update(4_999);
        Assert.That(result, Is.Null);

        controller.Update(5_000);

        Assert.That(result, Is.EqualTo((false, 1, "HOPPER1_CREDIT_TIMEOUT")));
    }

    [Test]
    public void UnexpectedHopperErrorOutsideDispense_IsIgnored()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.Hopper1Auto);
        int resultCount = 0;
        controller.DispenseFinished += (_, _, _) => resultCount++;

        port.RaiseData("HOPPER1 ERROR");

        Assert.That(resultCount, Is.Zero);
    }

    [Test]
    public void ConfiguredDigitalInput_ReportsOnlyEnoughOrLow()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(
            port,
            PeripheryDispenseMode.Hopper1Auto,
            stockSensorInputIndex: 1,
            stockLowWhenInputHigh: false);
        var levels = new List<MachineStockLevel>();
        controller.StockChanged += levels.Add;

        port.RaiseData("INPUT 67108863");
        port.RaiseData("INPUT 67108861");
        port.RaiseConnection(false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(port.SentCommands, Does.Contain("91"));
            Assert.That(levels, Is.EqualTo(new[]
            {
                MachineStockLevel.Enough,
                MachineStockLevel.Low,
                MachineStockLevel.Unknown,
            }));
        }
    }

    [Test]
    public void Hopper1StockInput_ActiveLowIn1MeansLowForConfirmedBoardMapping()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(
            port,
            PeripheryDispenseMode.Hopper1Auto,
            stockSensorInputIndex: PeripheryControllerInputs.Hopper1StockLow,
            stockLowWhenInputHigh: false);
        var levels = new List<MachineStockLevel>();
        controller.StockChanged += levels.Add;

        port.RaiseData("INPUT67108863");
        port.RaiseData("INPUT67108861");
        port.RaiseData("INPUT67108861");
        port.RaiseData("INPUT67108863");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(levels, Is.EqualTo(new[]
            {
                MachineStockLevel.Enough,
                MachineStockLevel.Low,
                MachineStockLevel.Enough,
            }));
            Assert.That(controller.StockLevel, Is.EqualTo(MachineStockLevel.Enough));
        }
    }

    [Test]
    public void ConfiguredStockSensor_IsPolledPeriodically()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(
            port,
            PeripheryDispenseMode.Hopper1Auto,
            stockSensorInputIndex: 1);
        int initialQueries = port.SentCommands.Count(command => command == "91");

        controller.Update(1_000);
        controller.Update(5_999);
        controller.Update(6_000);

        Assert.That(port.SentCommands.Count(command => command == "91"), Is.EqualTo(initialQueries + 1));
    }

    [Test]
    public void DisconnectDuringDispense_ProducesOneUnknownOutcomeInSessionFlow()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(
            port,
            PeripheryDispenseMode.Hopper1Auto,
            stockSensorInputIndex: 1,
            stockLowWhenInputHigh: false);
        using var flow = new SessionFlow(TestSettings.Create(), controller, new ManualClock(), new SequenceSessionIdGenerator());
        var outcomes = new List<OperationOutcome>();
        flow.OperationFinished += snapshot => outcomes.Add(snapshot.Outcome);
        port.RaiseData("INPUT 67108863");
        flow.BeginCard();
        flow.SelectCardAmount(100);
        port.RaiseData("MONEY:CASHLESS:100");
        Assert.That(flow.State, Is.EqualTo(SessionState.Dispensing));

        port.RaiseConnection(false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flow.ErrorCode, Is.EqualTo(SessionErrorCode.DispenseStatusUnknown));
            Assert.That(outcomes, Is.EqualTo(new[] { OperationOutcome.DispenseStatusUnknown }));
        }
    }

    [Test]
    public void Hopper1TokenRecount_CountsFirmwarePulseFramesAndStopsAfterSilence()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(
            port,
            PeripheryDispenseMode.Hopper1Auto);
        var progress = new List<int>();
        var results = new List<(bool Success, int Count, string? Error)>();
        controller.TokenRecountProgressChanged += progress.Add;
        controller.TokenRecountFinished += (success, count, error) => results.Add((success, count, error));
        Assert.That(controller.TryStartTokenRecount(2_000), Is.True);
        port.RaiseData("HOPPER1 OK");
        port.RaiseData("HOPPER1 PULSE=1");
        port.RaiseData("HOPPER1 PULSE=1");
        controller.Update(11_999);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(port.SentCommands.Count(command => command == "301"), Is.EqualTo(1));
            Assert.That(port.SentCommands, Does.Not.Contain("300"));
            Assert.That(progress, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(results, Is.Empty);
        }

        controller.Update(12_000);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(port.SentCommands.Count(command => command == "300"), Is.EqualTo(1));
            Assert.That(results, Is.EqualTo(new[] { (true, 2, (string?)null) }));
            Assert.That(controller.IsTokenRecountInProgress, Is.False);
        }
    }

    [Test]
    public void TokenRecount_UsesFirstHopperAndCancellationStopsMotor()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(
            port,
            PeripheryDispenseMode.Hopper1Auto,
            hopper1DispenseSensorInputIndex: PeripheryControllerInputs.Hopper1DispensePulse,
            hopper1DispenseSensorActiveHigh: false,
            hopper2DispenseSensorInputIndex: PeripheryControllerInputs.Hopper2DispensePulse,
            hopper2DispenseSensorActiveHigh: false);
        var results = new List<(bool Success, int Count, string? Error)>();
        controller.TokenRecountFinished += (success, count, error) => results.Add((success, count, error));
        Assert.That(controller.TryStartTokenRecount(500), Is.True);
        controller.CancelTokenRecount();
        controller.CancelTokenRecount();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(port.SentCommands.Count(command => command == "301"), Is.EqualTo(1));
            Assert.That(port.SentCommands.Count(command => command == "300"), Is.EqualTo(1));
            Assert.That(port.SentCommands, Does.Not.Contain("401"));
            Assert.That(results, Is.EqualTo(new[] { (false, 0, "TOKEN_RECOUNT_CANCELLED") }));
        }
    }

    [Test]
    public void DualHopperDispense_AccountsConfirmedTokensPerHopper()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.DualHopperAuto);
        controller.SetHopperInventory(1, 2);
        var accounted = new List<(int Hopper1, int Hopper2)>();
        var results = new List<(bool Success, int Count)>();
        controller.HopperDispenseAccounted += (hopper1, hopper2) => accounted.Add((hopper1, hopper2));
        controller.DispenseFinished += (success, count, _) => results.Add((success, count));

        controller.DispenseTokens(3);
        port.RaiseData("HOPPER1 OK");
        port.RaiseData("HOPPER2 OK");
        port.RaiseData("HOPPER2 OK");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(port.SentCommands, Does.Contain("3091"));
            Assert.That(port.SentCommands, Does.Contain("4092"));
            Assert.That(accounted, Is.EqualTo(new[] { (1, 2) }));
            Assert.That(results, Is.EqualTo(new[] { (true, 3) }));
        }
    }

    [Test]
    public void DualHopperDispense_ExhaustsFirstHopperBeforeUsingSecond()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.DualHopperAuto);
        controller.SetHopperInventory(6, 20);
        var accounted = new List<(int Hopper1, int Hopper2)>();
        var results = new List<(bool Success, int Count, string? Error)>();
        controller.HopperDispenseAccounted += (hopper1, hopper2) => accounted.Add((hopper1, hopper2));
        controller.DispenseFinished += (success, count, error) => results.Add((success, count, error));

        controller.DispenseTokens(10);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(port.SentCommands[^1], Is.EqualTo("3096"));
            Assert.That(port.SentCommands, Does.Not.Contain("4094"));
        }

        for (int index = 0; index < 6; index++)
        {
            port.RaiseData("HOPPER1 OK");
        }

        Assert.That(port.SentCommands[^1], Is.EqualTo("4094"));

        for (int index = 0; index < 4; index++)
        {
            port.RaiseData("HOPPER2 OK");
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(accounted, Is.EqualTo(new[] { (6, 4) }));
            Assert.That(results, Is.EqualTo(new[] { (true, 10, (string?)null) }));
        }
    }

    [Test]
    public void DualHopperDispense_RejectsRequestWhenCombinedInventoryIsInsufficient()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.DualHopperAuto);
        controller.SetHopperInventory(3, 6);
        var results = new List<(bool Success, int Count, string? Error)>();
        controller.DispenseFinished += (success, count, error) => results.Add((success, count, error));
        int commandsBeforeDispense = port.SentCommands.Count;

        controller.DispenseTokens(10);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(port.SentCommands.Count, Is.EqualTo(commandsBeforeDispense));
            Assert.That(results, Is.EqualTo(new[]
            {
                (false, 0, (string?)"INSUFFICIENT_ACCOUNTED_TOKENS"),
            }));
        }
    }

    [Test]
    public void DualHopperDispense_WithInventoryAccountingDisabled_IgnoresStoredCountAndChunksHopperOne()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.DualHopperAuto);
        controller.SetHopperInventory(0, 0);
        controller.SetInventoryAccountingEnabled(false);
        var results = new List<(bool Success, int Count, string? Error)>();
        controller.DispenseFinished += (success, count, error) => results.Add((success, count, error));
        int commandsBeforeDispense = port.SentCommands.Count;

        controller.DispenseTokens(120);
        for (int index = 0; index < 99; index++)
        {
            port.RaiseData("HOPPER1 OK");
        }

        Assert.That(
            port.SentCommands
                .Skip(commandsBeforeDispense)
                .Where(command => command.StartsWith("309", StringComparison.Ordinal)
                                  || command.StartsWith("409", StringComparison.Ordinal)),
            Is.EqualTo(new[] { "30999", "30921" }));

        for (int index = 0; index < 21; index++)
        {
            port.RaiseData("HOPPER1 OK");
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Is.EqualTo(new[] { (true, 120, (string?)null) }));
            Assert.That(port.SentCommands, Does.Not.Contain("40921"));
        }
    }

    [Test]
    public void DualHopperDispense_WithPurchaseLimitDisabled_TracksKnownFirstHopperAndAllowsRemainder()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.DualHopperAuto);
        controller.SetHopperInventory(3, 0);
        controller.SetPurchaseLimitEnabled(false);
        var accounted = new List<(int Hopper1, int Hopper2)>();
        var results = new List<(bool Success, int Count, string? Error)>();
        controller.HopperDispenseAccounted += (hopper1, hopper2) => accounted.Add((hopper1, hopper2));
        controller.DispenseFinished += (success, count, error) => results.Add((success, count, error));

        controller.DispenseTokens(5);
        for (int index = 0; index < 3; index++)
        {
            port.RaiseData("HOPPER1 OK");
        }

        Assert.That(port.SentCommands[^1], Is.EqualTo("4092"));
        for (int index = 0; index < 2; index++)
        {
            port.RaiseData("HOPPER2 OK");
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(accounted, Is.EqualTo(new[] { (3, 2) }));
            Assert.That(results, Is.EqualTo(new[] { (true, 5, (string?)null) }));
        }
    }

    [TestCase(205, 0, "HOPPER1", "309")]
    [TestCase(0, 205, "HOPPER2", "409")]
    public void DualHopperLargeDispense_SendsOneConfirmedChunkAtATime(
        int hopper1Inventory,
        int hopper2Inventory,
        string responsePrefix,
        string commandPrefix)
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.DualHopperAuto);
        controller.SetHopperInventory(hopper1Inventory, hopper2Inventory);
        var accounted = new List<(int Hopper1, int Hopper2)>();
        var results = new List<(bool Success, int Count, string? Error)>();
        controller.HopperDispenseAccounted += (hopper1, hopper2) => accounted.Add((hopper1, hopper2));
        controller.DispenseFinished += (success, count, error) => results.Add((success, count, error));
        int commandsBeforeDispense = port.SentCommands.Count;

        controller.DispenseTokens(205);
        port.RaiseData($"{responsePrefix} APPEND=99");
        for (int index = 0; index < 98; index++)
        {
            port.RaiseData($"{responsePrefix} OK");
        }

        Assert.That(
            port.SentCommands
                .Skip(commandsBeforeDispense)
                .Where(command => command.StartsWith("309", StringComparison.Ordinal)
                                  || command.StartsWith("409", StringComparison.Ordinal)),
            Is.EqualTo(new[] { $"{commandPrefix}99" }),
            "Следующая пачка не должна отправляться до последнего OK текущей пачки.");

        port.RaiseData($"{responsePrefix} OK");
        Assert.That(
            port.SentCommands
                .Skip(commandsBeforeDispense)
                .Where(command => command.StartsWith("309", StringComparison.Ordinal)
                                  || command.StartsWith("409", StringComparison.Ordinal)),
            Is.EqualTo(new[] { $"{commandPrefix}99", $"{commandPrefix}99" }));

        port.RaiseData($"{responsePrefix} APPEND=99");
        for (int index = 0; index < 99; index++)
        {
            port.RaiseData($"{responsePrefix} OK");
        }

        Assert.That(
            port.SentCommands
                .Skip(commandsBeforeDispense)
                .Where(command => command.StartsWith("309", StringComparison.Ordinal)
                                  || command.StartsWith("409", StringComparison.Ordinal)),
            Is.EqualTo(new[] { $"{commandPrefix}99", $"{commandPrefix}99", $"{commandPrefix}7" }));

        port.RaiseData($"{responsePrefix} APPEND=7");
        for (int index = 0; index < 7; index++)
        {
            port.RaiseData($"{responsePrefix} OK");
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(accounted, Is.EqualTo(new[] { (hopper1Inventory, hopper2Inventory) }));
            Assert.That(results, Is.EqualTo(new[] { (true, 205, (string?)null) }));
        }
    }

    [Test]
    public void DualHopperLargeSplit_CompletesAllFirstHopperChunksBeforeSecondHopper()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.DualHopperAuto);
        controller.SetHopperInventory(150, 50);
        var accounted = new List<(int Hopper1, int Hopper2)>();
        controller.HopperDispenseAccounted += (hopper1, hopper2) => accounted.Add((hopper1, hopper2));
        int commandsBeforeDispense = port.SentCommands.Count;

        controller.DispenseTokens(180);
        for (int index = 0; index < 99; index++)
        {
            port.RaiseData("HOPPER1 OK");
        }

        for (int index = 0; index < 50; index++)
        {
            port.RaiseData("HOPPER1 OK");
        }

        Assert.That(
            port.SentCommands
                .Skip(commandsBeforeDispense)
                .Where(command => command.StartsWith("309", StringComparison.Ordinal)
                                  || command.StartsWith("409", StringComparison.Ordinal)),
            Is.EqualTo(new[] { "30999", "30951" }),
            "Второй хоппер не должен запускаться до последнего OK второго пакета первого хоппера.");

        port.RaiseData("HOPPER1 OK");
        Assert.That(port.SentCommands[^1], Is.EqualTo("40930"));

        for (int index = 0; index < 30; index++)
        {
            port.RaiseData("HOPPER2 OK");
        }

        Assert.That(accounted, Is.EqualTo(new[] { (150, 30) }));
    }

    [Test]
    public void DualHopperRecount_UsesSecondHopperMotorAndPulseFrames()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(port, PeripheryDispenseMode.DualHopperAuto);
        var progress = new List<(HopperChannel Hopper, int Count)>();
        var results = new List<(HopperChannel Hopper, bool Success, int Count)>();
        controller.HopperTokenRecountProgressChanged +=
            (hopper, count) => progress.Add((hopper, count));
        controller.HopperTokenRecountFinished +=
            (hopper, success, count, _) => results.Add((hopper, success, count));

        bool started = controller.TryStartTokenRecount(HopperChannel.Hopper2, 500);
        port.RaiseData("HOPPER2 PULSE=1");
        port.RaiseData("HOPPER2 PULSE=1");
        controller.Update(10_500);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(started, Is.True);
            Assert.That(port.SentCommands, Does.Contain("401"));
            Assert.That(port.SentCommands, Does.Contain("400"));
            Assert.That(progress, Is.EqualTo(new[]
            {
                (HopperChannel.Hopper2, 0),
                (HopperChannel.Hopper2, 1),
                (HopperChannel.Hopper2, 2),
            }));
            Assert.That(results, Is.EqualTo(new[] { (HopperChannel.Hopper2, true, 2) }));
        }
    }

    [Test]
    public void DualStockSensors_ReportIn1AndIn4Separately()
    {
        var port = new FakeHardwarePort();
        using var controller = CreateReadyController(
            port,
            PeripheryDispenseMode.DualHopperAuto,
            stockSensorInputIndex: PeripheryControllerInputs.Hopper1StockLow,
            stockLowWhenInputHigh: false,
            hopper2StockSensorInputIndex: PeripheryControllerInputs.Hopper2StockLow,
            hopper2StockLowWhenInputHigh: false);
        var levels = new List<(HopperChannel Hopper, MachineStockLevel Level)>();
        controller.HopperStockChanged += (hopper, level) => levels.Add((hopper, level));

        port.RaiseData($"INPUT{(1 << 1) | (1 << 4)}");
        port.RaiseData($"INPUT{1 << 1}");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(levels, Does.Contain((HopperChannel.Hopper1, MachineStockLevel.Enough)));
            Assert.That(levels, Does.Contain((HopperChannel.Hopper2, MachineStockLevel.Enough)));
            Assert.That(levels, Does.Contain((HopperChannel.Hopper2, MachineStockLevel.Low)));
        }
    }

    private static PeripheryMachineController CreateReadyController(
        FakeHardwarePort port,
        PeripheryDispenseMode dispenseMode = PeripheryDispenseMode.Unsupported,
        int keepAliveSeconds = 120,
        int stockSensorInputIndex = -1,
        bool stockLowWhenInputHigh = false,
        int serviceButtonInputIndex = -1,
        bool serviceButtonActiveHigh = true,
        int hopper1DispenseSensorInputIndex = -1,
        bool hopper1DispenseSensorActiveHigh = false,
        int hopper2DispenseSensorInputIndex = -1,
        bool hopper2DispenseSensorActiveHigh = false,
        int inputPollMilliseconds = 100,
        int hopper2StockSensorInputIndex = -1,
        bool hopper2StockLowWhenInputHigh = false)
    {
        var authenticator = new ControllerAuthenticator(new IdentityBlockCipher(), () => HostChallenge);
        var controller = new PeripheryMachineController(
            port,
            authenticator,
            dispenseMode,
            keepAliveSeconds,
            stockSensorInputIndex,
            stockLowWhenInputHigh,
            serviceButtonInputIndex: serviceButtonInputIndex,
            serviceButtonActiveHigh: serviceButtonActiveHigh,
            hopper1DispenseSensorInputIndex: hopper1DispenseSensorInputIndex,
            hopper1DispenseSensorActiveHigh: hopper1DispenseSensorActiveHigh,
            hopper2DispenseSensorInputIndex: hopper2DispenseSensorInputIndex,
            hopper2DispenseSensorActiveHigh: hopper2DispenseSensorActiveHigh,
            inputPollMilliseconds: inputPollMilliseconds,
            hopper2StockSensorInputIndex: hopper2StockSensorInputIndex,
            hopper2StockLowWhenInputHigh: hopper2StockLowWhenInputHigh);
        port.RaiseConnection(true);
        port.RaiseData("periphery controller ver_test");
        controller.Update(0);
        controller.Update(2_000);
        port.RaiseData("CHALFFEEDDCCBBAA99887766554433221100");
        port.RaiseData("RAES00112233445566778899AABBCCDDEEFF");
        port.RaiseData("READY!");
        port.RaiseData("AUTH_CNT_OK");
        Assert.That(controller.IsReady, Is.True);
        return controller;
    }
}
