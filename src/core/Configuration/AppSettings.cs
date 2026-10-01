using System;
using System.Linq;
using System.Text.Json.Serialization;
using Exchanger.Hardware.PeripheryController;

namespace Exchanger.Core.Configuration;

/// <summary>
/// Типизированные настройки приложения. Значения могут переопределяться
/// пользователем без пересборки через user://config/appsettings.json.
/// </summary>
public sealed class AppSettings
{
    public const int CurrentConfigurationVersion = 25;

    public int ConfigurationVersion { get; set; }

    public WindowSettings Window { get; set; } = new();

    public ThemeSettings Themes { get; set; } = new();

    public HardwareSettings Hardware { get; set; } = new();

    public TimeoutSettings Timeouts { get; set; } = new();

    public PricingSettings Pricing { get; set; } = new();

    public PaymentSettings Payments { get; set; } = new();

    public BrandingSettings Branding { get; set; } = new();

    public AnimatedBannerTextSettings AnimatedBannerTexts { get; set; } = new();

    public AudioSettings Audio { get; set; } = new();

    public AdvertisementSettings Advertisement { get; set; } = new();

    public MenuVisibilitySettings MenuVisibility { get; set; } = new();

    public TokenInventorySettings TokenInventory { get; set; } = new();

    public LoggingSettings Logging { get; set; } = new();

    public SecuritySettings Security { get; set; } = new();

