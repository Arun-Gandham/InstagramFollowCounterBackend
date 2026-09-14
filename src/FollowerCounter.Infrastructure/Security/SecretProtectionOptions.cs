namespace FollowerCounter.Infrastructure.Security;

public class SecretProtectionOptions
{
    public const string SectionName = "SecretProtection";

    public string ActiveKeyId { get; set; } = "key01";

    /// <summary>
    /// Dictionary of key ID to 256-bit (32-byte) Base64 encoded encryption keys.
    /// Allows historical keys to decrypt old tokens during key rotation.
    /// </summary>
    public Dictionary<string, string> Keys { get; set; } = new();
}
