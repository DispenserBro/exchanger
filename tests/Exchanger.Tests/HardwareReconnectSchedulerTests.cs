using Exchanger.Hardware.Abstractions;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class HardwareReconnectSchedulerTests
{
    [Test]
    public void InitialConnectionFailure_WaitsFullConfiguredIntervalAndRearms()
    {
        var scheduler = new HardwareReconnectScheduler(intervalSeconds: 30);
        scheduler.ScheduleFrom(nowMilliseconds: 1_000);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(scheduler.TryBeginAttempt(30_999), Is.False);
            Assert.That(scheduler.TryBeginAttempt(31_000), Is.True);
            Assert.That(scheduler.TryBeginAttempt(60_999), Is.False);
            Assert.That(scheduler.TryBeginAttempt(61_000), Is.True);
        }
    }

    [Test]
    public void RuntimeDisconnect_ReplacesExpiredDeadlineWithFullConfiguredInterval()
    {
        var scheduler = new HardwareReconnectScheduler(intervalSeconds: 30);
        scheduler.ScheduleFrom(nowMilliseconds: 0);

        Assert.That(scheduler.ObserveConnection(isConnected: false, nowMilliseconds: 0), Is.False);
        Assert.That(scheduler.ObserveConnection(isConnected: true, nowMilliseconds: 10_000), Is.False);
        Assert.That(scheduler.ObserveConnection(isConnected: false, nowMilliseconds: 600_000), Is.True);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(scheduler.TryBeginAttempt(629_999), Is.False);
            Assert.That(scheduler.TryBeginAttempt(630_000), Is.True);
        }
    }

    [Test]
    public void InvalidInterval_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            (Action)(() => _ = new HardwareReconnectScheduler(intervalSeconds: 0)));
    }
}
