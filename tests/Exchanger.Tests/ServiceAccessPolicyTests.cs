using Exchanger.Core.Configuration;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class ServiceAccessPolicyTests
{
    [Test]
    public void SetPin_StoresOnlySaltedCredentialAndAuthorizesEngineer()
    {
        var settings = new SecuritySettings();

        ServiceAccessPolicy.SetPin(settings, "987654");
        var policy = new ServiceAccessPolicy(settings);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.ServicePinEnabled, Is.True);
            Assert.That(settings.ServicePinSaltBase64, Is.Not.Empty);
            Assert.That(settings.ServicePinHashBase64, Is.Not.Empty);
            Assert.That(settings.ServicePinHashBase64, Does.Not.Contain("987654"));
            Assert.That(policy.Verify("987654").Level, Is.EqualTo(ServiceAccessLevel.Engineer));
            Assert.That(policy.Verify("1234").Status, Is.EqualTo(ServiceAccessStatus.InvalidPin));
        }
    }

    [Test]
    public void Verify_LocksAfterFiveFailuresAndRecoversAfterTimeout()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        var settings = new SecuritySettings();
        ServiceAccessPolicy.SetPin(settings, "1234");
        var policy = new ServiceAccessPolicy(settings, () => now);

        for (int attempt = 1; attempt < ServiceAccessPolicy.MaximumAttempts; attempt++)
        {
            ServiceAccessResult invalid = policy.Verify("0000");
            Assert.That(invalid.Status, Is.EqualTo(ServiceAccessStatus.InvalidPin));
        }

        ServiceAccessResult locked = policy.Verify("0000");
        now = now.AddSeconds(ServiceAccessPolicy.LockDurationSeconds - 1);
        ServiceAccessResult stillLocked = policy.Verify("1234");
        now = now.AddSeconds(2);
        ServiceAccessResult authorized = policy.Verify("1234");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(locked.Status, Is.EqualTo(ServiceAccessStatus.Locked));
            Assert.That(stillLocked.Status, Is.EqualTo(ServiceAccessStatus.Locked));
            Assert.That(authorized.Status, Is.EqualTo(ServiceAccessStatus.Authorized));
        }
    }

    [Test]
    public void MissingOrDamagedCredential_StartsAsUnavailable()
    {
        var disabled = new SecuritySettings();
        var missing = new SecuritySettings { ServicePinEnabled = true };
        var damaged = new SecuritySettings
        {
            ServicePinEnabled = true,
            ServicePinSaltBase64 = "not-base64",
            ServicePinHashBase64 = "also-not-base64",
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(new ServiceAccessPolicy(disabled).IsAvailable, Is.False);
            Assert.That(new ServiceAccessPolicy(missing).Verify("1234").Status, Is.EqualTo(ServiceAccessStatus.Unavailable));
            Assert.That(new ServiceAccessPolicy(damaged).IsAvailable, Is.False);
        }
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("abc1")]
    [TestCase("123")]
    [TestCase("1234567890123")]
    public void SetPin_RejectsInvalidPin(string? pin)
    {
        var settings = new SecuritySettings();
        Action action = () => ServiceAccessPolicy.SetPin(settings, pin!);

        Assert.Throws<ArgumentException>(action);
    }

    [Test]
    public void ClearPin_RemovesCredential()
    {
        var settings = new SecuritySettings();
        ServiceAccessPolicy.SetPin(settings, "1234");

        ServiceAccessPolicy.ClearPin(settings);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.ServicePinSaltBase64, Is.Empty);
            Assert.That(settings.ServicePinHashBase64, Is.Empty);
            Assert.That(new ServiceAccessPolicy(settings).IsAvailable, Is.False);
        }
    }
}
