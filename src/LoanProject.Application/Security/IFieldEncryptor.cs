namespace LoanProject.Application.Security;

/// <summary>
/// Symmetric encryption for individual sensitive fields (PII) before they are
/// persisted. Abstracted so the domain and persistence mapping depend only on
/// "encrypt this string"; the algorithm and key handling live in Infrastructure.
/// The output is self-describing (it carries its own nonce), so Decrypt needs
/// nothing beyond the key the implementation already holds.
/// </summary>
public interface IFieldEncryptor
{
    /// <summary>Returns an opaque, storable representation of the plaintext.</summary>
    string Encrypt(string plaintext);

    /// <summary>Reverses <see cref="Encrypt"/>; throws if the input was tampered with.</summary>
    string Decrypt(string ciphertext);
}
