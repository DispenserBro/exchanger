using System;
using System.Security.Cryptography;

namespace Exchanger.Hardware.PeripheryController;

public interface IControllerBlockCipher
{
    byte[] EncryptBlock(ReadOnlySpan<byte> plaintext);
}

/// <summary>
/// Подтверждённый владельцем профиль платы: AES-256, ECB, без padding.
/// Handshake всегда передаёт ровно один 16-байтовый блок.
/// </summary>
public sealed class Aes256EcbBlockCipher : IControllerBlockCipher, IDisposable
{
    private readonly Aes _aes;

    public Aes256EcbBlockCipher(ReadOnlySpan<byte> key)
    {
        if (key.Length != 32)
        {
            throw new ArgumentException("Ключ AES-256 должен содержать ровно 32 байта.", nameof(key));
        }

        _aes = Aes.Create();
        _aes.Key = key.ToArray();
        _aes.Mode = CipherMode.ECB;
        _aes.Padding = PaddingMode.None;
    }

    public byte[] EncryptBlock(ReadOnlySpan<byte> plaintext)
    {
        if (plaintext.Length != 16)
        {
            throw new ArgumentException("Handshake использует один блок из 16 байт.", nameof(plaintext));
        }

        using ICryptoTransform encryptor = _aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(plaintext.ToArray(), 0, plaintext.Length);
    }

    public void Dispose() => _aes.Dispose();
}

public enum ControllerAuthenticationState
{
    NotStarted,
    ConfigurationMissing,
    AwaitingStartupBanner,
    StartupDelay,
    AwaitingChallenge,
    AwaitingControllerProof,
    AwaitingReady,
    Ready,
    Failed,
}

public sealed class ControllerAuthenticator
{
    private readonly IControllerBlockCipher? _cipher;
    private readonly Func<byte[]> _challengeFactory;
    private byte[]? _hostChallenge;

    public ControllerAuthenticator(IControllerBlockCipher? cipher, Func<byte[]>? challengeFactory = null)
    {
        _cipher = cipher;
        _challengeFactory = challengeFactory ?? CreateRandomChallenge;
    }

    public ControllerAuthenticationState State { get; private set; }

    public bool BeginStartup()
    {
        _hostChallenge = null;
        if (_cipher is null)
        {
            State = ControllerAuthenticationState.ConfigurationMissing;
            return false;
        }

        State = ControllerAuthenticationState.AwaitingStartupBanner;
        return true;
    }

    public bool AcceptStartupBanner(PeripheryMessage message)
    {
        if (message.Kind != PeripheryMessageKind.StartupBanner
            || State != ControllerAuthenticationState.AwaitingStartupBanner)
        {
            return false;
        }

        State = ControllerAuthenticationState.StartupDelay;
        return true;
    }

    public string? StartHandshake()
    {
        if (_cipher is null)
        {
            State = ControllerAuthenticationState.ConfigurationMissing;
            return null;
        }

        if (State != ControllerAuthenticationState.StartupDelay)
        {
            return null;
        }

        _hostChallenge = _challengeFactory();
        if (_hostChallenge.Length != 16)
        {
            throw new InvalidOperationException("Генератор challenge должен вернуть 16 байт.");
        }

        State = ControllerAuthenticationState.AwaitingChallenge;
        return "07" + Convert.ToHexString(_hostChallenge);
    }

    public string? Handle(PeripheryMessage message)
    {
        if (_cipher is null)
        {
            State = ControllerAuthenticationState.ConfigurationMissing;
            return null;
        }

        if (message.Kind == PeripheryMessageKind.StartupBanner)
        {
            AcceptStartupBanner(message);
            return null;
        }

        if (message.Kind == PeripheryMessageKind.Challenge
            && State == ControllerAuthenticationState.AwaitingChallenge)
        {
            byte[] response = _cipher.EncryptBlock(Convert.FromHexString(message.Payload));
            State = ControllerAuthenticationState.AwaitingControllerProof;
            return "08" + Convert.ToHexString(response);
        }

        if (message.Kind == PeripheryMessageKind.EncryptedResponse
            && State == ControllerAuthenticationState.AwaitingControllerProof)
        {
            byte[] expected = _cipher.EncryptBlock(_hostChallenge!);
            byte[] received = Convert.FromHexString(message.Payload);
            if (!CryptographicOperations.FixedTimeEquals(expected, received))
            {
                State = ControllerAuthenticationState.Failed;
                return null;
            }

            State = ControllerAuthenticationState.AwaitingReady;
            return null;
        }

        if (message.Kind == PeripheryMessageKind.Ready
            && State == ControllerAuthenticationState.AwaitingReady)
        {
            State = ControllerAuthenticationState.Ready;
            return null;
        }

        return null;
    }

    public void Reset()
    {
        _hostChallenge = null;
        State = ControllerAuthenticationState.NotStarted;
    }

    public void Fail()
    {
        _hostChallenge = null;
        State = ControllerAuthenticationState.Failed;
    }

    private static byte[] CreateRandomChallenge()
    {
        byte[] challenge = new byte[16];
        RandomNumberGenerator.Fill(challenge);
        return challenge;
    }
}
