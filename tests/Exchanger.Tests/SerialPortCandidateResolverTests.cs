using Exchanger.Core.Configuration;
using NUnit.Framework;
using System.Collections.Generic;

namespace Exchanger.Tests;

[TestFixture]
public sealed class SerialPortCandidateResolverTests
{
    [TestCase("COM", SerialPortPlatform.Windows, true)]
    [TestCase("ttyUSB", SerialPortPlatform.Linux, true)]
    [TestCase("/dev/ttyUSB", SerialPortPlatform.Linux, true)]
    [TestCase("COM3", SerialPortPlatform.Windows, false)]
    [TestCase("/dev/ttyUSB0", SerialPortPlatform.Linux, false)]
    public void IsAutomaticSearchPrefix_RecognizesOnlyConfiguredPrefixes(
        string configuredPort,
        SerialPortPlatform platform,
        bool expected)
    {
        Assert.That(
            SerialPortCandidateResolver.IsAutomaticSearchPrefix(configuredPort, platform),
            Is.EqualTo(expected));
    }

    [Test]
    public void ResolveCandidates_AutomaticWindowsComSearch_SortsCandidatesFromZero()
    {
        IReadOnlyList<string> candidates = SerialPortCandidateResolver.ResolveCandidates(
            "COM",
            SerialPortPlatform.Windows,
            new[] { "COM12", "COM3", "LPT1", "com0", "COM3" });

        Assert.That(candidates, Is.EqualTo(new[] { "COM0", "COM3", "COM12" }));
    }

    [Test]
    public void ResolveCandidates_AutomaticLinuxTtyUsbSearch_SortsCandidatesFromZero()
    {
        IReadOnlyList<string> candidates = SerialPortCandidateResolver.ResolveCandidates(
            "ttyUSB",
            SerialPortPlatform.Linux,
            new[] { "/dev/ttyUSB12", "ttyUSB3", "/dev/ttyACM0", "/dev/ttyUSB0", "/dev/ttyUSB3" });

        Assert.That(candidates, Is.EqualTo(new[] { "/dev/ttyUSB0", "/dev/ttyUSB3", "/dev/ttyUSB12" }));
    }

    [Test]
    public void ResolveCandidates_ExplicitPort_DoesNotEnumerateAlternatives()
    {
        IReadOnlyList<string> candidates = SerialPortCandidateResolver.ResolveCandidates(
            "COM7",
            SerialPortPlatform.Windows,
            new[] { "COM0", "COM1" });

        Assert.That(candidates, Is.EqualTo(new[] { "COM7" }));
    }

    [Test]
    public void NormalizePortName_LinuxShortTtyUsb_UsesDevicePath()
    {
        Assert.That(
            SerialPortCandidateResolver.NormalizePortName("ttyUSB7", SerialPortPlatform.Linux),
            Is.EqualTo("/dev/ttyUSB7"));
    }
}
