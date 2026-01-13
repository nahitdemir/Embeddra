using Microsoft.AspNetCore.Http;

namespace Embeddra.BuildingBlocks.Authentication;

public static class ApiKeyAuthenticationContext
{
    public const string ApiKeyIdItemName = "ApiKeyId";
    public const string ApiKeyTypeItemName = "ApiKeyType";
    public const string PlatformKeyItemName = "IsPlatformKey";
    public const string RoleItemName = "AuthRole";
    public const string AllowedOriginsItemName = "AllowedOrigins";

    public static bool IsPlatformKey(HttpContext context)
    {
        if (context.Items.TryGetValue(PlatformKeyItemName, out var value))
        {
            return value is true;
        }

        return false;
    }

    public static string? GetApiKeyType(HttpContext context)
    {
        if (context.Items.TryGetValue(ApiKeyTypeItemName, out var value))
        {
            return value as string;
        }

        return null;
    }
}
