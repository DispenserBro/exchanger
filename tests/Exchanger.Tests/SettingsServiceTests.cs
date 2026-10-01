using Exchanger.Core.Configuration;
using Exchanger.Hardware.PeripheryController;
using NUnit.Framework;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Exchanger.Tests;

[TestFixture]
public sealed class SettingsServiceTests
{
    private string _directory = null!;
    private string _defaultPath = null!;
    private string _userPath = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "Exchanger.SettingsServiceTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _defaultPath = Path.Combine(_directory, "default_settings.json");
        _userPath = Path.Combine(_directory, "config", "appsettings.json");
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
    public void Load_FirstRunCreatesNormalizedUserSettings()
    {
        WriteSettings(_defaultPath, CreateDefaults(masterVolume: 35));
        var service = new SettingsService(_defaultPath, _userPath);

        AppSettings loaded = service.Load();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(loaded.Audio.MasterVolumePercent, Is.EqualTo(35));
            Assert.That(loaded.ConfigurationVersion, Is.EqualTo(AppSettings.CurrentConfigurationVersion));
            Assert.That(service.LastLoadSource, Is.EqualTo(SettingsLoadSource.DefaultsCreated));
            Assert.That(File.Exists(_userPath), Is.True);
            Assert.That(File.Exists(_userPath + SettingsService.BackupSuffix), Is.False);
            Assert.That(File.Exists(_userPath + SettingsService.TemporarySuffix), Is.False);
        }
    }

    [Test]
    public void Save_NormalizesCandidateAndKeepsPreviousFileAsBackup()
    {
        WriteSettings(_defaultPath, CreateDefaults(masterVolume: 40));
        var service = new SettingsService(_defaultPath, _userPath);
        service.Load();

        AppSettings workingCopy = service.CreateWorkingCopy();
        workingCopy.Pricing.TokenPriceRubles = 25;
        workingCopy.Audio.MasterVolumePercent = 180;
        AppSettings saved = service.Save(workingCopy);

        AppSettings user = ReadSettings(_userPath);
        AppSettings backup = ReadSettings(_userPath + SettingsService.BackupSuffix);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved.Pricing.TokenPriceRubles, Is.EqualTo(25));
            Assert.That(saved.Audio.MasterVolumePercent, Is.EqualTo(100));
            Assert.That(user.Pricing.TokenPriceRubles, Is.EqualTo(25));
            Assert.That(backup.Pricing.TokenPriceRubles, Is.EqualTo(10));
            Assert.That(backup.Audio.MasterVolumePercent, Is.EqualTo(40));
            Assert.That(File.Exists(_userPath + SettingsService.TemporarySuffix), Is.False);
        }
    }

    [Test]
    public void Load_CorruptUserFileRecoversLastValidBackup()
    {
        WriteSettings(_defaultPath, CreateDefaults(masterVolume: 100));
        var firstService = new SettingsService(_defaultPath, _userPath);
        firstService.Load();
        AppSettings changed = firstService.CreateWorkingCopy();
        changed.Pricing.TokenPriceRubles = 20;
        firstService.Save(changed);
        File.WriteAllText(_userPath, "{ broken json");

        var warnings = new List<string>();
        var recoveredService = new SettingsService(_defaultPath, _userPath, warnings.Add);
        AppSettings recovered = recoveredService.Load();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recovered.Pricing.TokenPriceRubles, Is.EqualTo(10));
            Assert.That(recoveredService.LastLoadSource, Is.EqualTo(SettingsLoadSource.BackupRecovered));
            Assert.That(ReadSettings(_userPath).Pricing.TokenPriceRubles, Is.EqualTo(10));
            Assert.That(warnings, Has.Count.EqualTo(1));
            Assert.That(warnings[0], Does.Not.Contain(_directory));
        }
    }

    [Test]
    public void Load_CorruptUserAndBackupRecoverDefaults()
    {
        WriteSettings(_defaultPath, CreateDefaults(masterVolume: 55));
        Directory.CreateDirectory(Path.GetDirectoryName(_userPath)!);
        File.WriteAllText(_userPath, "not json");
        File.WriteAllText(_userPath + SettingsService.BackupSuffix, "also not json");
        var service = new SettingsService(_defaultPath, _userPath);

        AppSettings recovered = service.Load();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recovered.Audio.MasterVolumePercent, Is.EqualTo(55));
            Assert.That(service.LastLoadSource, Is.EqualTo(SettingsLoadSource.DefaultsRecovered));
            Assert.That(ReadSettings(_userPath).Audio.MasterVolumePercent, Is.EqualTo(55));
        }
    }

    [Test]
    public void Load_VersionSevenSettingsMigratesAudioAndCreatesBackup()
    {
        WriteSettings(_defaultPath, CreateDefaults(masterVolume: 100));
        var legacy = CreateDefaults(masterVolume: 100);
        legacy.ConfigurationVersion = 7;
        legacy.Audio = null!;
        WriteSettings(_userPath, legacy);
        var service = new SettingsService(_defaultPath, _userPath);

        AppSettings migrated = service.Load();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(migrated.ConfigurationVersion, Is.EqualTo(AppSettings.CurrentConfigurationVersion));
            Assert.That(migrated.Audio, Is.Not.Null);
            Assert.That(migrated.Audio.MasterVolumePercent, Is.EqualTo(100));
            Assert.That(service.LastLoadSource, Is.EqualTo(SettingsLoadSource.UserMigrated));
            Assert.That(ReadSettings(_userPath + SettingsService.BackupSuffix).ConfigurationVersion, Is.EqualTo(7));
        }
    }

    [Test]
    public void Load_VersionEightSettingsAddsEnabledPaymentMethods()
    {
        WriteSettings(_defaultPath, CreateDefaults(masterVolume: 100));
        var legacy = CreateDefaults(masterVolume: 100);
        legacy.ConfigurationVersion = 8;
        legacy.Payments = null!;
        WriteSettings(_userPath, legacy);
        var service = new SettingsService(_defaultPath, _userPath);

        AppSettings migrated = service.Load();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(migrated.ConfigurationVersion, Is.EqualTo(AppSettings.CurrentConfigurationVersion));
            Assert.That(migrated.Payments.CashEnabled, Is.True);
            Assert.That(migrated.Payments.CardEnabled, Is.True);
            Assert.That(ReadSettings(_userPath + SettingsService.BackupSuffix).ConfigurationVersion, Is.EqualTo(8));
        }
    }

    [Test]
    public void Load_VersionNineSettingsAddsConfirmedBoardInputMapping()
    {
        WriteSettings(_defaultPath, CreateDefaults(masterVolume: 100));
        var legacy = CreateDefaults(masterVolume: 100);
        legacy.ConfigurationVersion = 9;
        legacy.Hardware.StockSensorInputIndex = -1;
        legacy.Hardware.StockLowWhenInputHigh = false;
        WriteSettings(_userPath, legacy);
        var service = new SettingsService(_defaultPath, _userPath);

        AppSettings migrated = service.Load();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(migrated.ConfigurationVersion, Is.EqualTo(AppSettings.CurrentConfigurationVersion));
            Assert.That(migrated.Hardware.ServiceButtonInputIndex, Is.EqualTo(8));
            Assert.That(migrated.Hardware.StockSensorInputIndex, Is.EqualTo(1));
            Assert.That(migrated.Hardware.Hopper2StockSensorInputIndex, Is.EqualTo(4));
            Assert.That(migrated.Hardware.StockLowWhenInputHigh, Is.False);
            Assert.That(migrated.Hardware.Hopper1DispenseSensorInputIndex, Is.EqualTo(24));
            Assert.That(migrated.Hardware.Hopper1DispenseSensorActiveHigh, Is.False);
            Assert.That(migrated.Hardware.Hopper2DispenseSensorInputIndex, Is.EqualTo(25));
            Assert.That(migrated.Hardware.Hopper2DispenseSensorActiveHigh, Is.False);
            Assert.That(ReadSettings(_userPath + SettingsService.BackupSuffix).ConfigurationVersion, Is.EqualTo(9));
        }
    }

    [Test]
    public void Load_VersionElevenSettingsRemovesLegacySubtitleFromStoredJson()
    {
        WriteSettings(_defaultPath, CreateDefaults(masterVolume: 100));
        var legacy = CreateDefaults(masterVolume: 100);
        legacy.ConfigurationVersion = 11;
        string json = JsonSerializer.Serialize(
            legacy,
            new JsonSerializerOptions { WriteIndented = true });
        json = json.Replace(
            "\"Branding\": {",
            "\"Branding\": {\n    \"Subtitle\": \"Устаревшее описание\",",
            StringComparison.Ordinal);
        Directory.CreateDirectory(Path.GetDirectoryName(_userPath)!);
        File.WriteAllText(_userPath, json);
        var service = new SettingsService(_defaultPath, _userPath);

        AppSettings migrated = service.Load();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(migrated.ConfigurationVersion, Is.EqualTo(AppSettings.CurrentConfigurationVersion));
            Assert.That(service.LastLoadSource, Is.EqualTo(SettingsLoadSource.UserMigrated));
            Assert.That(File.ReadAllText(_userPath), Does.Not.Contain("\"Subtitle\""));
            Assert.That(File.ReadAllText(_userPath + SettingsService.BackupSuffix), Does.Contain("\"Subtitle\""));
        }
    }

    [Test]
    public void Load_VersionTwelveSettingsCorrectsHopperStockSensorToActiveLow()
    {
        WriteSettings(_defaultPath, CreateDefaults(masterVolume: 100));
        var legacy = CreateDefaults(masterVolume: 100);
        legacy.ConfigurationVersion = 12;
        legacy.Hardware.StockSensorInputIndex = PeripheryControllerInputs.Hopper2StockLow;
        legacy.Hardware.StockLowWhenInputHigh = true;
        WriteSettings(_userPath, legacy);
        var service = new SettingsService(_defaultPath, _userPath);

        AppSettings migrated = service.Load();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(migrated.ConfigurationVersion, Is.EqualTo(AppSettings.CurrentConfigurationVersion));
            Assert.That(migrated.Hardware.StockLowWhenInputHigh, Is.False);
            Assert.That(service.LastLoadSource, Is.EqualTo(SettingsLoadSource.UserMigrated));
            Assert.That(
                ReadSettings(_userPath + SettingsService.BackupSuffix).Hardware.StockLowWhenInputHigh,
                Is.True);
        }
    }

    [Test]
    public void Load_VersionThirteenSettingsAddsShortTextAndPersistsIt()
    {
        WriteSettings(_defaultPath, CreateDefaults(masterVolume: 100));
        var legacy = CreateDefaults(masterVolume: 100);
        legacy.ConfigurationVersion = 13;
        JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(legacy))!.AsObject();
        json["Branding"]!.AsObject().Remove("ShortText");
        Directory.CreateDirectory(Path.GetDirectoryName(_userPath)!);
        File.WriteAllText(
            _userPath,
            json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        var service = new SettingsService(_defaultPath, _userPath);

        AppSettings migrated = service.Load();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(migrated.ConfigurationVersion, Is.EqualTo(AppSettings.CurrentConfigurationVersion));
            Assert.That(migrated.Branding.ShortText, Is.EqualTo("ПОЛУЧИ СВОИ ЖЕТОНЫ!"));
            Assert.That(File.ReadAllText(_userPath), Does.Contain("\"ShortText\""));
            Assert.That(File.ReadAllText(_userPath + SettingsService.BackupSuffix), Does.Not.Contain("\"ShortText\""));
        }
    }

    [Test]
    public void Load_VersionFourteenTargetWiringMigratesToCurrentDualHopperMode()
    {
        WriteSettings(_defaultPath, CreateDefaults(masterVolume: 100));
        var legacy = CreateDefaults(masterVolume: 100);
        legacy.ConfigurationVersion = 14;
        legacy.Hardware.DispenseMode = "Hopper1Auto";
        // Значения старой v14-разводки нельзя брать из актуальных констант:
        // тогда это были IN1/IN25, хотя сейчас константы уже описывают новую плату.
        legacy.Hardware.StockSensorInputIndex = 1;
        legacy.Hardware.Hopper1DispenseSensorInputIndex = 25;
        WriteSettings(_userPath, legacy);
        var service = new SettingsService(_defaultPath, _userPath);

        AppSettings migrated = service.Load();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(migrated.ConfigurationVersion, Is.EqualTo(AppSettings.CurrentConfigurationVersion));
            Assert.That(migrated.Hardware.DispenseMode, Is.EqualTo("DualHopperAuto"));
            Assert.That(migrated.Hardware.ServiceButtonInputIndex, Is.EqualTo(8));
            Assert.That(migrated.Hardware.StockSensorInputIndex, Is.EqualTo(1));
            Assert.That(migrated.Hardware.Hopper2StockSensorInputIndex, Is.EqualTo(4));
            Assert.That(migrated.Hardware.Hopper1DispenseSensorInputIndex, Is.EqualTo(24));
            Assert.That(migrated.Hardware.Hopper2DispenseSensorInputIndex, Is.EqualTo(25));
            Assert.That(service.LastLoadSource, Is.EqualTo(SettingsLoadSource.UserMigrated));
        }
    }

    [Test]
    public void Load_VersionSixteenHopper2ProfileMigratesToDualModeAndConfirmedPulseInput()
    {
        WriteSettings(_defaultPath, CreateDefaults(masterVolume: 100));
        var legacy = CreateDefaults(masterVolume: 100);
        legacy.ConfigurationVersion = 16;
        legacy.Hardware.DispenseMode = "Hopper2Auto";
        legacy.Hardware.ServiceButtonInputIndex = 0;
        legacy.Hardware.StockSensorInputIndex = 1;
        legacy.Hardware.Hopper1DispenseSensorInputIndex = 24;
        legacy.Hardware.Hopper2DispenseSensorInputIndex = 25;
        WriteSettings(_userPath, legacy);
        var service = new SettingsService(_defaultPath, _userPath);

        AppSettings migrated = service.Load();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(migrated.ConfigurationVersion, Is.EqualTo(AppSettings.CurrentConfigurationVersion));
            Assert.That(migrated.Hardware.DispenseMode, Is.EqualTo("DualHopperAuto"));
            Assert.That(migrated.Hardware.ServiceButtonInputIndex, Is.EqualTo(8));
            Assert.That(migrated.Hardware.StockSensorInputIndex, Is.EqualTo(1));
            Assert.That(migrated.Hardware.Hopper2StockSensorInputIndex, Is.EqualTo(4));
            Assert.That(migrated.Hardware.Hopper1DispenseSensorInputIndex, Is.EqualTo(24));
            Assert.That(migrated.Hardware.Hopper2DispenseSensorInputIndex, Is.EqualTo(25));
            Assert.That(service.LastLoadSource, Is.EqualTo(SettingsLoadSource.UserMigrated));
        }
    }

    [Test]
    public void Load_VersionSeventeenSingleHopperProfileMigratesToDualHopper()
    {
        WriteSettings(_defaultPath, CreateDefaults(masterVolume: 100));
        var legacy = CreateDefaults(masterVolume: 100);
        legacy.ConfigurationVersion = 17;
        legacy.Hardware.DispenseMode = "Hopper1Auto";
        legacy.Hardware.Hopper2StockSensorInputIndex = -1;
        WriteSettings(_userPath, legacy);
        var service = new SettingsService(_defaultPath, _userPath);

        AppSettings migrated = service.Load();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(migrated.ConfigurationVersion, Is.EqualTo(AppSettings.CurrentConfigurationVersion));
            Assert.That(migrated.Hardware.DispenseMode, Is.EqualTo("DualHopperAuto"));
            Assert.That(migrated.Hardware.StockSensorInputIndex, Is.EqualTo(1));
            Assert.That(migrated.Hardware.Hopper2StockSensorInputIndex, Is.EqualTo(4));
            Assert.That(migrated.Hardware.StockLowWhenInputHigh, Is.False);
            Assert.That(migrated.Hardware.Hopper2StockLowWhenInputHigh, Is.False);
        }
    }

    [TestCase(5000, 2000)]
    [TestCase(7000, 7000)]
    public void Load_VersionEighteenHandshakeTimeoutMigratesOnlyPreviousDefault(
        int storedTimeout,
        int expectedTimeout)
    {
        WriteSettings(_defaultPath, CreateDefaults(masterVolume: 100));
        var legacy = CreateDefaults(masterVolume: 100);
        legacy.ConfigurationVersion = 18;
        legacy.Hardware.HandshakeTimeoutMilliseconds = storedTimeout;
        WriteSettings(_userPath, legacy);
        var service = new SettingsService(_defaultPath, _userPath);

        AppSettings migrated = service.Load();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(migrated.ConfigurationVersion, Is.EqualTo(AppSettings.CurrentConfigurationVersion));
            Assert.That(migrated.Hardware.HandshakeTimeoutMilliseconds, Is.EqualTo(expectedTimeout));
            Assert.That(service.LastLoadSource, Is.EqualTo(SettingsLoadSource.UserMigrated));
        }
    }

    [Test]
    public void Load_VersionNineteenSplitsLegacyPaymentTimeoutsWithoutLosingValues()
    {
        WriteSettings(_defaultPath, CreateDefaults(masterVolume: 100));
        var legacy = CreateDefaults(masterVolume: 100);
        legacy.ConfigurationVersion = 19;
        JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(legacy))!.AsObject();
        JsonObject timeouts = json["Timeouts"]!.AsObject();
        timeouts.Remove("CashPaymentSeconds");
        timeouts.Remove("CashlessPaymentSeconds");
        timeouts["SessionIdleSeconds"] = 73;
        timeouts["PaymentSeconds"] = 215;
        Directory.CreateDirectory(Path.GetDirectoryName(_userPath)!);
        File.WriteAllText(_userPath, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        var service = new SettingsService(_defaultPath, _userPath);

        AppSettings migrated = service.Load();
        string persisted = File.ReadAllText(_userPath);
        JsonObject persistedTimeouts = JsonNode.Parse(persisted)!["Timeouts"]!.AsObject();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(migrated.ConfigurationVersion, Is.EqualTo(AppSettings.CurrentConfigurationVersion));
            Assert.That(migrated.Timeouts.CashPaymentSeconds, Is.EqualTo(73));
            Assert.That(migrated.Timeouts.CashlessPaymentSeconds, Is.EqualTo(215));
            Assert.That(migrated.Timeouts.LegacyPaymentSeconds, Is.Zero);
            Assert.That(persisted, Does.Contain("\"CashPaymentSeconds\""));
            Assert.That(persisted, Does.Contain("\"CashlessPaymentSeconds\""));
            Assert.That(persistedTimeouts.ContainsKey("PaymentSeconds"), Is.False);
            Assert.That(service.LastLoadSource, Is.EqualTo(SettingsLoadSource.UserMigrated));
        }
    }

    [Test]
    public void Load_VersionTwentyAddsVisibleMenuElementsAndPersistsSection()
    {
        WriteSettings(_defaultPath, CreateDefaults(masterVolume: 100));
        var legacy = CreateDefaults(masterVolume: 100);
        legacy.ConfigurationVersion = 20;
        JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(legacy))!.AsObject();
        json.Remove("MenuVisibility");
        Directory.CreateDirectory(Path.GetDirectoryName(_userPath)!);
        File.WriteAllText(_userPath, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        var service = new SettingsService(_defaultPath, _userPath);

        AppSettings migrated = service.Load();
        string persisted = File.ReadAllText(_userPath);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(migrated.ConfigurationVersion, Is.EqualTo(AppSettings.CurrentConfigurationVersion));
            Assert.That(migrated.MenuVisibility.ShowStockStatus, Is.True);
            Assert.That(migrated.MenuVisibility.ShowCustomText, Is.True);
            Assert.That(migrated.MenuVisibility.ShowPromotionBlock, Is.True);
            Assert.That(persisted, Does.Contain("\"MenuVisibility\""));
            Assert.That(service.LastLoadSource, Is.EqualTo(SettingsLoadSource.UserMigrated));
        }
    }

    [Test]
    public void Load_VersionTwentyOneAddsEnabledTokenInventoryAndPersistsSection()
    {
        WriteSettings(_defaultPath, CreateDefaults(masterVolume: 100));
        var legacy = CreateDefaults(masterVolume: 100);
        legacy.ConfigurationVersion = 21;
        JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(legacy))!.AsObject();
        json.Remove("TokenInventory");
        Directory.CreateDirectory(Path.GetDirectoryName(_userPath)!);
        File.WriteAllText(_userPath, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        var service = new SettingsService(_defaultPath, _userPath);

        AppSettings migrated = service.Load();
        string persisted = File.ReadAllText(_userPath);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(migrated.ConfigurationVersion, Is.EqualTo(AppSettings.CurrentConfigurationVersion));
            Assert.That(migrated.TokenInventory.Enabled, Is.True);
            Assert.That(migrated.TokenInventory.PurchaseLimitEnabled, Is.True);
            Assert.That(persisted, Does.Contain("\"TokenInventory\""));
            Assert.That(service.LastLoadSource, Is.EqualTo(SettingsLoadSource.UserMigrated));
        }
    }

    [Test]
    public void Load_VersionTwentyTwoAddsEnabledPurchaseLimitAndPersistsProperty()
    {
        WriteSettings(_defaultPath, CreateDefaults(masterVolume: 100));
        var legacy = CreateDefaults(masterVolume: 100);
        legacy.ConfigurationVersion = 22;
        JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(legacy))!.AsObject();
        json["TokenInventory"]!.AsObject().Remove("PurchaseLimitEnabled");
        Directory.CreateDirectory(Path.GetDirectoryName(_userPath)!);
        File.WriteAllText(_userPath, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        var service = new SettingsService(_defaultPath, _userPath);

        AppSettings migrated = service.Load();
        string persisted = File.ReadAllText(_userPath);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(migrated.ConfigurationVersion, Is.EqualTo(AppSettings.CurrentConfigurationVersion));
            Assert.That(migrated.TokenInventory.Enabled, Is.True);
            Assert.That(migrated.TokenInventory.PurchaseLimitEnabled, Is.True);
            Assert.That(persisted, Does.Contain("\"PurchaseLimitEnabled\""));
            Assert.That(service.LastLoadSource, Is.EqualTo(SettingsLoadSource.UserMigrated));
        }
    }

    [Test]
    public void Load_FutureUserVersionRecoversCompatibleBackup()
    {
        WriteSettings(_defaultPath, CreateDefaults(masterVolume: 100));
        var firstService = new SettingsService(_defaultPath, _userPath);
        firstService.Load();
        AppSettings changed = firstService.CreateWorkingCopy();
        changed.Pricing.TokenPriceRubles = 20;
        firstService.Save(changed);

        AppSettings future = ReadSettings(_userPath);
        future.ConfigurationVersion = AppSettings.CurrentConfigurationVersion + 1;
        future.Pricing.TokenPriceRubles = 999;
        WriteSettings(_userPath, future);
        var warnings = new List<string>();
        var recoveredService = new SettingsService(_defaultPath, _userPath, warnings.Add);

        AppSettings recovered = recoveredService.Load();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recovered.ConfigurationVersion, Is.EqualTo(AppSettings.CurrentConfigurationVersion));
            Assert.That(recovered.Pricing.TokenPriceRubles, Is.EqualTo(10));
            Assert.That(recoveredService.LastLoadSource, Is.EqualTo(SettingsLoadSource.BackupRecovered));
            Assert.That(warnings, Has.Some.Contains("новее поддерживаемой"));
            Assert.That(ReadSettings(_userPath).ConfigurationVersion, Is.EqualTo(AppSettings.CurrentConfigurationVersion + 1));
            Assert.That(ReadSettings(_userPath).Pricing.TokenPriceRubles, Is.EqualTo(999));
        }
    }

    [Test]
    public void WorkingCopy_DoesNotMutateActiveSnapshotUntilSave()
    {
        WriteSettings(_defaultPath, CreateDefaults(masterVolume: 70));
        var service = new SettingsService(_defaultPath, _userPath);
        service.Load();

        AppSettings workingCopy = service.CreateWorkingCopy();
        workingCopy.Audio.MasterVolumePercent = 5;
        workingCopy.Pricing.BonusRules = [new BonusRule { MinimumAmountRubles = 100, BonusTokens = 50 }];
        AppSettings exposedSnapshot = service.Current;
        exposedSnapshot.Audio.MasterVolumePercent = 1;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(service.Current.Audio.MasterVolumePercent, Is.EqualTo(70));
            Assert.That(service.Current.Pricing.BonusRules, Is.Empty);
        }
    }

    [Test]
    public void Load_WhenUserFileCannotBeCreatedStillReturnsDefaults()
    {
        WriteSettings(_defaultPath, CreateDefaults(masterVolume: 65));
        var warnings = new List<string>();
        var service = new SettingsService(_defaultPath, _directory, warnings.Add);

        AppSettings loaded = service.Load();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(loaded.Audio.MasterVolumePercent, Is.EqualTo(65));
            Assert.That(service.LastLoadSource, Is.EqualTo(SettingsLoadSource.DefaultsCreated));
            Assert.That(warnings, Has.Some.Contains("Не удалось сохранить пользовательские настройки"));
            Assert.That(warnings, Has.All.Not.Contains(_directory));
            Assert.That(File.Exists(_directory + SettingsService.TemporarySuffix), Is.False);
        }
    }

    [Test]
    public void Load_RemovesStaleTemporaryFileAndKeepsCommittedUserSettings()
    {
        WriteSettings(_defaultPath, CreateDefaults(masterVolume: 40));
        var firstService = new SettingsService(_defaultPath, _userPath);
        firstService.Load();
        AppSettings committed = firstService.CreateWorkingCopy();
        committed.Audio.MasterVolumePercent = 70;
        firstService.Save(committed);

        AppSettings uncommitted = firstService.CreateWorkingCopy();
        uncommitted.Audio.MasterVolumePercent = 5;
        WriteSettings(_userPath + SettingsService.TemporarySuffix, uncommitted);

        var recoveredService = new SettingsService(_defaultPath, _userPath);
        AppSettings loaded = recoveredService.Load();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(loaded.Audio.MasterVolumePercent, Is.EqualTo(70));
            Assert.That(File.Exists(_userPath + SettingsService.TemporarySuffix), Is.False);
        }
    }

    [Test]
    public void CreateDefaultWorkingCopy_DoesNotMutateCurrentSnapshot()
    {
        WriteSettings(_defaultPath, CreateDefaults(masterVolume: 40));
        var service = new SettingsService(_defaultPath, _userPath);
        service.Load();
        AppSettings changed = service.CreateWorkingCopy();
        changed.Audio.MasterVolumePercent = 75;
        service.Save(changed);

        AppSettings defaults = service.CreateDefaultWorkingCopy();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(defaults.Audio.MasterVolumePercent, Is.EqualTo(40));
            Assert.That(service.Current.Audio.MasterVolumePercent, Is.EqualTo(75));
        }
    }

    private static AppSettings CreateDefaults(int masterVolume)
    {
        var settings = new AppSettings
        {
            ConfigurationVersion = AppSettings.CurrentConfigurationVersion,
            Audio = new AudioSettings
            {
                MasterVolumePercent = masterVolume,
                AdvertisementVolumePercent = 80,
            },
        };
        settings.Normalize();
        return settings;
    }

    private static void WriteSettings(string path, AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static AppSettings ReadSettings(string path)
    {
        return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidDataException("Настройки не десериализованы.");
    }
}
