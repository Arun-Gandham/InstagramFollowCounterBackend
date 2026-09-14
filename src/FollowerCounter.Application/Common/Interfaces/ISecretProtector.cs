namespace FollowerCounter.Application.Common.Interfaces;

public interface ISecretProtector
{
    /// <summary>
    /// Encrypts sensitive secret plaintext using AES-256-GCM and serializes to versioned format (v1:keyId:nonce:ciphertext:tag).
    /// </summary>
    string Encrypt(string plaintext);

    /// <summary>
    /// Decrypts versioned ciphertext using the key specified in the payload. Supports historical key rotation.
    /// </summary>
    string Decrypt(string ciphertext);
}
