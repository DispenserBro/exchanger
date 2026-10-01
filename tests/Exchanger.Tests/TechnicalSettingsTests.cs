using Exchanger.Core.Configuration;
using Exchanger.Core.TechnicalConfiguration;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class TechnicalSettingsTests
{
    private string _directory = null!;
    private string _defaultPath = null!;
    private string _userPath = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "Exchanger.TechnicalSettingsTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _defaultPath = Path.Combine(_directory, "default.toml");
        _userPath = Path.Combine(_directory, "user", "technical_settings.toml");
        File.WriteAllText(_defaultPath, ValidToml);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public void Load_FirstRunCreatesReadableUserCopyAndUsesIt()
    {
        var service = new TechnicalSettingsService(_defaultPath, _userPath);

        TechnicalSettings settings = service.Load();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.Exists(_userPath), Is.True);
            Assert.That(File.ReadAllText(_userPath), Is.EqualTo(ValidToml));
            Assert.That(settings.Controller.Port, Is.EqualTo(ValidControllerPort));
            Assert.That(settings.Controller.UseMock, Is.False);
            Assert.That(settings.Controller.ReconnectIntervalSeconds, Is.EqualTo(5));
            Assert.That(settings.Hopper1.StockSensorInput, Is.EqualTo(1));
            Assert.That(settings.Hopper1.DispenseSensorInput, Is.EqualTo(24));
            Assert.That(settings.Hopper2.StockSensorInput, Is.EqualTo(4));
            Assert.That(settings.Hopper2.DispenseSensorInput, Is.EqualTo(25));
            Assert.That(settings.Service.ButtonInput, Is.EqualTo(8));
            Assert.That(settings.Theme?.Enabled, Is.True);
            Assert.That(settings.Theme?.Directory, Is.EqualTo("user://themes"));
            Assert.That(settings.Theme?.FileName, Is.EqualTo("night-theme"));
            Assert.That(settings.Theme?.BuildPackagePath(), Is.EqualTo("user://themes/night-theme.pck"));
        }
    }

    [Test]
    public void Load_InvalidUserFileIsPreservedAndFallsBackToSafeDefault()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_userPath)!);
        File.WriteAllText(_userPath, "[controller]\nuse_mock = false\n");
        var warnings = new List<string>();
        var service = new TechnicalSettingsService(_defaultPath, _userPath, warnings.Add);

        TechnicalSettings settings = service.Load();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.Controller.Port, Is.EqualTo(ValidControllerPort));
            Assert.That(File.ReadAllText(_userPath), Is.EqualTo("[controller]\nuse_mock = false\n"));
            Assert.That(warnings, Has.Count.EqualTo(1));
            Assert.That(warnings[0], Does.Contain("не применена"));
            Assert.That(warnings[0], Does.Contain("безопасный профиль"));
        }
    }

    [Test]
    public void TryParse_MissingRequiredValueIsRejected()
    {
        string content = ValidToml.Replace("button_input = 8\n", string.Empty, StringComparison.Ordinal);

        bool valid = TechnicalSettingsService.TryParse(content, out _, out IReadOnlyList<string> issues);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(valid, Is.False);
            Assert.That(issues, Has.Some.Contains("service.button_input"));
        }
    }

    [Test]
    public void TryParse_DuplicateActiveInputIsRejected()
    {
        string content = ValidToml.Replace("button_input = 8", "button_input = 1", StringComparison.Ordinal);

        bool valid = TechnicalSettingsService.TryParse(content, out _, out IReadOnlyList<string> issues);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(valid, Is.False);
            Assert.That(issues, Has.Some.Contains("IN1 одновременно назначен"));
        }
    }

    [TestCase(true, true, "DualHopperAuto")]
    [TestCase(true, false, "Hopper1Auto")]
    [TestCase(false, true, "Hopper2Auto")]
    [TestCase(false, false, "Unsupported")]
    public void ResolveDispenseMode_UsesEnabledHoppers(bool hopper1, bool hopper2, string expected)
    {
        Assert.That(TechnicalSettings.ResolveDispenseMode(hopper1, hopper2), Is.EqualTo(expected));
    }

    [TestCase("DualHopperAuto", true, true)]
    [TestCase("Hopper1Auto", true, false)]
    [TestCase("hopper2auto", false, true)]
    [TestCase("Unsupported", false, false)]
    [TestCase(null, false, false)]
    public void ResolveConfiguredHoppers_UsesDispenseProfile(
        string? dispenseMode,
        bool hopper1,
        bool hopper2)
    {
        var configured = TechnicalSettings.ResolveConfiguredHoppers(dispenseMode);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(configured.Hopper1Enabled, Is.EqualTo(hopper1));
            Assert.That(configured.Hopper2Enabled, Is.EqualTo(hopper2));
        }
    }

    [Test]
    public void ApplyTo_DisabledHopperRemovesItsInputsAndSelectsSecondHopper()
    {
        Assert.That(
            TechnicalSettingsService.TryParse(
                ValidToml.Replace(
                    "[hopper_1]\nenabled = true",
                    "[hopper_1]\nenabled = false",
                    StringComparison.Ordinal),
                out TechnicalSettings? technical,
                out IReadOnlyList<string> issues),
            Is.True,
            string.Join(" ", issues));
        var hardware = new HardwareSettings();

        technical!.ApplyTo(hardware);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hardware.UseMock, Is.False);
            Assert.That(hardware.PortName, Is.EqualTo(ValidControllerPort));
            Assert.That(hardware.DispenseMode, Is.EqualTo("Hopper2Auto"));
            Assert.That(hardware.StockSensorInputIndex, Is.EqualTo(-1));
            Assert.That(hardware.Hopper1DispenseSensorInputIndex, Is.EqualTo(-1));
            Assert.That(hardware.Hopper2StockSensorInputIndex, Is.EqualTo(4));
            Assert.That(hardware.Hopper2DispenseSensorInputIndex, Is.EqualTo(25));
            Assert.That(hardware.ServiceButtonInputIndex, Is.EqualTo(8));
            Assert.That(hardware.InputPollMilliseconds, Is.EqualTo(100));
        }
    }

    [Test]
    public void ApplyTo_ThemeBuildsPckPathFromDirectoryAndExtensionlessFileName()
    {
        Assert.That(
            TechnicalSettingsService.TryParse(
                ValidToml,
                out TechnicalSettings? technical,
                out IReadOnlyList<string> issues),
            Is.True,
            string.Join(" ", issues));
        var themes = new ThemeSettings
        {
            ExternalDlcEnabled = false,
            ExternalDlcPath = "user://old/theme.pck",
        };

        technical!.ApplyTo(themes);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(themes.ExternalDlcEnabled, Is.True);
            Assert.That(themes.ExternalDlcPath, Is.EqualTo("user://themes/night-theme.pck"));
        }
    }

    [Test]
    public void TryParse_ThemeSectionIsOptionalForExistingVersionOneFiles()
    {
        string content = ValidToml.Replace(
            "[theme]\nenabled = true\ndirectory = \"user://themes\"\nfile_name = \"night-theme\"\n\n",
            string.Empty,
            StringComparison.Ordinal);

        bool valid = TechnicalSettingsService.TryParse(
            content,
            out TechnicalSettings? technical,
            out IReadOnlyList<string> issues);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(valid, Is.True, string.Join(" ", issues));
            Assert.That(technical?.Theme, Is.Null);
        }
    }

    [TestCase("night-theme.pck", "без расширения")]
    [TestCase("folder/night-theme", "недопустимое имя")]
    public void TryParse_InvalidThemeFileNameIsRejected(string fileName, string expectedIssue)
    {
        string content = ValidToml.Replace(
            "file_name = \"night-theme\"",
            $"file_name = \"{fileName}\"",
            StringComparison.Ordinal);

        bool valid = TechnicalSettingsService.TryParse(content, out _, out IReadOnlyList<string> issues);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(valid, Is.False);
            Assert.That(issues, Has.Some.Contains(expectedIssue));
        }
    }

    [Test]
    public void TryParse_RelativeThemeDirectoryIsRejected()
    {
        string content = ValidToml.Replace(
            "directory = \"user://themes\"",
            "directory = \"themes\"",
            StringComparison.Ordinal);

        bool valid = TechnicalSettingsService.TryParse(content, out _, out IReadOnlyList<string> issues);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(valid, Is.False);
            Assert.That(issues, Has.Some.Contains("theme.directory"));
        }
    }

    [Test]
    public void ThemePackagePath_PreservesUserRootPrefix()
    {
        var theme = new ThemeTechnicalSettings(true, "user://", "space-pixel");

        Assert.That(theme.BuildPackagePath(), Is.EqualTo("user://space-pixel.pck"));
    }

    [Test]
    [Platform("Win")]
    public void TryParse_WindowsLiteralThemeDirectoryPreservesBackslashes()
    {
        string content = ValidToml.Replace(
            "directory = \"user://themes\"",
            @"directory = 'D:\Exchanger\themes'",
            StringComparison.Ordinal);

        bool valid = TechnicalSettingsService.TryParse(
            content,
            out TechnicalSettings? technical,
            out IReadOnlyList<string> issues);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(valid, Is.True, string.Join(" ", issues));
            Assert.That(technical?.Theme?.Directory, Is.EqualTo(@"D:\Exchanger\themes"));
            Assert.That(
                technical?.Theme?.BuildPackagePath(),
                Is.EqualTo(@"D:\Exchanger\themes\night-theme.pck"));
        }
    }

    [Test]
    public void RepositoryTemplate_IsValidAndSafeByDefault()
    {
        string path = Path.Combine(ProjectPaths.FindRoot(), "config", "technical_settings.toml");
        string content = File.ReadAllText(path);

        bool valid = TechnicalSettingsService.TryParse(
            content,
            out TechnicalSettings? settings,
            out IReadOnlyList<string> issues);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(valid, Is.True, string.Join(" ", issues));
            Assert.That(settings!.Controller.UseMock, Is.True);
            Assert.That(settings.Controller.ReconnectIntervalSeconds, Is.EqualTo(30));
            Assert.That(settings.Hopper1.Enabled, Is.True);
            Assert.That(settings.Hopper2.Enabled, Is.True);
            Assert.That(settings.Theme?.Enabled, Is.True);
            Assert.That(settings.Theme?.BuildPackagePath(), Is.EqualTo("user://theme_dlc/active_theme.pck"));
            Assert.That(settings.Validate(), Is.Empty);
        }
    }

    [Test]
    public void Validate_ControllerPortUsesRequestedPlatformRules()
    {
        Assert.That(
            TechnicalSettingsService.TryParse(
                ValidToml,
                out TechnicalSettings? currentSettings,
                out IReadOnlyList<string> parseIssues),
            Is.True,
            string.Join(" ", parseIssues));
        var linuxSettings = new TechnicalSettings
        {
            Version = currentSettings!.Version,
            Controller = currentSettings.Controller with
            {
                UseMock = false,
                Port = "/dev/ttyUSB0",
            },
            Hopper1 = currentSettings.Hopper1,
            Hopper2 = currentSettings.Hopper2,
            Service = currentSettings.Service,
            Polling = currentSettings.Polling,
            Theme = currentSettings.Theme,
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(linuxSettings.Validate(SerialPortPlatform.Linux), Is.Empty);
            Assert.That(
                linuxSettings.Validate(SerialPortPlatform.Windows),
                Has.Some.Contains("controller.port"));
        }
    }

    private static string ValidControllerPort => OperatingSystem.IsLinux() ? "/dev/ttyUSB7" : "COM7";

    private static string ValidToml => ValidTomlTemplate.Replace("{PORT}", ValidControllerPort, StringComparison.Ordinal);

    private const string ValidTomlTemplate = """
        version = 1

        [theme]
        enabled = true
        directory = "user://themes"
        file_name = "night-theme"

        [controller]
        use_mock = false
        port = "{PORT}"
        baud_rate = 115200
        reconnect_interval_seconds = 5
        handshake_timeout_ms = 2000
        frame_timeout_ms = 100
        max_frame_length = 1024
        cashless_keep_alive_seconds = 120

        [hopper_1]
        enabled = true
        stock_sensor_input = 1
        stock_low_active_high = false
        dispense_sensor_input = 24
        dispense_pulse_active_high = false

        [hopper_2]
        enabled = true
        stock_sensor_input = 4
        stock_low_active_high = false
        dispense_sensor_input = 25
        dispense_pulse_active_high = false

        [service]
        button_input = 8
        button_active_high = false

        [polling]
        input_poll_ms = 100
        stock_poll_seconds = 5
        """;
}
