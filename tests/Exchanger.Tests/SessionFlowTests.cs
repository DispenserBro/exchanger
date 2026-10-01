using Exchanger.Core.Configuration;
using Exchanger.Core.Session;
using Exchanger.Hardware.Abstractions;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class SessionFlowTests
{
    [Test]
    public void LowStock_AllowsPaymentWithoutInventingQuantity()
    {
        var machine = new FakeMachineController();
        using var flow = new SessionFlow(TestSettings.Create(), machine, new ManualClock());
        machine.RaiseStockLevel(MachineStockLevel.Low);

        flow.BeginCard();
        bool selected = flow.SelectCardAmount(5000);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(selected, Is.True);
            Assert.That(flow.StockLevel, Is.EqualTo(MachineStockLevel.Low));
        }
    }

    [Test]
    public void UnknownQualitativeStock_BlocksStartingPayment()
    {
        var machine = new FakeMachineController();
        using var flow = new SessionFlow(TestSettings.Create(), machine, new ManualClock());
        machine.RaiseStockLevel(MachineStockLevel.Unknown);

        bool started = flow.BeginCard();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(started, Is.False);
            Assert.That(flow.State, Is.EqualTo(SessionState.Home));
            Assert.That(machine.BeginCardCalls, Is.Zero);
        }
    }
    [Test]
    public void CardApproved_CompletesOnceAndAutomaticallyReturnsHome()
    {
        var clock = new ManualClock();
        var machine = new FakeMachineController();
        using var flow = new SessionFlow(TestSettings.Create(), machine, clock);

        Assert.That(flow.BeginCard(), Is.True);
        Assert.That(flow.SelectCardAmount(100), Is.True);
        machine.RaiseCardResult(MachineCardResult.Approved);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flow.State, Is.EqualTo(SessionState.Dispensing));
            Assert.That(flow.BaseTokens, Is.EqualTo(10));
            Assert.That(flow.BonusTokens, Is.EqualTo(2));
            Assert.That(machine.DispenseCalls, Is.EqualTo(1));
            Assert.That(machine.LastDispenseCount, Is.EqualTo(12));
        }

        machine.CompleteDispense(success: true);
        Assert.That(flow.State, Is.EqualTo(SessionState.Completed));

        clock.AdvanceMilliseconds(9999);
        flow.Update();
        Assert.That(flow.State, Is.EqualTo(SessionState.Completed));

        clock.AdvanceMilliseconds(1);
        flow.Update();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(flow.State, Is.EqualTo(SessionState.Home));
            Assert.That(flow.PaymentMethod, Is.EqualTo(PaymentMethod.None));
            Assert.That(flow.SelectedAmountRubles, Is.Zero);
            Assert.That(flow.TotalTokens, Is.Zero);
        }
    }

    [Test]
    public void CashBalance_ProducesConfirmedDispense()
    {
        var machine = new FakeMachineController();
        using var flow = new SessionFlow(TestSettings.Create(), machine, new ManualClock());

        Assert.That(flow.BeginCash(), Is.True);
        machine.RaiseCashBalance(50);
        Assert.That(flow.ConfirmCashDispense(), Is.True);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flow.State, Is.EqualTo(SessionState.Dispensing));
            Assert.That(flow.CashBalanceRubles, Is.EqualTo(50));
            Assert.That(machine.EndCashCalls, Is.EqualTo(1));
            Assert.That(machine.LastDispenseCount, Is.EqualTo(5));
        }

        machine.CompleteDispense(success: true);
        Assert.That(flow.State, Is.EqualTo(SessionState.Completed));
    }

    [Test]
    public void CardAmount_ExceedingInventory_IsRejectedBeforePaymentStarts()
    {
        var machine = new FakeMachineController();
        using var flow = new SessionFlow(
            TestSettings.Create(),
            machine,
            new ManualClock(),
            maximumAvailableTokens: 11);

        Assert.That(flow.BeginCard(), Is.True);
        bool selected = flow.SelectCardAmount(100);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(selected, Is.False);
            Assert.That(flow.State, Is.EqualTo(SessionState.Error));
            Assert.That(flow.ErrorCode, Is.EqualTo(SessionErrorCode.InsufficientTokenInventory));
            Assert.That(flow.TotalTokens, Is.EqualTo(12));
            Assert.That(machine.BeginCardCalls, Is.Zero);
        }
    }

    [Test]
    public void CardAmount_WithDisabledInventoryLimit_StartsNormally()
    {
        var machine = new FakeMachineController();
        using var flow = new SessionFlow(
            TestSettings.Create(),
            machine,
            new ManualClock(),
            maximumAvailableTokens: null);

        Assert.That(flow.BeginCard(), Is.True);
        Assert.That(flow.SelectCardAmount(5000), Is.True);
        Assert.That(machine.BeginCardCalls, Is.EqualTo(1));
    }

    [Test]
    public void CashAmount_ExceedingInventory_IsRejectedBeforeDispense()
    {
        var machine = new FakeMachineController();
        using var flow = new SessionFlow(
            TestSettings.Create(),
            machine,
            new ManualClock(),
            maximumAvailableTokens: 4);

        Assert.That(flow.BeginCash(), Is.True);
        machine.RaiseCashBalance(50);
        bool confirmed = flow.ConfirmCashDispense();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(confirmed, Is.False);
            Assert.That(flow.State, Is.EqualTo(SessionState.Error));
            Assert.That(flow.ErrorCode, Is.EqualTo(SessionErrorCode.InsufficientTokenInventory));
            Assert.That(machine.EndCashCalls, Is.EqualTo(1));
            Assert.That(machine.DispenseCalls, Is.Zero);
        }
    }

    [Test]
    public void CardDeclined_IgnoresLateApprovalAndReturnsHomeAfterErrorTimeout()
    {
        var clock = new ManualClock();
        var machine = new FakeMachineController();
        using var flow = new SessionFlow(TestSettings.Create(), machine, clock);

        flow.BeginCard();
        flow.SelectCardAmount(100);
        machine.RaiseCardResult(MachineCardResult.Declined);
        machine.RaiseCardResult(MachineCardResult.Approved);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flow.State, Is.EqualTo(SessionState.PaymentDeclined));
            Assert.That(machine.DispenseCalls, Is.Zero);
        }

        clock.AdvanceSeconds(45);
        flow.Update();
        Assert.That(flow.State, Is.EqualTo(SessionState.Home));
    }

    [Test]
    public void CardCancelled_ReturnsHomeWithoutDispense()
    {
        var machine = new FakeMachineController();
        using var flow = new SessionFlow(TestSettings.Create(), machine, new ManualClock());
        flow.BeginCard();
        flow.SelectCardAmount(100);

        machine.RaiseCardResult(MachineCardResult.Cancelled);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flow.State, Is.EqualTo(SessionState.Home));
            Assert.That(machine.DispenseCalls, Is.Zero);
        }
    }

    [Test]
    public void CardTerminalBack_CancelsActiveRequestAndReturnsToAmountSelection()
    {
        var machine = new FakeMachineController();
        using var flow = new SessionFlow(
            TestSettings.Create(),
            machine,
            new ManualClock(),
            new SequenceSessionIdGenerator());
        var finishedOperations = new List<SessionOperationSnapshot>();
        flow.OperationFinished += finishedOperations.Add;

        Assert.That(flow.BeginCard(), Is.True);
        string operationId = flow.CurrentOperationId;
        Assert.That(flow.SelectCardAmount(100), Is.True);

        bool returned = flow.ReturnToCardAmounts();
        machine.RaiseCardResult(MachineCardResult.Approved);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(returned, Is.True);
            Assert.That(flow.State, Is.EqualTo(SessionState.CardAmountSelection));
            Assert.That(flow.CurrentOperationId, Is.EqualTo(operationId));
            Assert.That(machine.CancelCalls, Is.EqualTo(1));
            Assert.That(machine.DispenseCalls, Is.Zero);
            Assert.That(finishedOperations, Is.Empty);
        }

        Assert.That(flow.SelectCardAmount(50), Is.True);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(flow.State, Is.EqualTo(SessionState.CardTerminalWait));
            Assert.That(machine.BeginCardCalls, Is.EqualTo(2));
            Assert.That(machine.LastCardAmount, Is.EqualTo(50));
        }
    }

    [Test]
    public void HardwarePaymentTimeout_UsesTypedTimeoutErrorWithoutDispense()
    {
        var machine = new FakeMachineController();
        using var flow = new SessionFlow(TestSettings.Create(), machine, new ManualClock());
        flow.BeginCard();
        flow.SelectCardAmount(100);

        machine.RaiseCardResult(MachineCardResult.Timeout);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flow.State, Is.EqualTo(SessionState.PaymentDeclined));
            Assert.That(flow.ErrorCode, Is.EqualTo(SessionErrorCode.PaymentTimeout));
            Assert.That(machine.DispenseCalls, Is.Zero);
        }
    }

    [Test]
    public void PaymentTimeout_IsImmediateWithManualClockAndNeverDispenses()
    {
        var clock = new ManualClock();
        var machine = new FakeMachineController();
        using var flow = new SessionFlow(TestSettings.Create(), machine, clock);

        flow.BeginCard();
        flow.SelectCardAmount(100);
        clock.AdvanceSeconds(120);
        flow.Update();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flow.State, Is.EqualTo(SessionState.PaymentDeclined));
            Assert.That(machine.CancelCalls, Is.EqualTo(1));
            Assert.That(machine.DispenseCalls, Is.Zero);
            Assert.That(flow.ErrorTitle, Does.Contain("ВРЕМЯ"));
        }
    }

    [Test]
    public void CashAndCashlessPayment_UseIndependentTimeouts()
    {
        AppSettings settings = TestSettings.Create();
        settings.Timeouts.CashPaymentSeconds = 30;
        settings.Timeouts.CashlessPaymentSeconds = 90;
        var clock = new ManualClock();
        var machine = new FakeMachineController();
        using var flow = new SessionFlow(settings, machine, clock);

        Assert.That(flow.BeginCash(), Is.True);
        clock.AdvanceSeconds(29);
        flow.Update();
        Assert.That(flow.State, Is.EqualTo(SessionState.CashAccepting));
        clock.AdvanceSeconds(1);
        flow.Update();
        Assert.That(flow.State, Is.EqualTo(SessionState.Home));

        Assert.That(flow.BeginCard(), Is.True);
        Assert.That(flow.SelectCardAmount(100), Is.True);
        clock.AdvanceSeconds(89);
        flow.Update();
        Assert.That(flow.State, Is.EqualTo(SessionState.CardTerminalWait));
        clock.AdvanceSeconds(1);
        flow.Update();
        Assert.That(flow.State, Is.EqualTo(SessionState.PaymentDeclined));
    }

    [Test]
    public void UserActivity_ExtendsIdleDeadline()
    {
        var clock = new ManualClock();
        var machine = new FakeMachineController();
        using var flow = new SessionFlow(TestSettings.Create(), machine, clock);

        flow.BeginCard();
        clock.AdvanceSeconds(44);
        flow.NotifyUserActivity();
        clock.AdvanceSeconds(44);
        flow.Update();
        Assert.That(flow.State, Is.EqualTo(SessionState.CardAmountSelection));

        clock.AdvanceSeconds(1);
        flow.Update();
        Assert.That(flow.State, Is.EqualTo(SessionState.Home));
    }

    [Test]
    public void LowSignal_DoesNotBlockCardPackage()
    {
        var machine = new FakeMachineController(MachineStockLevel.Low);
        using var flow = new SessionFlow(TestSettings.Create(), machine, new ManualClock());

        flow.BeginCard();
        bool accepted = flow.SelectCardAmount(100);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(accepted, Is.True);
            Assert.That(flow.State, Is.EqualTo(SessionState.CardTerminalWait));
            Assert.That(machine.BeginCardCalls, Is.EqualTo(1));
            Assert.That(machine.DispenseCalls, Is.Zero);
        }
    }

    [Test]
    public void DuplicateActions_StartOnlyOnePaymentAndOneDispense()
    {
        var machine = new FakeMachineController();
        using var flow = new SessionFlow(TestSettings.Create(), machine, new ManualClock());

        flow.BeginCard();
        Assert.That(flow.SelectCardAmount(100), Is.True);
        Assert.That(flow.SelectCardAmount(50), Is.False);
        machine.RaiseCardResult(MachineCardResult.Approved);
        machine.RaiseCardResult(MachineCardResult.Approved);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(machine.BeginCardCalls, Is.EqualTo(1));
            Assert.That(machine.LastCardAmount, Is.EqualTo(100));
            Assert.That(machine.DispenseCalls, Is.EqualTo(1));
        }
    }

    [Test]
    public void PartialDispense_TransitionsToSafeError()
    {
        var machine = new FakeMachineController();
        using var flow = new SessionFlow(TestSettings.Create(), machine, new ManualClock());

        flow.BeginCard();
        flow.SelectCardAmount(100);
        machine.RaiseCardResult(MachineCardResult.Approved);
        machine.CompleteDispense(success: true, dispensedTokens: 10);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flow.State, Is.EqualTo(SessionState.Error));
            Assert.That(flow.ErrorCode, Is.EqualTo(SessionErrorCode.PartialDispense));
            Assert.That(flow.ErrorAction, Is.EqualTo(SessionErrorAction.ContactSupport));
        }
    }

    [Test]
    public void CancelDuringDispense_IsIgnored()
    {
        var machine = new FakeMachineController();
        using var flow = new SessionFlow(TestSettings.Create(), machine, new ManualClock());

        flow.BeginCard();
        flow.SelectCardAmount(100);
        machine.RaiseCardResult(MachineCardResult.Approved);
        int cancelsBefore = machine.CancelCalls;
        flow.Cancel();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flow.State, Is.EqualTo(SessionState.Dispensing));
            Assert.That(machine.CancelCalls, Is.EqualTo(cancelsBefore));
        }
    }

    [Test]
    public void DisposedFlow_DoesNotReceiveMachineEvents()
    {
        var machine = new FakeMachineController();
        var flow = new SessionFlow(TestSettings.Create(), machine, new ManualClock());
        flow.BeginCash();
        flow.Dispose();

        machine.RaiseCashBalance(100);

        Assert.That(flow.CashBalanceRubles, Is.Zero);
    }

    [Test]
    public void RepeatedFlowCreation_DoesNotAccumulateMachineSubscriptions()
    {
        var machine = new FakeMachineController();

        for (int index = 0; index < 50; index++)
        {
            using var flow = new SessionFlow(TestSettings.Create(), machine, new ManualClock());
            Assert.That(machine.TotalSubscriberCount, Is.EqualTo(5));
        }

        Assert.That(machine.TotalSubscriberCount, Is.Zero);
    }

    [Test]
    public void RepeatedCompletedSessions_RemainDeterministic()
    {
        var machine = new FakeMachineController();
        using var flow = new SessionFlow(TestSettings.Create(), machine, new ManualClock());

        for (int index = 0; index < 20; index++)
        {
            Assert.That(flow.BeginCash(), Is.True);
            machine.RaiseCashBalance(10);
            Assert.That(flow.ConfirmCashDispense(), Is.True);
            machine.CompleteDispense(success: true);
            Assert.That(flow.State, Is.EqualTo(SessionState.Completed));
            flow.ReturnHome();
            Assert.That(flow.State, Is.EqualTo(SessionState.Home));
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(machine.DispenseCalls, Is.EqualTo(20));
            Assert.That(flow.StockLevel, Is.EqualTo(MachineStockLevel.Enough));
        }
    }

    [Test]
    public void BeginCash_WhenCashDisabled_IsRejectedBeforeHardwareCall()
    {
        AppSettings settings = TestSettings.Create();
        settings.Payments.CashEnabled = false;
        var machine = new FakeMachineController();
        using var flow = new SessionFlow(settings, machine, new ManualClock());

        bool started = flow.BeginCash();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(started, Is.False);
            Assert.That(flow.State, Is.EqualTo(SessionState.Home));
            Assert.That(machine.BeginCashCalls, Is.Zero);
        }
    }

    [Test]
    public void BeginCard_WhenCardDisabled_IsRejectedBeforeOperationStarts()
    {
        AppSettings settings = TestSettings.Create();
        settings.Payments.CardEnabled = false;
        var machine = new FakeMachineController();
        using var flow = new SessionFlow(settings, machine, new ManualClock());

        bool started = flow.BeginCard();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(started, Is.False);
            Assert.That(flow.State, Is.EqualTo(SessionState.Home));
            Assert.That(flow.CurrentOperationId, Is.Empty);
        }
    }
}
