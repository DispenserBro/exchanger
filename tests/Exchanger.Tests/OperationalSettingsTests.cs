using Exchanger.Core.Configuration;
using Exchanger.Core.Logging;
using NUnit.Framework;
using System.Text.Json;

namespace Exchanger.Tests;

[TestFixture]
public sealed class OperationalSettingsTests
{
    [TestCase("Info", true)]
    [TestCase("warning", true)]
    [TestCase("Error", true)]
    [TestCase("Trace", false)]
    public void LoggingValidator_RestrictsLevels(string level, bool valid)
    {
        var settings = new LoggingSettings { MinimumLevel = level };

        Assert.That(LoggingSettingsValidator.Validate(settings).IsValid, Is.EqualTo(valid));
    }

    [TestCase(0, 14)]
    [TestCase(101, 14)]
    [TestCase(5, 0)]
    [TestCase(5, 366)]
    public void LoggingValidator_RejectsUnsafeBounds(int sizeMb, int retentionDays)
    {
        var settings = new LoggingSettings { MaxFileSizeMb = sizeMb, RetentionDays = retentionDays };

        Assert.That(LoggingSettingsValidator.Validate(settings).IsValid, Is.False);
    }

    [TestCase("COM1", true)]
    [TestCase("com256", true)]
    [TestCase("COM0", false)]
    [TestCase("COM257", false)]
    [TestCase("/dev/ttyUSB0", false)]
    public void HardwareValidator_UsesWindowsPortRules(string port, bool valid)
    {
        var settings = new HardwareSettings { UseMock = false, PortName = port };

        Assert.That(
            HardwareSettingsValidator.Validate(settings, SerialPortPlatform.Windows).IsValid,
            Is.EqualTo(valid));
    }

    [TestCase("/dev/ttyUSB0", true)]
    [TestCase("ttyUSB0", true)]
    [TestCase("/dev/ttyUSB12", true)]
    [TestCase("/dev/ttyACM0", true)]
    [TestCase("/dev/serial/by-id/usb-Silicon_Labs_CP2102-if00-port0", true)]
    [TestCase("COM3", false)]
    [TestCase("/dev/ttyUSB", true)]
    [TestCase("/dev/ttyS0", false)]
    [TestCase("/dev/serial/by-id/", false)]
    [TestCase("/dev/serial/by-id/../ttyUSB0", false)]
    [TestCase("/dev/serial/by-path/pci-controller", false)]
    public void HardwareValidator_UsesLinuxPortRules(string port, bool valid)
    {
        var settings = new HardwareSettings { UseMock = false, PortName = port };

        Assert.That(
            HardwareSettingsValidator.Validate(settings, SerialPortPlatform.Linux).IsValid,
            Is.EqualTo(valid));
    }

    [TestCase(SerialPortPlatform.Windows)]
    [TestCase(SerialPortPlatform.Linux)]
    public void HardwareValidator_MockModeDoesNotRequirePlatformSpecificPort(
        SerialPortPlatform platform)
    {
        var settings = new HardwareSettings { UseMock = true, PortName = "COM3" };

        Assert.That(HardwareSettingsValidator.Validate(settings, platform).IsValid, Is.True);
    }

    [Test]
    public void SettingsChangeDetector_ReportsSectionsWithoutValues()
    {
        var before = new AppSettings();
        before.Normalize();
        var after = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(before))!;
        after.Branding.ApplicationName = "СЕКРЕТНОЕ НАЗВАНИЕ";
        after.Hardware.PortName = "COM77";

