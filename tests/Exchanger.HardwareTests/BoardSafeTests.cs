using Exchanger.Hardware.Abstractions;
using Exchanger.Hardware.PeripheryController;
using NUnit.Framework;

namespace Exchanger.HardwareTests;

[TestFixture]
[NonParallelizable]
[Category("HardwareSafe")]
public sealed class BoardSafeTests
{
    [Test]
    public void TransportAndHandshake_ReachReadyAndCloseCleanly()
    {
        BoardTestConfiguration configuration = BoardTestConfiguration.RequireSafeTests();
        using var session = new BoardTestSession(configuration);

        session.ConnectAndAuthorize();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(session.Port.IsConnected, Is.True);
            Assert.That(session.Controller.AuthenticationState, Is.EqualTo(ControllerAuthenticationState.Ready));
            Assert.That(session.Controller.ConnectionState, Is.EqualTo(MachineConnectionState.Ready));
            Assert.That(session.ConnectionStates, Does.Contain(MachineConnectionState.Authenticating));
            Assert.That(session.ConnectionStates, Does.Contain(MachineConnectionState.Ready));
        }

        session.DisconnectAndWait();
        Assert.That(session.Port.IsConnected, Is.False);
    }

    [Test]
    public void DigitalInputs_QueryReturnsDocumentedTypedFrame()
    {
        BoardTestConfiguration configuration = BoardTestConfiguration.RequireSafeTests();
        using var session = new BoardTestSession(configuration);
        session.ConnectAndAuthorize();

        ObservedBoardMessage response = session.SendAndWaitForMessage(
            PeripheryProtocolCommands.QueryDigitalInputs,
            message => message.Kind == PeripheryMessageKind.DigitalInputs,
            "На команду 91 не получен документированный кадр INPUT<0..67108863>.");

        Assert.That(response.NumericValue, Is.InRange(0, 67_108_863));
    }

    [Test]
    public void Hopper1StockInput_UsesConfiguredBinaryPolarity()
    {
        BoardTestConfiguration configuration = BoardTestConfiguration.RequireSafeTests();
        if (configuration.StockSensorInputIndex < 0)
        {
            Assert.Ignore("Проверка IN1 отключена параметром StockInput=-1.");
        }

        using var session = new BoardTestSession(configuration);
        session.ConnectAndAuthorize();

        ObservedBoardMessage response = session.SendAndWaitForMessage(
            PeripheryProtocolCommands.QueryDigitalInputs,
            message => message.Kind == PeripheryMessageKind.DigitalInputs,
            "На команду 91 не получен снимок входов для проверки IN1.");
        session.PumpOnce();

        bool inputHigh = (response.NumericValue & (1 << configuration.StockSensorInputIndex)) != 0;
        MachineStockLevel expected = inputHigh == configuration.StockLowWhenInputHigh
            ? MachineStockLevel.Low
            : MachineStockLevel.Enough;

        Assert.That(session.Controller.StockLevel, Is.EqualTo(expected));
        TestContext.Progress.WriteLine($"IN{configuration.StockSensorInputIndex}: состояние запаса {expected}.");
    }

    [TestCase(PeripheryProtocolCommands.QueryCoinAcceptorStatus, TestName = "Status_CoinAcceptor_ReturnsFrame")]
    [TestCase(PeripheryProtocolCommands.QueryBillAcceptorStatus, TestName = "Status_BillAcceptor_ReturnsFrame")]
    [TestCase(PeripheryProtocolCommands.QueryCashlessStatus, TestName = "Status_Cashless_ReturnsFrame")]
    [TestCase(PeripheryProtocolCommands.QueryHopper1Credit, TestName = "Status_Hopper1Credit_ReturnsFrame")]
    [TestCase(PeripheryProtocolCommands.QueryControllerStatus, TestName = "Status_Controller_ReturnsFrame")]
    public void StatusQuery_ReturnsNonHandshakeFrame(string command)
    {
        BoardTestConfiguration configuration = BoardTestConfiguration.RequireSafeTests();
        using var session = new BoardTestSession(configuration);
        session.ConnectAndAuthorize();

        ObservedBoardMessage response = session.SendAndWaitForMessage(
            command,
            message => message.Kind is PeripheryMessageKind.Unknown
                or PeripheryMessageKind.Hopper1Credit
                or PeripheryMessageKind.ControllerError
                or PeripheryMessageKind.TransportError,
            $"Контроллер не вернул кадр на безопасный status-запрос {command}.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.FrameLength, Is.GreaterThan(0));
            Assert.That(response.Kind, Is.Not.EqualTo(PeripheryMessageKind.ControllerError));
            Assert.That(response.Kind, Is.Not.EqualTo(PeripheryMessageKind.TransportError));
        }
        TestContext.Progress.WriteLine($"Status-запрос {command}: получен кадр категории {response.Kind}, длина скрыта из отчёта протокола.");
    }

    [Test]
    public void ReconnectCycles_ReauthorizeExactlyOncePerCycle()
    {
        BoardTestConfiguration configuration = BoardTestConfiguration.RequireSafeTests();
        using var session = new BoardTestSession(configuration);

        for (int cycle = 1; cycle <= configuration.ReconnectCycles; cycle++)
        {
            TestContext.Progress.WriteLine($"Цикл переподключения {cycle}/{configuration.ReconnectCycles}.");
            session.ConnectAndAuthorize();
            session.DisconnectAndWait();
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                session.ConnectionStates.Count(state => state == MachineConnectionState.Authenticating),
                Is.EqualTo(configuration.ReconnectCycles));
            Assert.That(
                session.ConnectionStates.Count(state => state == MachineConnectionState.Ready),
                Is.EqualTo(configuration.ReconnectCycles));
            Assert.That(
                session.ConnectionStates.Count(state => state == MachineConnectionState.Disconnected),
                Is.EqualTo(configuration.ReconnectCycles));
        }
    }
}
