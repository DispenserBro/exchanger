using Exchanger.Hardware.PeripheryController;
using Exchanger.Hardware.SerialPortHardware;
using NUnit.Framework;
using System.Security.Cryptography;

namespace Exchanger.HardwareTests;

[TestFixture]
[NonParallelizable]
[Category("HardwareProbe")]
public sealed class BoardHandshakeProbeTests
{
    [Test]
    public void RandomHandshake_ClassifiesControllerProofWithoutLoggingRawFrames()
    {
        BoardTestConfiguration configuration = BoardTestConfiguration.RequireSafeTests();
        using var port = new SerialPortHardware();
        using var cipher = new Aes256EcbBlockCipher(PeripheryControllerSecurity.Key);
        byte[] hostChallenge = RandomNumberGenerator.GetBytes(16);
        bool startupBannerReceived = false;
        PeripheryMessage? challenge = null;
        PeripheryMessage? proof = null;
        bool hostAuthorizationAccepted = false;
        bool controllerAuthorizationAccepted = false;
        bool readyReceived = false;

        port.OnDataReceived += frame =>
        {
            PeripheryMessage message = PeripheryProtocolParser.Parse(frame);
            switch (message.Kind)
            {
                case PeripheryMessageKind.StartupBanner:
                    startupBannerReceived = true;
                    break;
                case PeripheryMessageKind.Challenge:
                    challenge = message;
                    break;
                case PeripheryMessageKind.EncryptedResponse:
                    proof = message;
                    break;
                case PeripheryMessageKind.AuthorizationConfirmed:
                    controllerAuthorizationAccepted = true;
                    break;
                case PeripheryMessageKind.HostAuthorizationAccepted:
                    hostAuthorizationAccepted = true;
                    break;
                case PeripheryMessageKind.Ready:
                    readyReceived = true;
                    break;
            }
        };

        port.Connect(configuration.PortName, configuration.BaudRate);
        Assert.That(port.IsConnected, Is.True, "Не удалось открыть COM-порт для безопасного handshake probe.");
        PumpUntil(
            port,
            () => startupBannerReceived,
            TimeSpan.FromSeconds(20),
            "После аппаратного сброса плата не прислала стартовый баннер periphery controller ver_<version>.");
        PumpFor(port, TimeSpan.FromSeconds(2));

        port.SendCommand("07" + Convert.ToHexString(hostChallenge));
        PumpUntil(
            port,
            () => challenge.HasValue,
            configuration.ResponseTimeout,
            "Плата не вернула CHAL на фиксированный запрос 07.");

        byte[] encryptedControllerChallenge = cipher.EncryptBlock(
            Convert.FromHexString(challenge!.Value.Payload));
        port.SendCommand("08" + Convert.ToHexString(encryptedControllerChallenge));
        PumpUntil(
            port,
            () => proof.HasValue,
            configuration.ResponseTimeout,
            "Плата не вернула RAES после ответа 08.");

        PumpUntil(
            port,
            () => readyReceived,
            TimeSpan.FromSeconds(5),
            "После RAES плата не прислала точную завершающую строку READY!.");

        byte[] actualProof = Convert.FromHexString(proof!.Value.Payload);
        byte[] encryptedHostChallenge = cipher.EncryptBlock(hostChallenge);
        byte[] decryptedHostChallenge = DecryptBlock(hostChallenge, PeripheryControllerSecurity.Key);
        byte[] reversedInput = hostChallenge.Reverse().ToArray();
        byte[] encryptedReversedInput = cipher.EncryptBlock(reversedInput);
        byte[] reversedEncryptedInput = encryptedReversedInput.Reverse().ToArray();

        bool matchesEncryption = CryptographicOperations.FixedTimeEquals(actualProof, encryptedHostChallenge);
        bool matchesDecryption = CryptographicOperations.FixedTimeEquals(actualProof, decryptedHostChallenge);
        bool matchesReversedInput = CryptographicOperations.FixedTimeEquals(actualProof, encryptedReversedInput);
        bool matchesReversedInputAndOutput = CryptographicOperations.FixedTimeEquals(actualProof, reversedEncryptedInput);

        TestContext.Out.WriteLine(
            $"Handshake probe: banner=yes, CHAL=yes, RAES=yes, AUTH_PC_OK={hostAuthorizationAccepted}, "
            + $"AUTH_CNT_OK={controllerAuthorizationAccepted}, READY!=yes, "
            + $"AES-Encrypt(07)={matchesEncryption}, AES-Decrypt(07)={matchesDecryption}, "
            + $"reverse-input={matchesReversedInput}, "
            + $"reverse-input-output={matchesReversedInputAndOutput}, "
            + $"proof-fingerprint={Fingerprint(actualProof)}, expected-fingerprint={Fingerprint(encryptedHostChallenge)}.");

        Assert.That(
            matchesEncryption,
            Is.True,
            "RAES не равен AES-256-ECB-Encrypt исходного блока 07 с текущим ключом.");
    }

    private static void PumpFor(SerialPortHardware port, TimeSpan duration)
    {
        DateTime deadline = DateTime.UtcNow + duration;
        while (DateTime.UtcNow < deadline)
        {
            port.ProcessPendingEvents();
            Thread.Sleep(10);
        }
    }

    private static void PumpUntil(
        SerialPortHardware port,
        Func<bool> condition,
        TimeSpan timeout,
        string failureMessage)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            port.ProcessPendingEvents();
            if (condition())
            {
                return;
            }

            Thread.Sleep(10);
        }

        port.ProcessPendingEvents();
        Assert.That(condition(), Is.True, failureMessage);
    }

    private static byte[] DecryptBlock(ReadOnlySpan<byte> value, ReadOnlySpan<byte> key)
    {
        using Aes aes = Aes.Create();
        aes.Key = key.ToArray();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        using ICryptoTransform decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(value.ToArray(), 0, value.Length);
    }

    private static string Fingerprint(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value))[..12];
}
