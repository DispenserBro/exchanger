using Exchanger.Hardware.SerialPortHardware;
using NUnit.Framework;

namespace Exchanger.Tests;

public sealed class SerialPortHardwareTests
{
    [TestCase(typeof(OperationCanceledException), true)]
    [TestCase(typeof(InvalidOperationException), true)]
    [TestCase(typeof(UnauthorizedAccessException), true)]
    [TestCase(typeof(IOException), true)]
    [TestCase(typeof(TimeoutException), false)]
    [TestCase(typeof(ArgumentException), false)]
    public void WorkerTerminationClassification_HandlesOnlyExpectedSerialFailures(
        Type exceptionType,
        bool expected)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        Assert.That(SerialPortHardware.IsWorkerTerminationException(exception), Is.EqualTo(expected));
    }
}
