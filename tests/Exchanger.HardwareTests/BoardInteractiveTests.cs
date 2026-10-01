using Exchanger.Hardware.Abstractions;
using Exchanger.Hardware.PeripheryController;
using NUnit.Framework;

namespace Exchanger.HardwareTests;

[TestFixture]
[NonParallelizable]
public sealed class BoardInteractiveTests
{
    [Test]
    [Category("HardwareInteractive")]
    public void ServiceButton_PublishesTypedEvent()
    {
        BoardTestConfiguration configuration = RequireInteractive();
        using var session = new BoardTestSession(configuration);
        session.ConnectAndAuthorize();
        bool requested = false;
        session.Controller.ServiceRequested += () => requested = true;

        TestContext.Progress.WriteLine("Нажмите и отпустите сервисную кнопку, подключённую к IN8.");
        session.WaitUntil(
            () => requested,
            configuration.InteractionTimeout,
            "За отведённое время active-low фронт IN8/SERVICE не получен.");
    }

    [Test]
    [Category("HardwareInteractive")]
    public void CoinAcceptor_ReportsAndAcknowledgesExpectedAmount()
    {
        BoardTestConfiguration configuration = RequireInteractive();
        int expectedRubles = BoardTestConfiguration.RequireOptionalPositiveInt(
            "EXCHANGER_BOARD_COIN_RUBLES",
            9999,
            "Монетоприёмник");
        using var session = new BoardTestSession(configuration, PeripheryDispenseMode.Hopper1Auto);
        session.ConnectAndAuthorize();
        int balance = 0;
        session.Controller.CashBalanceChanged += value => balance = value;

        session.Controller.BeginCashAcceptance();
        try
        {
            TestContext.Progress.WriteLine($"Внесите одну тестовую монету номиналом {expectedRubles} ₽.");
            session.WaitForMessage(
                message => message.Kind == PeripheryMessageKind.MoneyCoins
                    && message.NumericValue == expectedRubles,
                configuration.InteractionTimeout,
                "Не получен MONEY:COINS с ожидаемым номиналом.");
            Assert.That(balance, Is.EqualTo(expectedRubles));
        }
        finally
        {
            session.Controller.EndCashAcceptance();
        }
    }

    [Test]
    [Category("HardwareInteractive")]
    public void BillAcceptor_ReportsAndAcknowledgesExpectedAmount()
    {
        BoardTestConfiguration configuration = RequireInteractive();
        int expectedRubles = BoardTestConfiguration.RequireOptionalPositiveInt(
            "EXCHANGER_BOARD_BILL_RUBLES",
            9999,
            "Купюроприёмник");
        using var session = new BoardTestSession(configuration, PeripheryDispenseMode.Hopper1Auto);
        session.ConnectAndAuthorize();
        int balance = 0;
        session.Controller.CashBalanceChanged += value => balance = value;

        session.Controller.BeginCashAcceptance();
        try
        {
            TestContext.Progress.WriteLine($"Внесите одну тестовую купюру номиналом {expectedRubles} ₽.");
            session.WaitForMessage(
                message => message.Kind == PeripheryMessageKind.MoneyBill
                    && message.NumericValue == expectedRubles,
                configuration.InteractionTimeout,
                "Не получен MONEY:BILL с ожидаемым номиналом.");
            Assert.That(balance, Is.EqualTo(expectedRubles));
        }
        finally
        {
            session.Controller.EndCashAcceptance();
        }
    }

    [Test]
    [Category("HardwareInteractive")]
    public void StockSensor_ReportsConfiguredBinaryLevel()
    {
        BoardTestConfiguration configuration = RequireInteractive();
        if (configuration.StockSensorInputIndex < 0)
        {
            Assert.Ignore("Датчик запаса не проверяется: EXCHANGER_BOARD_STOCK_INPUT не задан.");
        }

        MachineStockLevel expected = BoardTestConfiguration.RequireOptionalExpectedStockLevel();
        using var session = new BoardTestSession(configuration);
        session.ConnectAndAuthorize();

        TestContext.Progress.WriteLine($"Датчик должен находиться в состоянии {expected}; отправляется команда 91.");
        session.SendAndWaitForMessage(
            PeripheryProtocolCommands.QueryDigitalInputs,
            message => message.Kind == PeripheryMessageKind.DigitalInputs,
            "Не получен документированный кадр цифровых входов.");
        session.PumpOnce();

        Assert.That(session.Controller.StockLevel, Is.EqualTo(expected));
    }

    [Test]
    [Category("HardwarePayment")]
    public void CashlessTerminal_ApprovesConfiguredRealPayment()
    {
        BoardTestConfiguration configuration = BoardTestConfiguration.RequireSafeTests();
        BoardTestConfiguration.RequireConfirmation(
            "EXCHANGER_BOARD_PAYMENT_CONFIRM",
            "I_ACCEPT_REAL_PAYMENT",
            "Тест может провести реальную банковскую операцию.");
        int amountRubles = BoardTestConfiguration.RequirePositiveInt("EXCHANGER_BOARD_CARD_RUBLES", 9999);
        using var session = new BoardTestSession(configuration, PeripheryDispenseMode.Hopper1Auto);
        session.ConnectAndAuthorize();
        MachineCardResult? result = null;
        session.Controller.CardPaymentFinished += value => result = value;

        TestContext.Progress.WriteLine($"На терминале будет запрошена реальная оплата {amountRubles} ₽. Завершите её тестовой картой.");
        session.Controller.BeginCardPayment(amountRubles);
        session.WaitUntil(
            () => result.HasValue,
            configuration.InteractionTimeout,
            "Терминал не вернул поддерживаемый итог карточной оплаты.");

        Assert.That(result, Is.EqualTo(MachineCardResult.Approved));
    }

