using System.Security.Cryptography;
using FluentAssertions;
using FollowerCounter.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace FollowerCounter.UnitTests.Security;

public class SecretProtectorTests
{
    private readonly string _key01;
    private readonly string _key02;

    public SecretProtectorTests()
    {
        // 32 bytes = 256 bits
        _key01 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _key02 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }

    [Fact]
    public void Encrypt_And_Decrypt_ShouldReturnOriginalPlaintext()
    {
        // Arrange
        var options = Options.Create(new SecretProtectionOptions
        {
            ActiveKeyId = "key01",
            Keys = new Dictionary<string, string>
            {
                ["key01"] = _key01
            }
        });
        var protector = new SecretProtector(options);
        var token = "IGQW1234567890abcdef-secret-access-token";

        // Act
        var ciphertext = protector.Encrypt(token);
        var decrypted = protector.Decrypt(ciphertext);

        // Assert
        ciphertext.Should().StartWith("v1:key01:");
        ciphertext.Should().NotContain(token); // ensure plaintext is never exposed
        decrypted.Should().Be(token);
    }

    [Fact]
    public void KeyRotation_ShouldDecryptOldTokens_UsingPreviousKey()
    {
        // Arrange: Token encrypted with key01
        var initialOptions = Options.Create(new SecretProtectionOptions
        {
            ActiveKeyId = "key01",
            Keys = new Dictionary<string, string>
            {
                ["key01"] = _key01
            }
        });
        var initialProtector = new SecretProtector(initialOptions);
        var originalSecret = "IGQW_SECRET_LONG_LIVED_TOKEN_VAL";
        var ciphertextOld = initialProtector.Encrypt(originalSecret);

        // Act: Key rotation! Active key is now key02, but key01 is retained in dictionary
        var rotatedOptions = Options.Create(new SecretProtectionOptions
        {
            ActiveKeyId = "key02",
            Keys = new Dictionary<string, string>
            {
                ["key01"] = _key01, // old key retained for decryption
                ["key02"] = _key02  // new key used for encryption
            }
        });
        var rotatedProtector = new SecretProtector(rotatedOptions);

        var decryptedWithRotated = rotatedProtector.Decrypt(ciphertextOld);
        var newCiphertext = rotatedProtector.Encrypt("NEW_SECRET_TOKEN");

        // Assert
        decryptedWithRotated.Should().Be(originalSecret);
        newCiphertext.Should().StartWith("v1:key02:");
    }

    [Fact]
    public void Decrypt_WithMissingKey_ShouldThrowCryptographicException()
    {
        // Arrange
        var optionsKey01 = Options.Create(new SecretProtectionOptions
        {
            ActiveKeyId = "key01",
            Keys = new Dictionary<string, string> { ["key01"] = _key01 }
        });
        var protector = new SecretProtector(optionsKey01);
        var ciphertext = protector.Encrypt("my_token");

        // Protector with only key02 (key01 deleted)
        var optionsOnlyKey02 = Options.Create(new SecretProtectionOptions
        {
            ActiveKeyId = "key02",
            Keys = new Dictionary<string, string> { ["key02"] = _key02 }
        });
        var isolatedProtector = new SecretProtector(optionsOnlyKey02);

        // Act
        var act = () => isolatedProtector.Decrypt(ciphertext);

        // Assert
        act.Should().Throw<CryptographicException>()
            .WithMessage("*key 'key01' required to decrypt this token is not available*");
    }

    [Fact]
    public void Decrypt_WithTamperedPayload_ShouldThrowCryptographicException()
    {
        // Arrange
        var options = Options.Create(new SecretProtectionOptions
        {
            ActiveKeyId = "key01",
            Keys = new Dictionary<string, string> { ["key01"] = _key01 }
        });
        var protector = new SecretProtector(options);
        var ciphertext = protector.Encrypt("sensitive_token");

        // Tamper with authentication tag (last segment)
        var parts = ciphertext.Split(':');
        var tamperedTag = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var tamperedCiphertext = $"{parts[0]}:{parts[1]}:{parts[2]}:{parts[3]}:{tamperedTag}";

        // Act
        var act = () => protector.Decrypt(tamperedCiphertext);

        // Assert: AES-GCM must detect authentication tag tampering and fail
        act.Should().Throw<CryptographicException>();
    }
}
