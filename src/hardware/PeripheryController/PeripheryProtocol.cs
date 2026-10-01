using System;
using System.Globalization;

namespace Exchanger.Hardware.PeripheryController;

public enum PeripheryMessageKind
{
    Unknown,
    StartupBanner,
    Ready,
    HostAuthorizationAccepted,
    AuthorizationConfirmed,
    EncryptedResponse,
    Challenge,
    MoneyCoins,
    MoneyBill,
    MoneyCashless,
    CoinPulse,
    Hopper1Ok,
    Hopper1Error,
    Hopper1Append,
    Hopper1Credit,
    Hopper1Pulse,
    Hopper2Queued,
    Hopper2Ok,
    Hopper2Error,
    Hopper2Append,
    Hopper2Credit,
    Hopper2Pulse,
    Service,
    ControllerError,
    TransportError,
    DigitalInputs,
}

public readonly record struct PeripheryMessage(
    PeripheryMessageKind Kind,
    int NumericValue = 0,
    string Payload = "");

/// <summary>
/// Чистый парсер входных ASCII-кадров протокола универсального контроллера 2.3.5.
/// Не журналирует сырые данные и не зависит от Godot.
/// </summary>
public static class PeripheryProtocolParser
{
    public static PeripheryMessage Parse(string frame)
    {
        string value = (frame ?? string.Empty).Trim();
        const string startupBannerPrefix = "periphery controller ver_";
        if (value.StartsWith(startupBannerPrefix, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(value[startupBannerPrefix.Length..]))
        {
            return new PeripheryMessage(
                PeripheryMessageKind.StartupBanner,
                Payload: value[startupBannerPrefix.Length..].Trim());
        }

        if (value.Equals("READY!", StringComparison.Ordinal))
        {
            return new PeripheryMessage(PeripheryMessageKind.Ready);
        }

        if (value.Equals("AUTH_PC_OK", StringComparison.OrdinalIgnoreCase))
        {
            return new PeripheryMessage(PeripheryMessageKind.HostAuthorizationAccepted);
        }

        if (value.Equals("AUTH_CNT_OK", StringComparison.OrdinalIgnoreCase))
        {
            return new PeripheryMessage(PeripheryMessageKind.AuthorizationConfirmed);
        }

        if (value.Equals("SERVICE", StringComparison.OrdinalIgnoreCase))
        {
            return new PeripheryMessage(PeripheryMessageKind.Service);
        }

        if (value.Equals("COIN", StringComparison.OrdinalIgnoreCase))
        {
            return new PeripheryMessage(PeripheryMessageKind.CoinPulse);
        }

        if (value.Equals("HOPPER1 OK", StringComparison.OrdinalIgnoreCase))
        {
            return new PeripheryMessage(PeripheryMessageKind.Hopper1Ok);
        }

        if (value.Equals("HOPPER1 ERROR", StringComparison.OrdinalIgnoreCase)
            || value.Equals("HOPPER ERROR", StringComparison.OrdinalIgnoreCase))
        {
            return new PeripheryMessage(PeripheryMessageKind.Hopper1Error);
        }

        if (TryParseHopperValue(value, 1, "APPEND", minimum: 1, out int hopper1Append))
        {
            return new PeripheryMessage(PeripheryMessageKind.Hopper1Append, hopper1Append);
        }

        if (TryParseHopperValue(value, 1, "CREDIT", minimum: 0, out int hopper1Credit))
        {
            return new PeripheryMessage(PeripheryMessageKind.Hopper1Credit, hopper1Credit);
        }

        if (TryParseHopperValue(value, 1, "PULSE", minimum: 1, out int hopper1Pulse)
            && hopper1Pulse == 1)
        {
            return new PeripheryMessage(PeripheryMessageKind.Hopper1Pulse, hopper1Pulse);
        }

        if (value.Equals("HOPPER2 OK", StringComparison.OrdinalIgnoreCase))
        {
            return new PeripheryMessage(PeripheryMessageKind.Hopper2Ok);
        }

        if (value.Equals("HOPPER2 ERROR", StringComparison.OrdinalIgnoreCase))
        {
            return new PeripheryMessage(PeripheryMessageKind.Hopper2Error);
        }

        if (TryParseHopperValue(value, 2, "APPEND", minimum: 1, out int hopper2Append))
        {
            return new PeripheryMessage(PeripheryMessageKind.Hopper2Append, hopper2Append);
        }

        if (TryParseHopperValue(value, 2, "CREDIT", minimum: 0, out int hopper2Credit))
        {
            return new PeripheryMessage(PeripheryMessageKind.Hopper2Credit, hopper2Credit);
        }

        if (TryParseHopperValue(value, 2, "PULSE", minimum: 1, out int hopper2Pulse)
            && hopper2Pulse == 1)
        {
            return new PeripheryMessage(PeripheryMessageKind.Hopper2Pulse, hopper2Pulse);
        }

        if (TryParseHopper2Queued(value, out int queuedCount))
        {
            return new PeripheryMessage(PeripheryMessageKind.Hopper2Queued, queuedCount);
        }

        if (TryParseHexPayload(value, "RAES", out string encrypted))
        {
            return new PeripheryMessage(PeripheryMessageKind.EncryptedResponse, Payload: encrypted);
        }

        if (TryParseHexPayload(value, "CHAL", out string challenge))
        {
            return new PeripheryMessage(PeripheryMessageKind.Challenge, Payload: challenge);
        }

        if (TryParseMoney(value, "MONEY:COINS:", PeripheryMessageKind.MoneyCoins, out PeripheryMessage coins))
        {
            return coins;
        }

        if (TryParseMoney(value, "MONEY:BILL:", PeripheryMessageKind.MoneyBill, out PeripheryMessage bill))
        {
            return bill;
        }

        if (TryParseMoney(value, "MONEY:CASHLESS:", PeripheryMessageKind.MoneyCashless, out PeripheryMessage cashless))
        {
            return cashless;
        }

        if (value.StartsWith("TRANSPORT_ERROR:", StringComparison.Ordinal))
        {
            return new PeripheryMessage(PeripheryMessageKind.TransportError);
        }

        if (value.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
        {
            return new PeripheryMessage(PeripheryMessageKind.ControllerError);
        }

        if (value.StartsWith("INPUT", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(value[5..].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int inputMask)
            && inputMask is >= 0 and <= 67_108_863)
        {
            return new PeripheryMessage(PeripheryMessageKind.DigitalInputs, inputMask);
        }

        return new PeripheryMessage(PeripheryMessageKind.Unknown);
    }

    private static bool TryParseMoney(
        string value,
        string prefix,
        PeripheryMessageKind kind,
        out PeripheryMessage message)
    {
        message = default;
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string amountText = value[prefix.Length..];
        if (!int.TryParse(amountText, NumberStyles.None, CultureInfo.InvariantCulture, out int amount)
            || amount <= 0)
        {
            return false;
        }

        message = new PeripheryMessage(kind, amount);
        return true;
    }

    private static bool TryParseHopper2Queued(string value, out int count)
    {
        const string prefix = "HOPPER2 QUEUED ";
        count = 0;
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string remainder = value[prefix.Length..];
        int separatorIndex = remainder.IndexOf(' ');
        if (separatorIndex <= 0
            || !int.TryParse(
                remainder[..separatorIndex],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out count)
            || count <= 0)
        {
            count = 0;
            return false;
        }

        string unit = remainder[(separatorIndex + 1)..];
        return unit.Equals("PRIZE", StringComparison.OrdinalIgnoreCase)
               || unit.Equals("PRIZES", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseHopperValue(
        string value,
        int hopperNumber,
        string field,
        int minimum,
        out int number)
    {
        string prefix = $"HOPPER{hopperNumber.ToString(CultureInfo.InvariantCulture)} {field}=";
        number = 0;
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
               && int.TryParse(
                   value[prefix.Length..],
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out number)
               && number >= minimum;
    }

    private static bool TryParseHexPayload(string value, string prefix, out string payload)
    {
        payload = string.Empty;
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // plate_config_app допускает как слитный формат CHAL<hex>/RAES<hex>,
        // так и диагностические варианты с пробелом или двоеточием после префикса.
        string candidate = value[prefix.Length..].Trim().TrimStart(':').Trim();
        if (candidate.Length != 32)
        {
            return false;
        }

        foreach (char character in candidate)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        payload = candidate.ToUpperInvariant();
        return true;
    }
}

public static class PeripheryProtocolCommands
{
    public const int MaxHopperDispenseBatch = 99;
    public const string ResetController = "00";
    public const string ClearCommandQueue = "90";
    public const string QueryCoinAcceptorStatus = "140";
    public const string EnableCoinSmartMode = "141";
    public const string DisableCoinAcceptor = "142";
    public const string QueryBillAcceptorStatus = "150";
    public const string EnableBillAcceptor = "151";
    public const string DisableBillAcceptor = "152";
    public const string QueryCashlessStatus = "160";
    public const string EnableCashless = "161";
    public const string DisableCashless = "162";
    public const string DisableHopper1Motor = "300";
    public const string EnableHopper1Motor = "301";
    public const string QueryHopper1Credit = "3090";
    public const string QueryHopper1Queue = QueryHopper1Credit;
    public const string DisableHopper2Motor = "400";
    public const string EnableHopper2Motor = "401";
    public const string QueryHopper2Credit = "4090";
    public const string QueryHopper2Queue = QueryHopper2Credit;
    public const string DispenseOneFromHopper2 = "4091";
    public const string QueryDigitalInputs = "91";
    public const string QueryControllerStatus = "99";
    // Подсветка автомата: выключена, штатный режим и режим выдачи жетонов.
    public const string DisableLighting = "856500";
    public const string EnableDefaultLighting = "856501";
    public const string EnableDispenseLighting = "856540";
    // Дополнительный режим подсветки платы: выкл., штатный режим и мигание при выдаче.
    public const string DisableAuxiliaryLighting = "2170";
    public const string EnableAuxiliaryDefaultLighting = "2171";
    public const string EnableAuxiliaryDispenseLighting = "2172";

    public static string AcknowledgeCoinPulse() => "10";

    public static string AcknowledgeCoins(int amountRubles) => MoneyAcknowledgement("COINS", amountRubles);

    public static string AcknowledgeBill(int amountRubles) => MoneyAcknowledgement("BILL", amountRubles);

    public static string AcknowledgeCashless(int amountRubles) => MoneyAcknowledgement("CASHLESS", amountRubles);

    public static string RequestCashlessPayment(int amountRubles)
    {
        if (amountRubles is < 1 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(amountRubles), "Протокол 163 допускает сумму от 1 до 9999 ₽.");
        }

        return $"163{amountRubles.ToString(CultureInfo.InvariantCulture)}";
    }

    public static string DispenseFromHopper1(int tokenCount)
    {
        if (tokenCount is < 1 or > MaxHopperDispenseBatch)
        {
            throw new ArgumentOutOfRangeException(nameof(tokenCount), "Команда выдачи 309N допускает от 1 до 99 жетонов; 3090 зарезервирована для запроса CREDIT.");
        }

        return $"309{tokenCount.ToString(CultureInfo.InvariantCulture)}";
    }

    public static string DispenseFromHopper2(int tokenCount)
    {
        if (tokenCount is < 1 or > MaxHopperDispenseBatch)
        {
            throw new ArgumentOutOfRangeException(nameof(tokenCount), "Команда 409zz допускает от 1 до 99 единиц.");
        }

        return $"409{tokenCount.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string MoneyAcknowledgement(string source, int amountRubles)
    {
        if (amountRubles <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amountRubles));
        }

        return $"ASK:{source}:{amountRubles.ToString(CultureInfo.InvariantCulture)}";
    }
}
