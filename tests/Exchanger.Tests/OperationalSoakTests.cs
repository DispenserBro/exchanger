using Exchanger.Core.Session;
using Exchanger.Hardware.Abstractions;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class OperationalSoakTests
{
    [Test]
    public void AcceleratedTwentyFourHours_StaysDeterministicWithoutSubscriptionGrowth()
    {
        const int simulatedMinutes = 24 * 60;
        var machine = new FakeMachineController();
        var clock = new ManualClock();
        using var flow = new SessionFlow(TestSettings.Create(), machine, clock, new SequenceSessionIdGenerator());

        for (int minute = 0; minute < simulatedMinutes; minute++)
        {
            if (minute % 3 == 0)
            {
                Assert.That(flow.BeginCash(), Is.True, $"minute={minute}");
                machine.RaiseCashBalance(10);
                Assert.That(flow.ConfirmCashDispense(), Is.True, $"minute={minute}");
                machine.CompleteDispense(success: true);
                Assert.That(flow.State, Is.EqualTo(SessionState.Completed), $"minute={minute}");
                flow.ReturnHome();
            }

            clock.AdvanceSeconds(60);
            flow.Update();
            Assert.That(flow.State, Is.EqualTo(SessionState.Home), $"minute={minute}");
            Assert.That(machine.TotalSubscriberCount, Is.EqualTo(5), $"minute={minute}");
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(machine.DispenseCalls, Is.EqualTo(simulatedMinutes / 3));
            Assert.That(flow.StockLevel, Is.EqualTo(MachineStockLevel.Enough));
        }
    }

    [Test]
    public void TenThousandFlowLifecycles_ReleaseAllSubscriptions()
    {
        var machine = new FakeMachineController();

        for (int index = 0; index < 10_000; index++)
        {
            using var flow = new SessionFlow(TestSettings.Create(), machine, new ManualClock());
        }

        Assert.That(machine.TotalSubscriberCount, Is.Zero);
    }
}
