using System.Security.Cryptography;
using LoanProject.Infrastructure.Security;

namespace LoanProject.Infrastructure.Tests;

/// <summary>Pure unit tests for the AES-256-GCM field encryptor — no infrastructure.</summary>
public class AesGcmFieldEncryptorTests
{
    private static AesGcmFieldEncryptor NewEncryptor() => new("unit-test-key-material");

    [Fact]
    public void EncryptThenDecrypt_RoundTrips()
    {
        var encryptor = NewEncryptor();
        const string plaintext = "1234567890123";

        var cipher = encryptor.Encrypt(plaintext);

        Assert.NotEqual(plaintext, cipher);
        Assert.Equal(plaintext, encryptor.Decrypt(cipher));
    }

    [Fact]
    public void Encrypt_SameInputTwice_ProducesDifferentCiphertext()
    {
        var encryptor = NewEncryptor();

        // A fresh nonce per call means no equality leak between identical values.
        Assert.NotEqual(encryptor.Encrypt("secret"), encryptor.Encrypt("secret"));
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_Throws()
    {
        var encryptor = NewEncryptor();
        var bytes = Convert.FromBase64String(encryptor.Encrypt("secret"));
        bytes[^1] ^= 0xFF; // flip a bit — GCM's tag check must reject it

        Assert.ThrowsAny<CryptographicException>(
            () => encryptor.Decrypt(Convert.ToBase64String(bytes)));
    }

    [Fact]
    public void Decrypt_WithDifferentKey_Throws()
    {
        var cipher = new AesGcmFieldEncryptor("key-one").Encrypt("secret");

        Assert.ThrowsAny<CryptographicException>(
            () => new AesGcmFieldEncryptor("key-two").Decrypt(cipher));
    }
}
