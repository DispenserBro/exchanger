using Exchanger.Core.Session;
using Exchanger.Hardware.Abstractions;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class SessionOperationTests
{
    [Test]
    public void ConsecutiveOperations_HaveUniqueIdsAndOneFinalOutcomeEach()
    {
        var machine = new FakeMachineController();
        var ids = new SequenceSessionIdGenerator();
        using var flow = new SessionFlow(TestSettings.Create(), machine, new ManualClock(), ids);
        var started = new List<SessionOperationSnapshot>();
        var finished = new List<SessionOperationSnapshot>();
        flow.OperationStarted += started.Add;
        flow.OperationFinished += finished.Add;

        flow.BeginCash();
        flow.Cancel();
        flow.BeginCard();
        flow.SelectCardAmount(100);
        machine.RaiseCardResult(MachineCardResult.Declined);
        flow.ReturnHome();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(started.Select(item => item.OperationId), Is.EqualTo(new[] { "operation-0001", "operation-0002" }));
            Assert.That(finished.Select(item => item.OperationId), Is.EqualTo(new[] { "operation-0001", "operation-0002" }));
            Assert.That(finished.Select(item => item.Outcome), Is.EqualTo(new[] { OperationOutcome.Cancelled, OperationOutcome.PaymentDeclined }));
            Assert.That(finished[1].AmountRubles, Is.EqualTo(100));
            Assert.That(finished[1].ErrorCode, Is.EqualTo(SessionErrorCode.PaymentDeclined));
        }
    }

    [Test]
    public void DisposeActiveOperation_RecordsInterruptedOutcome()
    {
        var machine = new FakeMachineController();
        var flow = new SessionFlow(TestSettings.Create(), machine, new ManualClock(), new SequenceSessionIdGenerator());
        SessionOperationSnapshot? finished = null;
        flow.OperationFinished += snapshot => finished = snapshot;
        flow.BeginCard();

        flow.Dispose();

        Assert.That(finished, Is.Not.Null);
        Assert.That(finished!.Value.Outcome, Is.EqualTo(OperationOutcome.Interrupted));
    }

    [Test]
    public void ConnectionLossBeforeDispense_UsesTypedSafeError()
    {
        var machine = new FakeMachineController();
        using var flow = new SessionFlow(TestSettings.Create(), machine, new ManualClock(), new SequenceSessionIdGenerator());
        SessionOperationSnapshot? finished = null;
        flow.OperationFinished += snapshot => finished = snapshot;
        flow.BeginCard();
        flow.SelectCardAmount(100);

        machine.RaiseFault(MachineFault.ConnectionLost);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flow.State, Is.EqualTo(SessionState.Error));
            Assert.That(flow.ErrorCode, Is.EqualTo(SessionErrorCode.ConnectionLost));
            Assert.That(flow.ErrorAction, Is.EqualTo(SessionErrorAction.ContactSupport));
            Assert.That(finished!.Value.Outcome, Is.EqualTo(OperationOutcome.ConnectionLost));
        }
    }

    [Test]
    public void ConnectionLossDuringDispense_ForbidsAssumingTheResult()
    {
        var machine = new FakeMachineController();
        using var flow = new SessionFlow(TestSettings.Create(), machine, new ManualClock(), new SequenceSessionIdGenerator());
        SessionOperationSnapshot? finished = null;
        flow.OperationFinished += snapshot => finished = snapshot;
        flow.BeginCard();
        flow.SelectCardAmount(100);
        machine.RaiseCardResult(MachineCardResult.Approved);

        machine.RaiseFault(MachineFault.ConnectionLost);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flow.State, Is.EqualTo(SessionState.Error));
            Assert.That(flow.ErrorCode, Is.EqualTo(SessionErrorCode.DispenseStatusUnknown));
            Assert.That(flow.ErrorMessage, Does.Contain("Не повторяйте оплату"));
            Assert.That(finished!.Value.Outcome, Is.EqualTo(OperationOutcome.DispenseStatusUnknown));
        }
    }

    [Test]
    public void RawMachineError_IsNeverShownToUser()
    {
        var machine = new FakeMachineController();
        using var flow = new SessionFlow(TestSettings.Create(), machine, new ManualClock());
        flow.BeginCard();
        flow.SelectCardAmount(100);
        machine.RaiseCardResult(MachineCardResult.Approved);

        machine.CompleteDispense(success: false, dispensedTokens: 0, error: "SECRET RAW CONTROLLER PAYLOAD");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flow.ErrorCode, Is.EqualTo(SessionErrorCode.DispenseFailed));
            Assert.That(flow.ErrorMessage, Does.Not.Contain("SECRET"));
            Assert.That(flow.ErrorMessage, Does.Not.Contain("CONTROLLER"));
        }
    }
}
