using Exchanger.Hardware.PeripheryController;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class PeripheryProtocolTests
{
    [TestCase("MONEY:COINS:30", PeripheryMessageKind.MoneyCoins, 30)]
    [TestCase("MONEY:BILL:100", PeripheryMessageKind.MoneyBill, 100)]
    [TestCase("MONEY:CASHLESS:500", PeripheryMessageKind.MoneyCashless, 500)]
    public void MoneyMessages_AreTyped(string frame, PeripheryMessageKind kind, int amount)
    {
        PeripheryMessage message = PeripheryProtocolParser.Parse(frame);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.Kind, Is.EqualTo(kind));
            Assert.That(message.NumericValue, Is.EqualTo(amount));
        }
    }

    [TestCase("MONEY:BILL:0")]
    [TestCase("MONEY:BILL:-10")]
    [TestCase("MONEY:CASHLESS:ABC")]
    [TestCase("RAES00")]
    [TestCase("CHALZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    [TestCase("AUTH_CNT_BAD")]
    [TestCase("READY")]
    [TestCase("ready!")]
    [TestCase("periphery controller ver_")]
    [TestCase("Periphery controller ver_2.3.5")]
    public void MalformedMessage_IsUnknown(string frame)
    {
        Assert.That(PeripheryProtocolParser.Parse(frame).Kind, Is.EqualTo(PeripheryMessageKind.Unknown));
    }

    [TestCase("HOPPER1 ERROR")]
    [TestCase("HOPPER ERROR")]
    public void DocumentedHopperErrorSpellings_AreTyped(string frame)
    {
        Assert.That(PeripheryProtocolParser.Parse(frame).Kind, Is.EqualTo(PeripheryMessageKind.Hopper1Error));
    }

    [TestCase("HOPPER2 OK", PeripheryMessageKind.Hopper2Ok)]
    [TestCase("HOPPER2 ERROR", PeripheryMessageKind.Hopper2Error)]
    public void Hopper2Results_AreTyped(string frame, PeripheryMessageKind expected)
    {
        Assert.That(PeripheryProtocolParser.Parse(frame).Kind, Is.EqualTo(expected));
    }

    [Test]
    public void Hopper2QueuedCount_IsParsedWithoutTreatingItAsDispensed()
    {
        PeripheryMessage message = PeripheryProtocolParser.Parse("HOPPER2 queued 10 prizes");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.Kind, Is.EqualTo(PeripheryMessageKind.Hopper2Queued));
            Assert.That(message.NumericValue, Is.EqualTo(10));
        }
    }

    [Test]
    public void Commands_MatchProtocol235()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(PeripheryProtocolCommands.RequestCashlessPayment(150), Is.EqualTo("163150"));
            Assert.That(PeripheryProtocolCommands.AcknowledgeCoins(30), Is.EqualTo("ASK:COINS:30"));
            Assert.That(PeripheryProtocolCommands.AcknowledgeBill(100), Is.EqualTo("ASK:BILL:100"));
            Assert.That(PeripheryProtocolCommands.AcknowledgeCashless(500), Is.EqualTo("ASK:CASHLESS:500"));
            Assert.That(PeripheryProtocolCommands.DisableHopper1Motor, Is.EqualTo("300"));
            Assert.That(PeripheryProtocolCommands.EnableHopper1Motor, Is.EqualTo("301"));
            Assert.That(PeripheryProtocolCommands.DispenseFromHopper1(12), Is.EqualTo("30912"));
            Assert.That(PeripheryProtocolCommands.DispenseFromHopper1(99), Is.EqualTo("30999"));
            Assert.That(PeripheryProtocolCommands.QueryHopper1Credit, Is.EqualTo("3090"));
            Assert.That(PeripheryProtocolCommands.DisableHopper2Motor, Is.EqualTo("400"));
            Assert.That(PeripheryProtocolCommands.EnableHopper2Motor, Is.EqualTo("401"));
            Assert.That(PeripheryProtocolCommands.DispenseOneFromHopper2, Is.EqualTo("4091"));
            Assert.That(PeripheryProtocolCommands.DispenseFromHopper2(10), Is.EqualTo("40910"));
            Assert.That(PeripheryProtocolCommands.QueryHopper2Credit, Is.EqualTo("4090"));
            Assert.That(PeripheryProtocolCommands.QueryCoinAcceptorStatus, Is.EqualTo("140"));
            Assert.That(PeripheryProtocolCommands.QueryBillAcceptorStatus, Is.EqualTo("150"));
            Assert.That(PeripheryProtocolCommands.QueryCashlessStatus, Is.EqualTo("160"));
            Assert.That(PeripheryProtocolCommands.QueryControllerStatus, Is.EqualTo("99"));
        }
    }

    [TestCase(0)]
    [TestCase(100)]
    public void Hopper1DispenseCommand_RejectsCountsOutsideOneToNinetyNine(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>((Action)(() =>
        {
            _ = PeripheryProtocolCommands.DispenseFromHopper1(count);
        }));
    }

    [TestCase("INPUT67108861")]
    [TestCase("INPUT 67108861")]
    public void DigitalInputMask_IsParsed(string frame)
    {
        PeripheryMessage message = PeripheryProtocolParser.Parse(frame);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.Kind, Is.EqualTo(PeripheryMessageKind.DigitalInputs));
            Assert.That(message.NumericValue, Is.EqualTo(67_108_861));
        }
    }

    [Test]
    public void ControllerInputDefaults_MatchConfirmedWiring()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(PeripheryControllerInputs.ServiceButton, Is.EqualTo(8));
            Assert.That(PeripheryControllerInputs.Hopper1StockLow, Is.EqualTo(1));
            Assert.That(PeripheryControllerInputs.Hopper2StockLow, Is.EqualTo(4));
            Assert.That(PeripheryControllerInputs.Hopper1DispensePulse, Is.EqualTo(24));
            Assert.That(PeripheryControllerInputs.Hopper2DispensePulse, Is.EqualTo(25));
        }
    }

    [TestCase("HOPPER1 APPEND=7", PeripheryMessageKind.Hopper1Append, 7)]
    [TestCase("HOPPER1 CREDIT=0", PeripheryMessageKind.Hopper1Credit, 0)]
    [TestCase("HOPPER1 CREDIT=12", PeripheryMessageKind.Hopper1Credit, 12)]
    [TestCase("HOPPER1 PULSE=1", PeripheryMessageKind.Hopper1Pulse, 1)]
    [TestCase("HOPPER2 APPEND=3", PeripheryMessageKind.Hopper2Append, 3)]
    [TestCase("HOPPER2 CREDIT=4", PeripheryMessageKind.Hopper2Credit, 4)]
    [TestCase("HOPPER2 PULSE=1", PeripheryMessageKind.Hopper2Pulse, 1)]
    public void UpdatedHopperFeedback_IsTyped(string frame, PeripheryMessageKind kind, int value)
    {
        PeripheryMessage message = PeripheryProtocolParser.Parse(frame);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.Kind, Is.EqualTo(kind));
            Assert.That(message.NumericValue, Is.EqualTo(value));
        }
    }

    [Test]
    public void Authentication_CompletesDoubleHandshake()
    {
        byte[] hostChallenge = Convert.FromHexString("00112233445566778899AABBCCDDEEFF");
        var authenticator = new ControllerAuthenticator(new IdentityBlockCipher(), () => hostChallenge);

        Assert.That(authenticator.BeginStartup(), Is.True);
        authenticator.Handle(PeripheryProtocolParser.Parse("periphery controller ver_2.3.5"));
        string? request = authenticator.StartHandshake();
        string? response = authenticator.Handle(PeripheryProtocolParser.Parse("CHALFFEEDDCCBBAA99887766554433221100"));
        authenticator.Handle(PeripheryProtocolParser.Parse("RAES00112233445566778899AABBCCDDEEFF"));
        authenticator.Handle(PeripheryProtocolParser.Parse("AUTH_PC_OK"));
        Assert.That(authenticator.State, Is.EqualTo(ControllerAuthenticationState.AwaitingReady));
        authenticator.Handle(PeripheryProtocolParser.Parse("READY!"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(request, Is.EqualTo("0700112233445566778899AABBCCDDEEFF"));
            Assert.That(response, Is.EqualTo("08FFEEDDCCBBAA99887766554433221100"));
            Assert.That(authenticator.State, Is.EqualTo(ControllerAuthenticationState.Ready));
        }
    }

    [Test]
    public void Authentication_RejectsWrongControllerProof()
    {
        var authenticator = new ControllerAuthenticator(
            new IdentityBlockCipher(),
            () => new byte[16]);
        authenticator.BeginStartup();
        authenticator.Handle(PeripheryProtocolParser.Parse("periphery controller ver_test"));
        authenticator.StartHandshake();

        authenticator.Handle(PeripheryProtocolParser.Parse("CHALFFEEDDCCBBAA99887766554433221100"));
        authenticator.Handle(PeripheryProtocolParser.Parse("RAESFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"));

        Assert.That(authenticator.State, Is.EqualTo(ControllerAuthenticationState.Failed));
    }

    [Test]
    public void Authentication_DoesNotAcceptOutOfOrderAuthorizationConfirmation()
    {
        var authenticator = new ControllerAuthenticator(
            new IdentityBlockCipher(),
            () => new byte[16]);
        authenticator.BeginStartup();

        authenticator.Handle(PeripheryProtocolParser.Parse("AUTH_CNT_OK"));

        Assert.That(authenticator.State, Is.EqualTo(ControllerAuthenticationState.AwaitingStartupBanner));
    }

    [Test]
    public void AuthorizationConfirmation_IsTyped()
    {
        Assert.That(
            PeripheryProtocolParser.Parse("AUTH_CNT_OK").Kind,
            Is.EqualTo(PeripheryMessageKind.AuthorizationConfirmed));
    }

    [Test]
    public void StartupAndFinalAuthorizationFrames_AreStrictlyTyped()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                PeripheryProtocolParser.Parse("periphery controller ver_2.3.5").Kind,
                Is.EqualTo(PeripheryMessageKind.StartupBanner));
            Assert.That(
                PeripheryProtocolParser.Parse("AUTH_PC_OK").Kind,
                Is.EqualTo(PeripheryMessageKind.HostAuthorizationAccepted));
            Assert.That(
                PeripheryProtocolParser.Parse("READY!").Kind,
                Is.EqualTo(PeripheryMessageKind.Ready));
        }
    }

    [TestCase("CHAL00112233445566778899AABBCCDDEEFF", PeripheryMessageKind.Challenge)]
    [TestCase("CHAL 00112233445566778899AABBCCDDEEFF", PeripheryMessageKind.Challenge)]
    [TestCase("CHAL:00112233445566778899AABBCCDDEEFF", PeripheryMessageKind.Challenge)]
    [TestCase("RAES00112233445566778899AABBCCDDEEFF", PeripheryMessageKind.EncryptedResponse)]
    [TestCase("RAES 00112233445566778899AABBCCDDEEFF", PeripheryMessageKind.EncryptedResponse)]
    [TestCase("RAES:00112233445566778899AABBCCDDEEFF", PeripheryMessageKind.EncryptedResponse)]
    public void AuthenticationPayload_AcceptsPlateConfigAppSeparators(
        string frame,
        PeripheryMessageKind expectedKind)
    {
        PeripheryMessage message = PeripheryProtocolParser.Parse(frame);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.Kind, Is.EqualTo(expectedKind));
            Assert.That(message.Payload, Is.EqualTo("00112233445566778899AABBCCDDEEFF"));
        }
    }

    [Test]
    public void ValidRaes_DoesNotAuthorizeWithoutReadyBang()
    {
        var authenticator = new ControllerAuthenticator(
            new IdentityBlockCipher(),
            () => new byte[16]);
        authenticator.BeginStartup();
        authenticator.Handle(PeripheryProtocolParser.Parse("periphery controller ver_test"));
        authenticator.StartHandshake();
        authenticator.Handle(PeripheryProtocolParser.Parse("CHALFFEEDDCCBBAA99887766554433221100"));
        authenticator.Handle(PeripheryProtocolParser.Parse("RAES00000000000000000000000000000000"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(authenticator.State, Is.EqualTo(ControllerAuthenticationState.AwaitingReady));
            authenticator.Handle(PeripheryProtocolParser.Parse("READY"));
            Assert.That(authenticator.State, Is.EqualTo(ControllerAuthenticationState.AwaitingReady));
        }
    }

    [Test]
    public void Aes256Ecb_UsesKnownStandardVector()
    {
        byte[] key = Convert.FromHexString("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F");
        byte[] plaintext = Convert.FromHexString("00112233445566778899AABBCCDDEEFF");
        using var cipher = new Aes256EcbBlockCipher(key);

        byte[] encrypted = cipher.EncryptBlock(plaintext);

        Assert.That(Convert.ToHexString(encrypted), Is.EqualTo("8EA2B7CA516745BFEAFC49904B496089"));
    }

    [Test]
    public void EmbeddedControllerKey_IsDecodedBeforeUse()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(PeripheryControllerSecurity.Key.Length, Is.EqualTo(32));
            Assert.That(
                Convert.ToHexString(PeripheryControllerSecurity.Key),
                Is.Not.EqualTo("37363534333231303F3E3D3C3B3A393827262524232221202F2E2D2C2B2A2928"));
        }
    }

    [Test]
    public void EmbeddedKey_ReproducesControllerReferenceProof()
    {
        byte[] challenge = Convert.FromHexString("8C704E9C4CBF4BA2255741C566FB0F7A");
        using var cipher = new Aes256EcbBlockCipher(PeripheryControllerSecurity.Key);

        string result = Convert.ToHexString(cipher.EncryptBlock(challenge));

        Assert.That(result, Is.EqualTo("61AAAC26B3BCBB23EA9E610A49D1541F"));
    }
}
