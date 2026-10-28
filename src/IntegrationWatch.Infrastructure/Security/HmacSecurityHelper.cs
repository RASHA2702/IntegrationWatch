using System.Security.Cryptography;
using System.Text;

namespace IntegrationWatch.Infrastructure.Security;

public static class HmacSecurityHelper
{
    public static string ComputeHmacSha256(string payload, string secret)
    {
        if (string.IsNullOrEmpty(payload)) throw new ArgumentNullException(nameof(payload));
        if (string.IsNullOrEmpty(secret)) throw new ArgumentNullException(nameof(secret));

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static bool VerifyHmacSha256(string payload, string secret, string expectedSignature)
    {
        if (string.IsNullOrEmpty(payload) || string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(expectedSignature))
        {
            return false;
        }

        var normalizedExpected = expectedSignature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
            ? expectedSignature.Substring(7).Trim().ToLowerInvariant()
            : expectedSignature.Trim().ToLowerInvariant();

        var computed = ComputeHmacSha256(payload, secret);

        var computedBytes = Encoding.UTF8.GetBytes(computed);
        var expectedBytes = Encoding.UTF8.GetBytes(normalizedExpected);

        // Constant-time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(computedBytes, expectedBytes);
    }
}
