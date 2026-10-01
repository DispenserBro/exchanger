using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Exchanger.Hardware.PeripheryController;

namespace Exchanger.Core.Configuration;

public enum SettingsLoadSource
{
    User,
    UserMigrated,
    BackupRecovered,
    DefaultsCreated,
    DefaultsRecovered,
}

/// <summary>
/// Владеет активным снимком пользовательской конфигурации и сохраняет его
/// атомарно. Godot-пути преобразуются снаружи, поэтому класс тестируется без
/// запуска движка.
/// </summary>
public sealed class SettingsService
{
    public const string BackupSuffix = ".bak";
    public const string TemporarySuffix = ".tmp";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    private readonly string _defaultSettingsPath;
    private readonly string _userSettingsPath;
    private readonly string _backupSettingsPath;
    private readonly string _temporarySettingsPath;
    private readonly Action<string>? _warningSink;
    private AppSettings _current = new();

    public SettingsService(
        string defaultSettingsPath,
        string userSettingsPath,
        Action<string>? warningSink = null)
    {
        if (string.IsNullOrWhiteSpace(defaultSettingsPath))
        {
            throw new ArgumentException("Не указан путь настроек по умолчанию.", nameof(defaultSettingsPath));
        }

        if (string.IsNullOrWhiteSpace(userSettingsPath))
        {
            throw new ArgumentException("Не указан путь пользовательских настроек.", nameof(userSettingsPath));
        }

        _defaultSettingsPath = Path.GetFullPath(defaultSettingsPath);
        _userSettingsPath = Path.GetFullPath(userSettingsPath);
        _backupSettingsPath = _userSettingsPath + BackupSuffix;
        _temporarySettingsPath = _userSettingsPath + TemporarySuffix;
        _warningSink = warningSink;
    }

    public AppSettings Current => Clone(_current);

    public SettingsLoadSource LastLoadSource { get; private set; }

    public AppSettings Load()
    {
        DeleteStaleTemporaryFile();
        AppSettings defaults = TryReadSupported(
                                   _defaultSettingsPath,
                                   "встроенные настройки",
                                   out _) ?? new AppSettings();
        AppSettingsMigration.Migrate(defaults);
        defaults.Normalize();

        if (!File.Exists(_userSettingsPath))
        {
            _current = Clone(defaults);
            TryPersistDuringLoad(_current, createBackup: false);
            LastLoadSource = SettingsLoadSource.DefaultsCreated;
            return Clone(_current);
        }

        AppSettings? loaded = TryReadSupported(
            _userSettingsPath,
            "пользовательские настройки",
            out bool unsupportedUserVersion);
        bool recovered = loaded is null;
        if (loaded is null)
        {
            loaded = TryReadSupported(
                _backupSettingsPath,
                "резервную копию настроек",
                out _);
            LastLoadSource = loaded is null
                ? SettingsLoadSource.DefaultsRecovered
                : SettingsLoadSource.BackupRecovered;
            loaded ??= Clone(defaults);
        }
        else
        {
            LastLoadSource = SettingsLoadSource.User;
        }

        bool migrated = AppSettingsMigration.Migrate(loaded);
        loaded.Normalize();
        _current = Clone(loaded);

        if (recovered && !unsupportedUserVersion)
        {
            // Не перезаписываем последнюю корректную .bak повреждённым user-файлом.
            TryPersistDuringLoad(_current, createBackup: false);
        }
        else if (migrated)
        {
            TryPersistDuringLoad(_current, createBackup: true);
            LastLoadSource = SettingsLoadSource.UserMigrated;
        }

        return Clone(_current);
    }

    public AppSettings CreateWorkingCopy() => Clone(_current);

    public AppSettings CreateDefaultWorkingCopy()
    {
        AppSettings defaults = TryReadSupported(
                                   _defaultSettingsPath,
                                   "встроенные настройки",
                                   out _) ?? new AppSettings();
        AppSettingsMigration.Migrate(defaults);
        defaults.Normalize();
        return Clone(defaults);
    }

    public AppSettings Save(AppSettings candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        AppSettings normalized = Clone(candidate);
        AppSettingsMigration.Migrate(normalized);
        normalized.Normalize();
        WriteAtomic(normalized, createBackup: true);
        _current = Clone(normalized);
        return Clone(_current);
    }

