using System.Security.Cryptography;
using System.Text;
using FollowerCounter.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace FollowerCounter.Infrastructure.Security;

public class SecretProtector : ISecretProtector
{
    private const string CurrentVersion = "v1";
    private const int NonceSizeBytes = 12; // 96-bit recommended for AES-GCM
    private const int TagSizeBytes = 16;   // 128-bit authentication tag

    private readonly SecretProtectionOptions _options;
    private readonly Dictionary<string, byte[]> _resolvedKeys = new();

    public SecretProtector(IOptions<SecretProtectionOptions> options)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.ActiveKeyId))
        {
            throw new InvalidOperationException("SecretProtection:ActiveKeyId is not configured.");
        }

        if (!_options.Keys.TryGetValue(_options.ActiveKeyId, out var activeKeyBase64) || string.IsNullOrWhiteSpace(activeKeyBase64))
        {
            throw new InvalidOperationException($"Active encryption key '{_options.ActiveKeyId}' was not found in SecretProtection:Keys.");
        }

        foreach (var (keyId, base64Key) in _options.Keys)
        {
            try
            {
                var keyBytes = Convert.FromBase64String(base64Key);
                if (keyBytes.Length != 32)
                {
                    throw new InvalidOperationException($"Key '{keyId}' must be exactly 256 bits (32 bytes). Found {keyBytes.Length} bytes.");
                }
                _resolvedKeys[keyId] = keyBytes;
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException($"Key '{keyId}' is not valid Base64 string.", ex);
            }
        }
    }

    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            throw new ArgumentException("Plaintext cannot be null or empty.", nameof(plaintext));
        }

        var activeKey = _resolvedKeys[_options.ActiveKeyId];
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

        var nonce = new byte[NonceSizeBytes];
        RandomNumberGenerator.Fill(nonce);

        var ciphertextBytes = new byte[plaintextBytes.Length];
        var tagBytes = new byte[TagSizeBytes];

        using (var aesGcm = new AesGcm(activeKey, TagSizeBytes))
        {
            aesGcm.Encrypt(nonce, plaintextBytes, ciphertextBytes, tagBytes);
        }

        return $"{CurrentVersion}:{_options.ActiveKeyId}:{Convert.ToBase64String(nonce)}:{Convert.ToBase64String(ciphertextBytes)}:{Convert.ToBase64String(tagBytes)}";
    }

    public string Decrypt(string ciphertextPayload)
    {
        if (string.IsNullOrWhiteSpace(ciphertextPayload))
        {
            throw new ArgumentException("Ciphertext cannot be null or empty.", nameof(ciphertextPayload));
        }

        var parts = ciphertextPayload.Split(':');
        if (parts.Length != 5)
        {
            throw new CryptographicException("Invalid ciphertext payload format. Expected v1:keyId:nonce:ciphertext:tag.");
        }

        var version = parts[0];
        var keyId = parts[1];
        var nonceBase64 = parts[2];
        var ciphertextBase64 = parts[3];
        var tagBase64 = parts[4];

        if (version != CurrentVersion)
        {
            throw new CryptographicException($"Unsupported ciphertext version '{version}'. Expected '{CurrentVersion}'.");
        }

        if (!_resolvedKeys.TryGetValue(keyId, out var keyBytes))
        {
            throw new CryptographicException($"Encryption key '{keyId}' required to decrypt this token is not available in configuration.");
        }

        var nonce = Convert.FromBase64String(nonceBase64);
        var ciphertextBytes = Convert.FromBase64String(ciphertextBase64);
        var tagBytes = Convert.FromBase64String(tagBase64);

        var plaintextBytes = new byte[ciphertextBytes.Length];

        using (var aesGcm = new AesGcm(keyBytes, TagSizeBytes))
        {
            aesGcm.Decrypt(nonce, ciphertextBytes, tagBytes, plaintextBytes);
        }

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
