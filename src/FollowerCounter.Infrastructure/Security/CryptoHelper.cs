using System.Security.Cryptography;
using System.Text;

namespace FollowerCounter.Infrastructure.Security;

public static class CryptoHelper
{
    public static string GenerateSecureToken(int byteLength = 32)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string GenerateClaimCode()
    {
        // Generates user-friendly uppercase claim code: CLM-XXXX-XXXX-XXXX
        var bytes = RandomNumberGenerator.GetBytes(9);
        var base32Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // readable Crockford Base32
        var sb = new StringBuilder("CLM-");
        for (int i = 0; i < 9; i++)
        {
            if (i > 0 && i % 3 == 0) sb.Append('-');
            sb.Append(base32Chars[bytes[i] % base32Chars.Length]);
        }
        return sb.ToString();
    }

    public static string ComputeSha256Hash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static bool FixedTimeEquals(string a, string b)
    {
        if (a == null || b == null) return false;
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