    private AppSettings? TryRead(string path, string description)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or JsonException
                                          or NotSupportedException)
        {
            _warningSink?.Invoke($"Не удалось прочитать {description}: {exception.GetType().Name}.");
            return null;
        }
    }

    private AppSettings? TryReadSupported(string path, string description, out bool unsupportedVersion)
    {
        unsupportedVersion = false;
        AppSettings? settings = TryRead(path, description);
        if (settings is null || settings.ConfigurationVersion <= AppSettings.CurrentConfigurationVersion)
        {
            return settings;
        }

        unsupportedVersion = true;
        _warningSink?.Invoke(
            $"Не удалось применить {description}: версия {settings.ConfigurationVersion} новее поддерживаемой.");
        return null;
    }

    private void TryPersistDuringLoad(AppSettings settings, bool createBackup)
    {
        try
        {
            WriteAtomic(settings, createBackup);
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or InvalidDataException
                                          or NotSupportedException)
        {
            _warningSink?.Invoke($"Не удалось сохранить пользовательские настройки: {exception.GetType().Name}.");
        }
    }

    private void WriteAtomic(AppSettings settings, bool createBackup)
    {
        string? directory = Path.GetDirectoryName(_userSettingsPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            byte[] json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(settings, JsonOptions));
            using (var stream = new FileStream(
                       _temporarySettingsPath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(json);
                stream.Flush(flushToDisk: true);
            }

            if (TryRead(_temporarySettingsPath, "временный файл настроек") is null)
            {
                throw new InvalidDataException("Временный файл настроек не прошёл проверку.");
            }

            if (!File.Exists(_userSettingsPath))
            {
                File.Move(_temporarySettingsPath, _userSettingsPath);
                return;
            }

            if (!createBackup)
            {
                File.Move(_temporarySettingsPath, _userSettingsPath, overwrite: true);
                return;
            }

            try
            {
                File.Replace(
                    _temporarySettingsPath,
                    _userSettingsPath,
                    _backupSettingsPath,
                    ignoreMetadataErrors: true);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceWithPortableFallback();
            }
            catch (IOException)
            {
                ReplaceWithPortableFallback();
            }
        }
        finally
        {
            if (File.Exists(_temporarySettingsPath))
            {
                try
                {
                    File.Delete(_temporarySettingsPath);
                }
                catch (IOException)
                {
                    _warningSink?.Invoke("Не удалось удалить временный файл настроек: IOException.");
                }
                catch (UnauthorizedAccessException)
                {
                    _warningSink?.Invoke("Не удалось удалить временный файл настроек: UnauthorizedAccessException.");
                }
            }
        }
    }

    private void ReplaceWithPortableFallback()
    {
        File.Copy(_userSettingsPath, _backupSettingsPath, overwrite: true);
        File.Move(_temporarySettingsPath, _userSettingsPath, overwrite: true);
    }

    private void DeleteStaleTemporaryFile()
    {
        if (!File.Exists(_temporarySettingsPath))
        {
            return;
        }

        try
        {
            File.Delete(_temporarySettingsPath);
        }
        catch (IOException)
        {
            _warningSink?.Invoke("Не удалось удалить незавершённый временный файл настроек: IOException.");
        }
        catch (UnauthorizedAccessException)
        {
            _warningSink?.Invoke("Не удалось удалить незавершённый временный файл настроек: UnauthorizedAccessException.");
        }
    }

    private static AppSettings Clone(AppSettings settings)
    {
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
    }
}

