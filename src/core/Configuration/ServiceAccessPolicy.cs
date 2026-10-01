using System;
using System.Security.Cryptography;

namespace Exchanger.Core.Configuration;

public enum ServiceAccessLevel
{
    None,
    Operator,
    Engineer,
    Debug,
}

public enum ServiceAccessStatus
{
    Authorized,
    InvalidPin,
    Locked,
    Unavailable,
}

public readonly record struct ServiceAccessResult(
    ServiceAccessStatus Status,
    ServiceAccessLevel Level = ServiceAccessLevel.None,
    int RemainingAttempts = 0,
    int LockSeconds = 0);

/// <summary>
/// Проверяет сервисный PIN по PBKDF2-SHA256. В конфигурации приложения
/// сохраняются только случайная соль, число итераций и производный хеш.
/// </summary>
public sealed class ServiceAccessPolicy
{
    public const int MinimumPinLength = 4;
    public const int MaximumPinLength = 12;
    public const int MaximumAttempts = 5;
    public const int LockDurationSeconds = 30;
    public const int DefaultPbkdf2Iterations = 210000;
    public const int MinimumPbkdf2Iterations = 100000;
    public const int MaximumPbkdf2Iterations = 1000000;

    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;

    private readonly byte[]? _salt;
    private readonly byte[]? _expectedHash;
    private readonly int _iterations;
    private readonly Func<DateTimeOffset> _clock;
    private int _failedAttempts;
    private DateTimeOffset _lockedUntil;

    public ServiceAccessPolicy(SecuritySettings settings, Func<DateTimeOffset>? clock = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _iterations = settings.ServicePinIterations;

        if (!settings.ServicePinEnabled
            || _iterations is < MinimumPbkdf2Iterations or > MaximumPbkdf2Iterations
            || !TryDecode(settings.ServicePinSaltBase64, SaltSizeBytes, out _salt)
            || !TryDecode(settings.ServicePinHashBase64, HashSizeBytes, out _expectedHash))
        {
            _salt = null;
            _expectedHash = null;
        }
    }

    public bool IsAvailable => _salt is not null && _expectedHash is not null;

    public static bool IsValidPin(string? pin)
    {
        if (pin is null || pin.Length is < MinimumPinLength or > MaximumPinLength)
        {
            return false;
        }

        foreach (char character in pin)
        {
            if (character is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    public static void SetPin(SecuritySettings settings, string pin)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!IsValidPin(pin))
        {
            throw new ArgumentException(
                $"PIN должен содержать от {MinimumPinLength} до {MaximumPinLength} цифр.",
                nameof(pin));
        }

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        byte[] hash = DeriveHash(pin, salt, DefaultPbkdf2Iterations);
        try
        {
            settings.ServicePinEnabled = true;
            settings.ServicePinSaltBase64 = Convert.ToBase64String(salt);
            settings.ServicePinHashBase64 = Convert.ToBase64String(hash);
            settings.ServicePinIterations = DefaultPbkdf2Iterations;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(hash);
        }
    }

    public static void ClearPin(SecuritySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.ServicePinSaltBase64 = string.Empty;
        settings.ServicePinHashBase64 = string.Empty;
        settings.ServicePinIterations = DefaultPbkdf2Iterations;
    }

    public ServiceAccessResult Verify(string? pin)
    {
        DateTimeOffset now = _clock();
        if (!IsAvailable)
        {
            return new ServiceAccessResult(ServiceAccessStatus.Unavailable);
        }

        if (now < _lockedUntil)
        {
            return new ServiceAccessResult(
                ServiceAccessStatus.Locked,
                LockSeconds: Math.Max(1, (int)Math.Ceiling((_lockedUntil - now).TotalSeconds)));
        }

        byte[] candidate = DeriveHash(pin ?? string.Empty, _salt!, _iterations);
        bool matches = CryptographicOperations.FixedTimeEquals(candidate, _expectedHash!);
        CryptographicOperations.ZeroMemory(candidate);

        if (matches)
        {
            _failedAttempts = 0;
            _lockedUntil = default;
            return new ServiceAccessResult(
                ServiceAccessStatus.Authorized,
                ServiceAccessLevel.Engineer,
                MaximumAttempts);
        }

        _failedAttempts++;
        if (_failedAttempts >= MaximumAttempts)
        {
            _failedAttempts = 0;
            _lockedUntil = now.AddSeconds(LockDurationSeconds);
            return new ServiceAccessResult(ServiceAccessStatus.Locked, LockSeconds: LockDurationSeconds);
        }

        return new ServiceAccessResult(
            ServiceAccessStatus.InvalidPin,
            RemainingAttempts: MaximumAttempts - _failedAttempts);
    }

    private static byte[] DeriveHash(string pin, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(
            pin,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            HashSizeBytes);

    private static bool TryDecode(string? value, int expectedSize, out byte[]? bytes)
    {
        bytes = null;
        try
        {
            byte[] decoded = Convert.FromBase64String(value ?? string.Empty);
            if (decoded.Length != expectedSize)
            {
                CryptographicOperations.ZeroMemory(decoded);
                return false;
            }

            bytes = decoded;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
