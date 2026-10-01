using System;

namespace Exchanger.Hardware.PeripheryController;

/// <summary>
/// Обфусцированное представление ключа контроллера 2.3.5.
/// Перед AES каждый байт восстанавливается через XOR 0x37 — так же, как
/// в рабочей Unity-реализации контроллера.
/// </summary>
public static class PeripheryControllerSecurity
{
    private const byte KeyMask = 0x37;

    private static readonly byte[] AesEncryptedKey =
    {
            0x37, 0x36, 0x35, 0x34, 0x33, 0x32, 0x31, 0x30, 0x3F, 0x3E, 0x3D, 0x3C, 0x3B, 0x3A, 0x39, 0x38, 0x27, 0x26, 0x25, 0x24, 0x23, 0x22, 0x21, 0x20, 0x2F, 0x2E, 0x2D, 0x2C, 0x2B, 0x2A, 0x29, 0x28
        };

    private static readonly byte[] AesKey = BuildAesKey();

    /// <summary>
    /// Эффективный 32-байтовый ключ AES-256. Значение переменной окружения,
    /// если оно задано, считается уже эффективным ключом и этим методом не проходит.
    /// </summary>
    public static ReadOnlySpan<byte> Key => AesKey;

    private static byte[] BuildAesKey()
    {
        byte[] key = new byte[AesEncryptedKey.Length];
        for (int index = 0; index < key.Length; index++)
        {
            key[index] = (byte)(AesEncryptedKey[index] ^ KeyMask);
        }

        return key;
    }
}