public static class AppSettingsMigration
{
    public static bool Migrate(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.Window ??= new WindowSettings();
        settings.Themes ??= new ThemeSettings();
        settings.Hardware ??= new HardwareSettings();
        settings.Audio ??= new AudioSettings();
        settings.Payments ??= new PaymentSettings();
        settings.AnimatedBannerTexts ??= new AnimatedBannerTextSettings();
        settings.Security ??= new SecuritySettings();
        settings.Timeouts ??= new TimeoutSettings();
        settings.MenuVisibility ??= new MenuVisibilitySettings();
        settings.TokenInventory ??= new TokenInventorySettings();

        if (settings.ConfigurationVersion > AppSettings.CurrentConfigurationVersion)
        {
            throw new NotSupportedException(
                $"Версия настроек {settings.ConfigurationVersion} новее поддерживаемой "
                + AppSettings.CurrentConfigurationVersion + ".");
        }

        if (settings.ConfigurationVersion >= AppSettings.CurrentConfigurationVersion)
        {
            return false;
        }

        if (settings.ConfigurationVersion < 2
            && settings.Window.DebugWindowWidth == 1280
            && settings.Window.DebugWindowHeight == 720)
        {
            settings.Window.DebugWindowWidth = 720;
            settings.Window.DebugWindowHeight = 1280;
        }

        if (settings.ConfigurationVersion < 3 && settings.Hardware.BaudRate == 9600)
        {
            settings.Hardware.BaudRate = 115200;
        }

        if (settings.ConfigurationVersion < 6
            && string.IsNullOrWhiteSpace(settings.Hardware.ControllerAesMode))
        {
            settings.Hardware.ControllerAesMode = "ECB";
        }

        if (settings.ConfigurationVersion < 10
            && settings.Hardware.StockSensorInputIndex < 0)
        {
            settings.Hardware.StockSensorInputIndex = 1;
            settings.Hardware.StockLowWhenInputHigh = false;
        }

        if (settings.ConfigurationVersion < 13
            && settings.Hardware.StockSensorInputIndex == 1)
        {
            // Все цифровые входы платы имеют pull-up: активный сигнал сбрасывает бит.
            // Поэтому IN1=0 означает «мало жетонов», а IN1=1 — «много жетонов».
            settings.Hardware.StockLowWhenInputHigh = false;
        }

        if (settings.ConfigurationVersion < 15
            && settings.Hardware.DispenseMode.Equals("Hopper1Auto", StringComparison.OrdinalIgnoreCase)
            && settings.Hardware.StockSensorInputIndex == 1
            && settings.Hardware.Hopper1DispenseSensorInputIndex == 25)
        {
            // Подключённый автомат использует второй аппаратный хоппер: IN1/IN25 и команду 409.
            settings.Hardware.DispenseMode = "Hopper2Auto";
        }

        if (settings.ConfigurationVersion < 16)
        {
            if (!settings.Hardware.DispenseMode.Equals("Hopper1Auto", StringComparison.OrdinalIgnoreCase))
            {
                // До v16 единственное поле с именем Hopper1 фактически задавало датчик
                // активного целевого второго хоппера. Переносим его в явную пару IN25.
                if (settings.Hardware.Hopper1DispenseSensorInputIndex
                    != PeripheryControllerInputs.Hopper1DispensePulse)
                {
                    settings.Hardware.Hopper2DispenseSensorInputIndex =
                        settings.Hardware.Hopper1DispenseSensorInputIndex;
                    settings.Hardware.Hopper2DispenseSensorActiveHigh =
                        settings.Hardware.Hopper1DispenseSensorActiveHigh;
                }

                settings.Hardware.Hopper1DispenseSensorInputIndex =
                    PeripheryControllerInputs.Hopper1DispensePulse;
                settings.Hardware.Hopper1DispenseSensorActiveHigh = false;
            }
            else
            {
                settings.Hardware.Hopper2DispenseSensorInputIndex =
                    25;
                settings.Hardware.Hopper2DispenseSensorActiveHigh = false;
            }
        }

        if (settings.ConfigurationVersion < 17)
        {
            // Актуальная прошивка полностью меняет целевую разводку. До уточнения
            // второго датчика выдачи приложение использует только hopper 1 / 30X.
            settings.Hardware.ServiceButtonInputIndex = PeripheryControllerInputs.ServiceButton;
            settings.Hardware.ServiceButtonActiveHigh = false;
            settings.Hardware.StockSensorInputIndex = PeripheryControllerInputs.Hopper1StockLow;
            settings.Hardware.StockLowWhenInputHigh = false;
            settings.Hardware.Hopper1DispenseSensorInputIndex =
                PeripheryControllerInputs.Hopper1DispensePulse;
            settings.Hardware.Hopper1DispenseSensorActiveHigh = false;
            settings.Hardware.Hopper2DispenseSensorInputIndex =
                PeripheryControllerInputs.Hopper2DispensePulse;
            settings.Hardware.Hopper2DispenseSensorActiveHigh = false;
            if (settings.Hardware.DispenseMode.Equals("Hopper2Auto", StringComparison.OrdinalIgnoreCase))
            {
                settings.Hardware.DispenseMode = "Hopper1Auto";
            }
        }

        if (settings.ConfigurationVersion < 18)
        {
            // Оба хоппера подтверждены владельцем: IN1/30X и IN4/40X.
            settings.Hardware.ServiceButtonInputIndex = PeripheryControllerInputs.ServiceButton;
            settings.Hardware.ServiceButtonActiveHigh = false;
            settings.Hardware.StockSensorInputIndex = PeripheryControllerInputs.Hopper1StockLow;
            settings.Hardware.StockLowWhenInputHigh = false;
            settings.Hardware.Hopper2StockSensorInputIndex = PeripheryControllerInputs.Hopper2StockLow;
            settings.Hardware.Hopper2StockLowWhenInputHigh = false;
            if (settings.Hardware.DispenseMode.Equals("Hopper1Auto", StringComparison.OrdinalIgnoreCase)
                || settings.Hardware.DispenseMode.Equals("Hopper2Auto", StringComparison.OrdinalIgnoreCase))
            {
                settings.Hardware.DispenseMode = "DualHopperAuto";
            }
        }

        if (settings.ConfigurationVersion < 19
            && settings.Hardware.HandshakeTimeoutMilliseconds == 5000)
        {
            // Актуальный plate_config_app ждёт CHAL и RAES по 2 секунды.
            // Меняем только прежний стандартный default, не затирая явную
            // пользовательскую настройку другого таймаута.
            settings.Hardware.HandshakeTimeoutMilliseconds = 2000;
        }

        if (settings.ConfigurationVersion < 20)
        {
            // До v20 наличная ветка использовала SessionIdleSeconds, а поле
            // PaymentSeconds задавало ожидание безналичного терминала.
            settings.Timeouts.CashPaymentSeconds = settings.Timeouts.SessionIdleSeconds;
            if (settings.Timeouts.LegacyPaymentSeconds > 0)
            {
                settings.Timeouts.CashlessPaymentSeconds = settings.Timeouts.LegacyPaymentSeconds;
            }

            settings.Timeouts.LegacyPaymentSeconds = 0;
        }

        settings.ConfigurationVersion = AppSettings.CurrentConfigurationVersion;
        return true;
    }
}