    [Test]
    [Category("HardwareDispense")]
    public void Hopper1DirectMotor_ReportsFirmwarePulse()
    {
        BoardTestConfiguration configuration = BoardTestConfiguration.RequireSafeTests();
        BoardTestConfiguration.RequireConfirmation(
            "EXCHANGER_BOARD_DISPENSE_CONFIRM",
            "I_ACCEPT_TOKEN_DISPENSE",
            "Тест напрямую включит первый хоппер командой 301 и обязательно выключит его командой 300.");
        using var session = new BoardTestSession(configuration);
        session.ConnectAndAuthorize();
        int pulseCount = 0;

        void ObservePulse(string frame)
        {
            PeripheryMessage message = PeripheryProtocolParser.Parse(frame);
            if (message.Kind == PeripheryMessageKind.Hopper1Pulse)
            {
                pulseCount++;
            }
        }

        session.Port.OnDataReceived += ObservePulse;
        try
        {
            TestContext.Progress.WriteLine("→ 301; ожидание первого HOPPER1 PULSE=1.");
            session.Port.SendCommand(PeripheryProtocolCommands.EnableHopper1Motor);
            session.WaitUntil(
                () => pulseCount > 0,
                TimeSpan.FromSeconds(5),
                "После 301 плата не прислала HOPPER1 PULSE=1.");
        }
        finally
        {
            TestContext.Progress.WriteLine("→ 300; двигатель первого хоппера выключен.");
            if (session.Port.IsConnected)
            {
                session.Port.SendCommand(PeripheryProtocolCommands.DisableHopper1Motor);
                session.DrainAndClear(TimeSpan.FromMilliseconds(250));
            }

            session.Port.OnDataReceived -= ObservePulse;
        }

        Assert.That(pulseCount, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    [Category("HardwareDispense")]
    public void Hopper_DispensesExactlyConfiguredTokenCount()
    {
        BoardTestConfiguration configuration = BoardTestConfiguration.RequireSafeTests();
        BoardTestConfiguration.RequireConfirmation(
            "EXCHANGER_BOARD_DISPENSE_CONFIRM",
            "I_ACCEPT_TOKEN_DISPENSE",
            "Тест физически запустит хоппер.");
        int tokenCount = BoardTestConfiguration.RequirePositiveInt("EXCHANGER_BOARD_DISPENSE_COUNT", 10);
        using var session = new BoardTestSession(configuration, PeripheryDispenseMode.Hopper1Auto);
        session.ConnectAndAuthorize();
        (bool Success, int Count, string? Error)? result = null;
        session.Controller.DispenseFinished += (success, count, error) => result = (success, count, error);

        TestContext.Progress.WriteLine($"Запускается физическая выдача {tokenCount} жетонов. Освободите лоток.");
        session.Controller.DispenseTokens(tokenCount);
        session.WaitUntil(
            () => result.HasValue,
            configuration.InteractionTimeout,
            "Хоппер не прислал финальное подтверждение выдачи.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.Value.Success, Is.True, result.Value.Error);
            Assert.That(result.Value.Count, Is.EqualTo(tokenCount));
        }
    }

    [Test]
    [Category("HardwareCable")]
    public void UsbCableRemoval_IsDetectedAndBoardCanReauthorizeAfterReconnect()
    {
        BoardTestConfiguration configuration = BoardTestConfiguration.RequireSafeTests();
        BoardTestConfiguration.RequireConfirmation(
            "EXCHANGER_BOARD_CABLE_CONFIRM",
            "I_WILL_UNPLUG_USB",
            "Тест ожидает ручное отключение и повторное подключение USB-кабеля.");
        using var session = new BoardTestSession(configuration);
        session.ConnectAndAuthorize();

        TestContext.Progress.WriteLine("Отключите USB-кабель контроллера.");
        session.WaitUntil(
            () => session.Controller.ConnectionState == MachineConnectionState.Disconnected,
            configuration.InteractionTimeout,
            "Отключение USB не было обнаружено.");

        TestContext.Progress.WriteLine("Подключите USB-кабель обратно; тест периодически откроет тот же COM-порт.");
        DateTime reconnectDeadline = DateTime.UtcNow + configuration.InteractionTimeout;
        while (DateTime.UtcNow < reconnectDeadline && !session.Controller.IsReady)
        {
            if (!session.Port.IsConnected)
            {
                session.Port.Connect(configuration.PortName, configuration.BaudRate);
            }

            session.PumpOnce();
            Thread.Sleep(500);
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(session.Controller.IsReady, Is.True, "После возврата USB контроллер не прошёл повторную авторизацию.");
            Assert.That(session.ConnectionStates, Does.Contain(MachineConnectionState.Disconnected));
            Assert.That(session.ConnectionStates.Count(state => state == MachineConnectionState.Ready), Is.GreaterThanOrEqualTo(2));
        }
    }

    private static BoardTestConfiguration RequireInteractive()
    {
        BoardTestConfiguration configuration = BoardTestConfiguration.RequireSafeTests();
        BoardTestConfiguration.RequireConfirmation(
            "EXCHANGER_BOARD_INTERACTIVE_CONFIRM",
            "I_AM_READY",
            "Тест требует ручного действия с периферией.");
        return configuration;
    }
}
