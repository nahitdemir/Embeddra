using System.Security.Claims;
using Embeddra.BuildingBlocks.Authentication;
using Embeddra.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Http;

namespace Embeddra.Admin.WebApi.Auth;

public static class AdminAuthContext
{
    public static AdminAuthInfo Get(HttpContext context)
    {
        if (ApiKeyAuthenticationContext.IsPlatformKey(context))
        {
            return new AdminAuthInfo(AdminRoles.PlatformOwner, TenantContext.TenantId, true, false);
        }

        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var role = user.FindFirstValue(ClaimTypes.Role) ?? AdminRoles.TenantOwner;
            var tenantId = user.FindFirstValue(AdminClaims.TenantId) ?? TenantContext.TenantId;
            var isPlatform = string.Equals(role, AdminRoles.PlatformOwner, StringComparison.OrdinalIgnoreCase);
            return new AdminAuthInfo(role, tenantId, isPlatform, true);
        }

        if (context.Items.TryGetValue(ApiKeyAuthenticationContext.RoleItemName, out var roleValue)
            && roleValue is string roleItem)
        {
            return new AdminAuthInfo(roleItem, TenantContext.TenantId, false, false);
        }

        return new AdminAuthInfo(null, TenantContext.TenantId, false, false);
    }

    public static bool CanTenantWrite(HttpContext context)
    {
        var info = Get(context);
        if (info.IsPlatform)
        {
            return true;
        }

        return info.Role is not null && AdminRoles.TenantWriteRoles.Contains(info.Role);
    }

    public static bool CanTenantRead(HttpContext context)
    {
        var info = Get(context);
        if (info.IsPlatform)
        {
            return true;
        }

        return info.Role is not null && AdminRoles.TenantReadRoles.Contains(info.Role);
    }

    public static bool IsOwner(HttpContext context)
    {
        var info = Get(context);
        return info.Role == AdminRoles.TenantOwner;
    }
}

public sealed record AdminAuthInfo(string? Role, string? TenantId, bool IsPlatform, bool IsUser);

public static class AdminClaims
{
    public const string TenantId = "tenant_id";
}
