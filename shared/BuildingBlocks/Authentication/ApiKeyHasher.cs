using System.Security.Cryptography;
using System.Text;

namespace Embeddra.BuildingBlocks.Authentication;

public static class ApiKeyHasher
{
    public static string ComputeHash(string apiKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string ComputePrefix(string apiKey, int length = 8)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return string.Empty;
        }

        var normalized = apiKey.Trim();
        return normalized.Length <= length ? normalized : normalized[..length];
    }
}
