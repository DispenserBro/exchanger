using Exchanger.Core.Session;
using Exchanger.Hardware.Abstractions;
using Exchanger.Hardware.MockHardware;
using Exchanger.Hardware.PeripheryController;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class PeripheryControllerSimulatorTests
{
    private static readonly byte[] HostChallenge =
        Convert.FromHexString("00112233445566778899AABBCCDDEEFF");

    [TestCase("309100", "ERROR:30:invalid count (0-99)")]
    [TestCase("409100", "ERROR:40:invalid count (0-99)")]
    public void Simulator_RejectsSingleHopperBatchAboveNinetyNineLikeFirmware(string command, string error)
    {
        var simulator = new PeripheryControllerSimulator(new IdentityBlockCipher());
        var received = new List<string>();
        simulator.OnDataReceived += received.Add;
        simulator.Connect("SIM", 115200);
        simulator.ProcessPendingEvents();
        received.Clear();

        simulator.SendCommand(command);
        simulator.ProcessPendingEvents();

        Assert.That(received, Is.EqualTo(new[] { error }));
    }

    [Test]
    public void Simulator_RunsAuthenticatedCardAndDispenseFlowWithoutComPort()
    {
        var cipher = new IdentityBlockCipher();
        var simulator = new PeripheryControllerSimulator(cipher)
        {
            DigitalInputMask = 67_108_863,
        };
        var authenticator = new ControllerAuthenticator(cipher, () => HostChallenge);
        using var controller = new PeripheryMachineController(
            simulator,
            authenticator,
            PeripheryDispenseMode.Hopper1Auto,
            stockSensorInputIndex: 1,
            stockLowWhenInputHigh: false);
        using var flow = new SessionFlow(TestSettings.Create(), controller, new ManualClock());

        simulator.Connect("SIM", 115200);
        CompleteStartup(simulator, controller);
        Assert.That(controller.IsReady, Is.True);
        Assert.That(flow.StockLevel, Is.EqualTo(MachineStockLevel.Enough));

        Assert.That(flow.BeginCard(), Is.True);
        Assert.That(flow.SelectCardAmount(100), Is.True);
        simulator.ReportCashless(100);
        simulator.ProcessPendingEvents();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flow.State, Is.EqualTo(SessionState.Completed));
            Assert.That(simulator.SentCommands, Does.Contain("ASK:CASHLESS:100"));
            Assert.That(simulator.SentCommands.Count(command => command == "30912"), Is.EqualTo(1));
            Assert.That(simulator.SentCommands, Does.Not.Contain("40912"));
        }
    }

    [Test]
    public void Simulator_RunsAuthenticatedCashAndDispenseFlowWithoutComPort()
    {
        var cipher = new IdentityBlockCipher();
        var simulator = new PeripheryControllerSimulator(cipher)
        {
            DigitalInputMask = 67_108_863,
        };
        var authenticator = new ControllerAuthenticator(cipher, () => HostChallenge);
        using var controller = new PeripheryMachineController(
            simulator,
            authenticator,
            PeripheryDispenseMode.Hopper1Auto,
            stockSensorInputIndex: 1,
            stockLowWhenInputHigh: false);
        using var flow = new SessionFlow(TestSettings.Create(), controller, new ManualClock());

        simulator.Connect("SIM", 115200);
        CompleteStartup(simulator, controller);
        int commandCount = simulator.SentCommands.Count;

        Assert.That(flow.BeginCash(), Is.True);
        simulator.ReportBill(50);
        simulator.ProcessPendingEvents();
        Assert.That(flow.ConfirmCashDispense(), Is.True);
        simulator.ProcessPendingEvents();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flow.State, Is.EqualTo(SessionState.Completed));
            Assert.That(simulator.SentCommands.Skip(commandCount).Take(3),
                Is.EqualTo(new[] { "162", "141", "151" }));
            Assert.That(simulator.SentCommands, Does.Contain("ASK:BILL:50"));
            Assert.That(simulator.SentCommands, Does.Contain("142"));
            Assert.That(simulator.SentCommands, Does.Contain("152"));
            Assert.That(simulator.SentCommands.Count(command => command == "3095"), Is.EqualTo(1));
        }
    }

    [Test]
    public void Simulator_LargeCardPackageDispensesInSequentialProtocolSizedChunks()
    {
        var cipher = new IdentityBlockCipher();
        var simulator = new PeripheryControllerSimulator(cipher)
        {
            DigitalInputMask = 67_108_863,
        };
        var authenticator = new ControllerAuthenticator(cipher, () => HostChallenge);
        using var controller = new PeripheryMachineController(
            simulator,
            authenticator,
            PeripheryDispenseMode.Hopper1Auto,
            stockSensorInputIndex: 1,
            stockLowWhenInputHigh: false);
        using var flow = new SessionFlow(TestSettings.Create(), controller, new ManualClock());

        simulator.Connect("SIM", 115200);
        CompleteStartup(simulator, controller);
        Assert.That(flow.BeginCard(), Is.True);
        Assert.That(flow.SelectCardAmount(5000), Is.True);

        simulator.ReportCashless(5000);
        simulator.ProcessPendingEvents();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flow.TotalTokens, Is.EqualTo(515));
            Assert.That(flow.State, Is.EqualTo(SessionState.Completed));
            Assert.That(simulator.SentCommands.Count(command => command == "30999"), Is.EqualTo(5));
            Assert.That(simulator.SentCommands.Count(command => command == "30920"), Is.EqualTo(1));
            Assert.That(simulator.SentCommands.Any(command => command.StartsWith("409", StringComparison.Ordinal)), Is.False);
        }
    }

    [Test]
    public void Simulator_ReproducesPartialHopperFailureDeterministically()
    {
        var cipher = new IdentityBlockCipher();
        var simulator = new PeripheryControllerSimulator(cipher);
        var authenticator = new ControllerAuthenticator(cipher, () => HostChallenge);
        using var controller = new PeripheryMachineController(
            simulator,
            authenticator,
            PeripheryDispenseMode.Hopper1Auto,
            stockSensorInputIndex: 1,
            stockLowWhenInputHigh: false);
        using var flow = new SessionFlow(TestSettings.Create(), controller, new ManualClock());
        simulator.Connect("SIM", 115200);
        CompleteStartup(simulator, controller);
        simulator.FailNextDispenseAfter(3);

        flow.BeginCard();
        flow.SelectCardAmount(100);
        simulator.ReportCashless(100);
        simulator.ProcessPendingEvents();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flow.State, Is.EqualTo(SessionState.Error));
            Assert.That(flow.ErrorCode, Is.EqualTo(SessionErrorCode.PartialDispense));
            Assert.That(simulator.SentCommands.Count(command => command == "30912"), Is.EqualTo(1));
        }
    }

    [Test]
    public void MalformedFrame_DoesNotBreakFollowingValidPaymentFrame()
    {
        var cipher = new IdentityBlockCipher();
        var simulator = new PeripheryControllerSimulator(cipher);
        var authenticator = new ControllerAuthenticator(cipher, () => HostChallenge);
        using var controller = new PeripheryMachineController(
            simulator,
            authenticator,
            PeripheryDispenseMode.Hopper1Auto,
            stockSensorInputIndex: 1,
            stockLowWhenInputHigh: false);
        simulator.Connect("SIM", 115200);
        CompleteStartup(simulator, controller);
        MachineCardResult? result = null;
        controller.CardPaymentFinished += value => result = value;
        controller.BeginCardPayment(50);

        simulator.InjectFrame("MONEY:CASHLESS:not-a-number");
        simulator.ReportCashless(50);
        simulator.ProcessPendingEvents();

        Assert.That(result, Is.EqualTo(MachineCardResult.Approved));
        Assert.That(simulator.SentCommands.Count(command => command == "ASK:CASHLESS:50"), Is.EqualTo(1));
    }

    [Test]
    public void ReconnectStress_DoesNotReplayInterruptedDispenseOrAccumulateStateTransitions()
    {
        const int cycleCount = 200;
        var cipher = new IdentityBlockCipher();
        var simulator = new PeripheryControllerSimulator(cipher);
        var authenticator = new ControllerAuthenticator(cipher, () => HostChallenge);
        using var controller = new PeripheryMachineController(
            simulator,
            authenticator,
            PeripheryDispenseMode.Hopper1Auto,
            stockSensorInputIndex: 1,
            stockLowWhenInputHigh: false);
        var states = new List<MachineConnectionState>();
        int dispenseCompletions = 0;
        controller.ConnectionStateChanged += states.Add;
        controller.DispenseFinished += (_, _, _) => dispenseCompletions++;

        for (int cycle = 0; cycle < cycleCount; cycle++)
        {
            simulator.Connect("SIM", 115200);
            Assert.That(controller.ConnectionState, Is.EqualTo(MachineConnectionState.Authenticating));

            CompleteStartup(simulator, controller, (ulong)cycle * 3_000UL);
            Assert.That(controller.ConnectionState, Is.EqualTo(MachineConnectionState.Ready));

            controller.DispenseTokens(1);
            simulator.Disconnect();
            simulator.ProcessPendingEvents();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(controller.ConnectionState, Is.EqualTo(MachineConnectionState.Disconnected));
                Assert.That(controller.StockLevel, Is.EqualTo(MachineStockLevel.Unknown));
                Assert.That(dispenseCompletions, Is.Zero);
            }
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(states.Count(state => state == MachineConnectionState.Authenticating), Is.EqualTo(cycleCount));
            Assert.That(states.Count(state => state == MachineConnectionState.Ready), Is.EqualTo(cycleCount));
            Assert.That(states.Count(state => state == MachineConnectionState.Disconnected), Is.EqualTo(cycleCount));
            Assert.That(simulator.SentCommands.Count(command => command == "3091"), Is.EqualTo(cycleCount));
            Assert.That(dispenseCompletions, Is.Zero);
        }
    }

    private static void CompleteStartup(
        PeripheryControllerSimulator simulator,
        PeripheryMachineController controller,
        ulong startedAt = 0)
    {
        simulator.ProcessPendingEvents();
        Assert.That(
            controller.AuthenticationState,
            Is.EqualTo(ControllerAuthenticationState.StartupDelay));
        controller.Update(startedAt);
        controller.Update(startedAt + 2_000UL);
        simulator.ProcessPendingEvents();
        Assert.That(controller.IsReady, Is.True);
    }
}