        SettingsChangeSummary summary = SettingsChangeDetector.Compare(before, after);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summary.ChangedSections, Is.EqualTo(new[] { "Общие", "Оборудование" }));
            Assert.That(summary.RestartRequiredSections, Is.EqualTo(new[] { "Оборудование" }));
            Assert.That(JsonSerializer.Serialize(summary), Does.Not.Contain("СЕКРЕТНОЕ"));
            Assert.That(JsonSerializer.Serialize(summary), Does.Not.Contain("COM77"));
        }
    }

    [Test]
    public void SettingsChangeDetector_ReportsMenuVisibilityAsImmediateServiceChange()
    {
        var before = new AppSettings();
        before.Normalize();
        var after = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(before))!;
        after.MenuVisibility.ShowPromotionBlock = false;

        SettingsChangeSummary summary = SettingsChangeDetector.Compare(before, after);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summary.ChangedSections, Is.EqualTo(new[] { "Сервис" }));
            Assert.That(summary.RestartRequiredSections, Is.Empty);
        }
    }

    [Test]
    public void SettingsChangeDetector_ReportsInventoryAccountingAsImmediateServiceChange()
    {
        var before = new AppSettings();
        before.Normalize();
        var after = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(before))!;
        after.TokenInventory.Enabled = false;

        SettingsChangeSummary summary = SettingsChangeDetector.Compare(before, after);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summary.ChangedSections, Is.EqualTo(new[] { "Сервис" }));
            Assert.That(summary.RestartRequiredSections, Is.Empty);
        }
    }

    [Test]
    public void SettingsChangeDetector_ReportsPurchaseLimitAsImmediateServiceChange()
    {
        var before = new AppSettings();
        before.Normalize();
        var after = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(before))!;
        after.TokenInventory.PurchaseLimitEnabled = false;

        SettingsChangeSummary summary = SettingsChangeDetector.Compare(before, after);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summary.ChangedSections, Is.EqualTo(new[] { "Сервис" }));
            Assert.That(summary.RestartRequiredSections, Is.Empty);
        }
    }

    [Test]
    public void ConfigurationAuditStore_WritesOnlySafeMetadata()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "settings-audit.jsonl");
            var store = new ConfigurationAuditStore(path);
            var summary = new SettingsChangeSummary(
                new[] { "Общие", "Оборудование" },
                new[] { "Оборудование" });

            store.Record(summary, "Engineer", DateTimeOffset.UnixEpoch);

            string json = File.ReadAllText(path);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(json, Does.Contain("Engineer"));
                Assert.That(json, Does.Contain("Оборудование"));
                Assert.That(json, Does.Not.Contain("COM"));
                Assert.That(json, Does.Not.Contain("Path"));
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void LogMaintenance_DeletesExpiredAndOldestArchivesButProtectsActiveFiles()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string active = WriteSizedFile(directory, "exchanger.log", 20, DateTime.UtcNow);
            WriteSizedFile(directory, "exchanger-old.log", 20, DateTime.UtcNow.AddDays(-30));
            string archive1 = WriteSizedFile(directory, "operations-1.jsonl", 20, DateTime.UtcNow.AddHours(-2));
            string archive2 = WriteSizedFile(directory, "operations-2.jsonl", 20, DateTime.UtcNow.AddHours(-1));

            LogMaintenanceResult result = LogMaintenance.Prune(directory, 14, 45, active);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.ExpiredDeleted, Is.EqualTo(1));
                Assert.That(result.QuotaDeleted, Is.EqualTo(1));
                Assert.That(File.Exists(active), Is.True);
                Assert.That(File.Exists(archive1), Is.False);
                Assert.That(File.Exists(archive2), Is.True);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestCase("Ошибка user://config/private.json", "Подробности с локальным путём скрыты.")]
    [TestCase("RAW FRAME 07ABCDEF", "Содержимое протокольного сообщения скрыто.")]
    [TestCase("Безопасная ошибка соединения", "Безопасная ошибка соединения")]
    public void SafeDiagnosticText_HidesPathsAndProtocolFrames(string source, string expected)
    {
        Assert.That(SafeDiagnosticText.Sanitize(source), Is.EqualTo(expected));
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "Exchanger.OperationalSettingsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string WriteSizedFile(string directory, string name, int bytes, DateTime timestamp)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllBytes(path, new byte[bytes]);
        File.SetLastWriteTimeUtc(path, timestamp);
        return path;
    }
}
