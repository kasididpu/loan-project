using System.Security.Cryptography;
using System.Text;
using LoanProject.Application.Security;

namespace LoanProject.Infrastructure.Security;

/// <summary>
/// AES-256-GCM field encryptor. GCM is authenticated encryption: decryption
/// verifies an integrity tag, so a tampered ciphertext throws rather than
/// returning garbage. A fresh random 96-bit nonce per call means encrypting the
/// same value twice yields different output (no equality leak), while Decrypt
/// still needs only the key because the nonce travels with the ciphertext.
///
/// Wire format, concatenated then Base64: [nonce(12)][tag(16)][ciphertext].
/// </summary>
public sealed class AesGcmFieldEncryptor : IFieldEncryptor
{
    private const int NonceSize = 12; // 96-bit nonce — the GCM standard/optimum
    private const int TagSize = 16;   // 128-bit authentication tag

    private readonly byte[] _key;

    /// <param name="keyMaterial">
    /// The encryption secret (from Vault). A fixed 256-bit AES key is derived
    /// from it with SHA-256, so any high-entropy secret works regardless of its
    /// length and AES-GCM always gets the 32 bytes it needs.
    /// </param>
    public AesGcmFieldEncryptor(string keyMaterial)
    {
        if (string.IsNullOrWhiteSpace(keyMaterial))
            throw new ArgumentException("Encryption key material is required.", nameof(keyMaterial));

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(keyMaterial));
    }

    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var output = new byte[NonceSize + TagSize + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, output, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, output, NonceSize, TagSize);
        Buffer.BlockCopy(cipherBytes, 0, output, NonceSize + TagSize, cipherBytes.Length);
        return Convert.ToBase64String(output);
    }

    public string Decrypt(string ciphertext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ciphertext);

        var input = Convert.FromBase64String(ciphertext);
        if (input.Length < NonceSize + TagSize)
            throw new CryptographicException("Ciphertext is too short to be valid.");

        var nonce = input.AsSpan(0, NonceSize);
        var tag = input.AsSpan(NonceSize, TagSize);
        var cipherBytes = input.AsSpan(NonceSize + TagSize);
        var plainBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes); // throws on tag mismatch (tampering)
        return Encoding.UTF8.GetString(plainBytes);
    }
}