    public void Normalize()
    {
        if (ConfigurationVersion <= CurrentConfigurationVersion)
        {
            ConfigurationVersion = CurrentConfigurationVersion;
        }
        Window ??= new WindowSettings();
        Themes ??= new ThemeSettings();
        Hardware ??= new HardwareSettings();
        Timeouts ??= new TimeoutSettings();
        Pricing ??= new PricingSettings();
        Payments ??= new PaymentSettings();
        Branding ??= new BrandingSettings();
        AnimatedBannerTexts ??= new AnimatedBannerTextSettings();
        Audio ??= new AudioSettings();
        Advertisement ??= new AdvertisementSettings();
        MenuVisibility ??= new MenuVisibilitySettings();
        TokenInventory ??= new TokenInventorySettings();
        Logging ??= new LoggingSettings();
        Security ??= new SecuritySettings();

        Window.DebugWindowWidth = Math.Clamp(Window.DebugWindowWidth, 640, 3840);
        Window.DebugWindowHeight = Math.Clamp(Window.DebugWindowHeight, 360, 2160);
        Window.TargetFps = Math.Clamp(Window.TargetFps, 30, 240);

        Themes.ExternalDlcPath = string.IsNullOrWhiteSpace(Themes.ExternalDlcPath)
                                 || Themes.ExternalDlcPath.Trim().Length > ThemeSettingsValidator.MaximumDlcPathLength
            ? "user://theme_dlc/active_theme.pck"
            : Themes.ExternalDlcPath.Trim();

        Hardware.PortName = string.IsNullOrWhiteSpace(Hardware.PortName)
            ? "COM3"
            : Hardware.PortName.Trim();
        Hardware.BaudRate = Math.Clamp(Hardware.BaudRate, 300, 921600);
        Hardware.FrameTimeoutMilliseconds = Math.Clamp(Hardware.FrameTimeoutMilliseconds, 10, 5000);
        Hardware.MaximumFrameLength = Math.Clamp(Hardware.MaximumFrameLength, 2, 65536);
        Hardware.HandshakeTimeoutMilliseconds = Math.Clamp(Hardware.HandshakeTimeoutMilliseconds, 1000, 60000);
        Hardware.CashlessKeepAliveSeconds = Math.Clamp(Hardware.CashlessKeepAliveSeconds, 30, 170);
        Hardware.ReconnectIntervalSeconds = Math.Clamp(Hardware.ReconnectIntervalSeconds, 1, 60);
        Hardware.ServiceButtonInputIndex = Math.Clamp(Hardware.ServiceButtonInputIndex, -1, 25);
        Hardware.StockSensorInputIndex = Math.Clamp(Hardware.StockSensorInputIndex, -1, 25);
        Hardware.Hopper2StockSensorInputIndex = Math.Clamp(Hardware.Hopper2StockSensorInputIndex, -1, 25);
        Hardware.Hopper1DispenseSensorInputIndex = Math.Clamp(Hardware.Hopper1DispenseSensorInputIndex, -1, 25);
        Hardware.Hopper2DispenseSensorInputIndex = Math.Clamp(Hardware.Hopper2DispenseSensorInputIndex, -1, 25);
        Hardware.InputPollMilliseconds = Math.Clamp(Hardware.InputPollMilliseconds, 50, 1000);
        Hardware.StockPollSeconds = Math.Clamp(Hardware.StockPollSeconds, 1, 60);
        Hardware.ControllerAesMode = Hardware.ControllerAesMode?.Trim().ToUpperInvariant() ?? string.Empty;
        Hardware.ControllerAesKeyEnvironmentVariable = string.IsNullOrWhiteSpace(Hardware.ControllerAesKeyEnvironmentVariable)
            ? "EXCHANGER_CONTROLLER_AES_KEY"
            : Hardware.ControllerAesKeyEnvironmentVariable.Trim();
        Hardware.DispenseMode = string.IsNullOrWhiteSpace(Hardware.DispenseMode)
            ? "Unsupported"
            : Hardware.DispenseMode.Trim();
        Hardware.MockStockLevel = Hardware.MockStockLevel?.Trim() switch
        {
            "Low" => "Low",
            _ => "Enough",
        };

        Timeouts.SessionIdleSeconds = Math.Clamp(Timeouts.SessionIdleSeconds, 15, 600);
        Timeouts.CashPaymentSeconds = Math.Clamp(Timeouts.CashPaymentSeconds, 15, 600);
        Timeouts.CashlessPaymentSeconds = Math.Clamp(Timeouts.CashlessPaymentSeconds, 30, 600);
        Timeouts.SuccessSeconds = Math.Clamp(Timeouts.SuccessSeconds, 3, 60);

        Pricing.TokenPriceRubles = Math.Clamp(Pricing.TokenPriceRubles, 1, 100000);
        Pricing.MaxCardAmountRubles = Math.Clamp(Pricing.MaxCardAmountRubles, Pricing.TokenPriceRubles, 1000000);
        Pricing.CustomAmountStepRubles = Math.Clamp(Pricing.CustomAmountStepRubles, 1, Pricing.MaxCardAmountRubles);
        Pricing.PaymentPresets = (Pricing.PaymentPresets ?? Array.Empty<PaymentPreset>())
            .Where(preset => preset is not null)
            .Select(preset => new PaymentPreset
            {
                AmountRubles = Math.Clamp(preset.AmountRubles, Pricing.TokenPriceRubles, Pricing.MaxCardAmountRubles),
            })
            .GroupBy(preset => preset.AmountRubles)
            .Select(group => group.First())
            .OrderBy(preset => preset.AmountRubles)
            .ToArray();
        Pricing.BonusRules = (Pricing.BonusRules ?? Array.Empty<BonusRule>())
            .Where(rule => rule is not null)
            .Select(rule => new BonusRule
            {
                MinimumAmountRubles = Math.Clamp(rule.MinimumAmountRubles, Pricing.TokenPriceRubles, Pricing.MaxCardAmountRubles),
                BonusTokens = Math.Clamp(rule.BonusTokens, 0, 100000),
            })
            .OrderBy(rule => rule.MinimumAmountRubles)
            .ToArray();

        Branding.ApplicationName = NormalizeText(
            Branding.ApplicationName,
            "РАЗМЕН ЖЕТОНОВ",
            BrandingSettingsValidator.MaximumApplicationNameLength);
        Branding.ShortText = NormalizeText(
            Branding.ShortText,
            "ПОЛУЧИ СВОИ ЖЕТОНЫ!",
            BrandingSettingsValidator.MaximumShortTextLength);
        Branding.SupportPhone = NormalizeText(
            Branding.SupportPhone,
            "Телефон поддержки уточняется",
            BrandingSettingsValidator.MaximumSupportPhoneLength);

        AnimatedBannerTexts.CashPayment = NormalizeText(
            AnimatedBannerTexts.CashPayment,
            "АВТОМАТ НЕ ВЫДАЁТ СДАЧУ",
            AnimatedBannerTextSettings.MaximumTextLength);
        AnimatedBannerTexts.CardAmount = NormalizeText(
            AnimatedBannerTexts.CardAmount,
            "ВЫБЕРИТЕ СУММУ ДЛЯ ПЕРЕХОДА К ТЕРМИНАЛУ",
            AnimatedBannerTextSettings.MaximumTextLength);
        AnimatedBannerTexts.CardCustomAmount = NormalizeText(
            AnimatedBannerTexts.CardCustomAmount,
            "ВВЕДИТЕ СУММУ ДЛЯ ОПЛАТЫ",
            AnimatedBannerTextSettings.MaximumTextLength);
        AnimatedBannerTexts.CardTerminalCountdownTemplate = NormalizeText(
            AnimatedBannerTexts.CardTerminalCountdownTemplate,
            "ОСТАЛОСЬ {seconds} СЕК.",
            AnimatedBannerTextSettings.MaximumTextLength);

        Audio.MasterVolumePercent = Math.Clamp(Audio.MasterVolumePercent, 0, 100);
        Audio.AdvertisementVolumePercent = Math.Clamp(Audio.AdvertisementVolumePercent, 0, 100);
        Audio.MusicVolumePercent = Math.Clamp(Audio.MusicVolumePercent, 0, 100);
        Audio.SoundEffectsVolumePercent = Math.Clamp(Audio.SoundEffectsVolumePercent, 0, 100);

        if (string.Equals(
                Advertisement.FallbackText?.Trim(),
                "РЕКЛАМНЫЙ БЛОК",
                StringComparison.OrdinalIgnoreCase))
        {
            Advertisement.FallbackText = "ПРОМО-БЛОК";
        }

        Advertisement.FallbackText = NormalizeText(
            Advertisement.FallbackText,
            "ПРОМО-БЛОК",
            AdvertisementSettingsValidator.MaximumFallbackTextLength);
        Advertisement.PosterPath = NormalizeAssetPath(Advertisement.PosterPath);
        Advertisement.VideoPaths = (Advertisement.VideoPaths ?? Array.Empty<string>())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(AdvertisementVideoLibrary.NormalizeSelectedVideo)
            .Where(path => path.Length <= AdvertisementSettingsValidator.MaximumAssetPathLength)
            .GroupBy(AdvertisementVideoLibrary.GetComparisonKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(AdvertisementSettingsValidator.MaximumVideoPaths)
            .ToArray();

        Logging.MinimumLevel = LoggingSettingsValidator.NormalizeLevel(Logging.MinimumLevel);
        Logging.MaxFileSizeMb = Math.Clamp(Logging.MaxFileSizeMb, 1, 100);
        Logging.RetentionDays = Math.Clamp(Logging.RetentionDays, 1, 365);

        Security.ServicePinSaltBase64 = Security.ServicePinSaltBase64?.Trim() ?? string.Empty;
        Security.ServicePinHashBase64 = Security.ServicePinHashBase64?.Trim() ?? string.Empty;
        if (Security.ServicePinIterations is < ServiceAccessPolicy.MinimumPbkdf2Iterations
            or > ServiceAccessPolicy.MaximumPbkdf2Iterations)
        {
            Security.ServicePinSaltBase64 = string.Empty;
            Security.ServicePinHashBase64 = string.Empty;
            Security.ServicePinIterations = ServiceAccessPolicy.DefaultPbkdf2Iterations;
        }
    }

    private static string NormalizeText(string? value, string fallback, int maximumLength = int.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        string normalized = string.Join(
            " ",
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= maximumLength
            ? normalized
            : normalized[..maximumLength].TrimEnd();
    }

    private static string NormalizeAssetPath(string? value)
    {
        string path = value?.Trim() ?? string.Empty;
        return path.Length <= AdvertisementSettingsValidator.MaximumAssetPathLength
            ? AdvertisementVideoLibrary.NormalizeSelectedVideo(path)
            : string.Empty;
    }
}

public sealed class WindowSettings
{
    public bool Fullscreen { get; set; } = true;

    public bool Borderless { get; set; } = true;

    public bool DebugWindowInDebugBuild { get; set; } = true;

    public int DebugWindowWidth { get; set; } = 1280;

    public int DebugWindowHeight { get; set; } = 720;

    public int TargetFps { get; set; } = 60;
}

public sealed class ThemeSettings
{
    public bool ReducedEffects { get; set; }

    public bool ExternalDlcEnabled { get; set; } = true;

    /// <summary>
    /// Путь из пользовательского appsettings.json: user:// либо абсолютный
    /// путь к Godot resource pack с расширением .pck.
    /// </summary>
    public string ExternalDlcPath { get; set; } = "user://theme_dlc/active_theme.pck";
}

public sealed class HardwareSettings
{
    public bool UseMock { get; set; } = true;

    public string PortName { get; set; } = "COM3";

    public int BaudRate { get; set; } = 115200;

    public int FrameTimeoutMilliseconds { get; set; } = 100;

    public int MaximumFrameLength { get; set; } = 1024;

    public int HandshakeTimeoutMilliseconds { get; set; } = 2000;

    public int CashlessKeepAliveSeconds { get; set; } = 120;

    public int ReconnectIntervalSeconds { get; set; } = 30;

    /// <summary>IN8 по актуальной разводке; -1 отключает физический сервисный вход.</summary>
    public int ServiceButtonInputIndex { get; set; } = PeripheryControllerInputs.ServiceButton;

    /// <summary>Кнопочные входы платы имеют pull-up: нажатие переводит IN8 в ноль.</summary>
    public bool ServiceButtonActiveHigh { get; set; }

    /// <summary>Номер бита INPUT, 0..25; -1 — вход ещё не назначен.</summary>
    public int StockSensorInputIndex { get; set; } = PeripheryControllerInputs.Hopper1StockLow;

    public bool StockLowWhenInputHigh { get; set; }

    /// <summary>IN4 — датчик малого остатка второго хоппера; активный низкий уровень означает «мало».</summary>
    public int Hopper2StockSensorInputIndex { get; set; } = PeripheryControllerInputs.Hopper2StockLow;

    public bool Hopper2StockLowWhenInputHigh { get; set; }

    /// <summary>IN24 — датчик выдачи первого хоппера; -1 отключает его программный подсчёт.</summary>
    public int Hopper1DispenseSensorInputIndex { get; set; } = PeripheryControllerInputs.Hopper1DispensePulse;

    /// <summary>Датчик выдачи имеет pull-up: низкий уровень означает импульс жетона.</summary>
    public bool Hopper1DispenseSensorActiveHigh { get; set; }

    /// <summary>IN25 — физический датчик выдачи второго хоппера; -1 отключает его использование.</summary>
    public int Hopper2DispenseSensorInputIndex { get; set; } = PeripheryControllerInputs.Hopper2DispensePulse;

    /// <summary>Датчик выдачи имеет pull-up: низкий уровень означает импульс жетона.</summary>
    public bool Hopper2DispenseSensorActiveHigh { get; set; }

    /// <summary>Частота резервного опроса INPUT для кнопки и импульсного датчика.</summary>
    public int InputPollMilliseconds { get; set; } = 100;

    /// <summary>Интервал старого медленного опроса, если быстрые входы явно отключены.</summary>
    public int StockPollSeconds { get; set; } = 5;

    public string ControllerAesMode { get; set; } = "ECB";

    public string ControllerAesKeyEnvironmentVariable { get; set; } = "EXCHANGER_CONTROLLER_AES_KEY";

    public string DispenseMode { get; set; } = "Unsupported";

    public string MockStockLevel { get; set; } = "Enough";
}

public sealed class TimeoutSettings
{
    public int SessionIdleSeconds { get; set; } = 45;

    public int CashPaymentSeconds { get; set; } = 45;

    public int CashlessPaymentSeconds { get; set; } = 120;

    /// <summary>Поле конфигурации до v20; используется только при миграции.</summary>
    [JsonPropertyName("PaymentSeconds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int LegacyPaymentSeconds { get; set; }

    public int SuccessSeconds { get; set; } = 10;
}

public sealed class PricingSettings
{
    public int TokenPriceRubles { get; set; } = 10;

    public int MaxCardAmountRubles { get; set; } = 5000;

    public int CustomAmountStepRubles { get; set; } = 10;

    public PaymentPreset[] PaymentPresets { get; set; } =
        new PaymentPreset[]
    {
        new() { AmountRubles = 10 },
        new() { AmountRubles = 50 },
        new() { AmountRubles = 100 },
        new() { AmountRubles = 150 },
        new() { AmountRubles = 200 },
        new() { AmountRubles = 300 },
        new() { AmountRubles = 400 },
        new() { AmountRubles = 500 },
    };

    public BonusRule[] BonusRules { get; set; } = Array.Empty<BonusRule>();
}

public sealed class PaymentSettings
{
    public bool CashEnabled { get; set; } = true;

    public bool CardEnabled { get; set; } = true;
}

public sealed class PaymentPreset
{
    public int AmountRubles { get; set; }
}

public sealed class BonusRule
{
    public int MinimumAmountRubles { get; set; }

    public int BonusTokens { get; set; }
}

public sealed class BrandingSettings
{
    public string ApplicationName { get; set; } = "РАЗМЕН ЖЕТОНОВ";

    public string ShortText { get; set; } = "ПОЛУЧИ СВОИ ЖЕТОНЫ!";

    public string SupportPhone { get; set; } = "Телефон поддержки уточняется";
}

/// <summary>Тексты, накладываемые на нижние анимированные баннеры.</summary>
public sealed class AnimatedBannerTextSettings
{
    public const int MaximumTextLength = 120;

    public string CashPayment { get; set; } = "АВТОМАТ НЕ ВЫДАЁТ СДАЧУ";

    public string CardAmount { get; set; } = "ВЫБЕРИТЕ СУММУ ДЛЯ ПЕРЕХОДА К ТЕРМИНАЛУ";

    public string CardCustomAmount { get; set; } = "ВВЕДИТЕ СУММУ ДЛЯ ОПЛАТЫ";

    /// <summary>Шаблон ожидания терминала; <c>{seconds}</c> заменяется числом секунд.</summary>
    public string CardTerminalCountdownTemplate { get; set; } = "ОСТАЛОСЬ {seconds} СЕК.";
}

public sealed class AudioSettings
{
    public int MasterVolumePercent { get; set; } = 100;

    public bool Muted { get; set; }

    public int AdvertisementVolumePercent { get; set; } = 100;

    public int MusicVolumePercent { get; set; } = 100;

    public int SoundEffectsVolumePercent { get; set; } = 100;
}

public sealed class AdvertisementSettings
{
    public bool Enabled { get; set; } = true;

    public string[] VideoPaths { get; set; } = Array.Empty<string>();

    public string PosterPath { get; set; } = string.Empty;

    public bool Muted { get; set; } = true;

    public string FallbackText { get; set; } = "ПРОМО-БЛОК";
}

public sealed class MenuVisibilitySettings
{
    public bool ShowStockStatus { get; set; } = true;

    // Имя JSON намеренно сохранено, чтобы существующие пользовательские настройки
    // продолжили управлять расширенным блоком без сброса значения при обновлении.
    [JsonPropertyName("ShowRightCharacter")]
    public bool ShowCustomText { get; set; } = true;

    public bool ShowPromotionBlock { get; set; } = true;

    /// <summary>Включает интерактивного питомца, объявленного активной темой.</summary>
    public bool ShowInteractivePet { get; set; } = true;
}

public sealed class TokenInventorySettings
{
    /// <summary>
    /// Включает точный программный учёт и сервисные действия пересчёта/пополнения.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Не позволяет купить больше жетонов, чем находится на учтённом остатке.</summary>
    public bool PurchaseLimitEnabled { get; set; } = true;
}

public sealed class LoggingSettings
{
    public string MinimumLevel { get; set; } = "Info";

    public int MaxFileSizeMb { get; set; } = 5;

    public int RetentionDays { get; set; } = 14;
}

public sealed class SecuritySettings
{
    /// <summary>Если включено, SERVICE и debug-действие требуют сервисный PIN.</summary>
    public bool ServicePinEnabled { get; set; }

    /// <summary>Случайная соль PBKDF2 в Base64. Сам PIN в конфигурации не хранится.</summary>
    public string ServicePinSaltBase64 { get; set; } = string.Empty;

    /// <summary>Результат PBKDF2-SHA256 в Base64.</summary>
    public string ServicePinHashBase64 { get; set; } = string.Empty;

    public int ServicePinIterations { get; set; } = ServiceAccessPolicy.DefaultPbkdf2Iterations;
}
