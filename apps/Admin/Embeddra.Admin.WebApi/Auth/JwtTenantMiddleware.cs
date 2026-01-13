using Embeddra.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Embeddra.Admin.WebApi.Auth;

public sealed class JwtTenantMiddleware
{
    private readonly RequestDelegate _next;

    public JwtTenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        var previousTenant = TenantContext.TenantId;
        var tenantId = context.User.FindFirstValue(AdminClaims.TenantId);
        var role = context.User.FindFirstValue(ClaimTypes.Role);
        var isPlatform = string.Equals(role, AdminRoles.PlatformOwner, StringComparison.OrdinalIgnoreCase);

        if (isPlatform && context.Request.Headers.TryGetValue(TenantIdMiddleware.HeaderName, out var headerValues))
        {
            var headerTenant = headerValues.ToString();
            if (!string.IsNullOrWhiteSpace(headerTenant))
            {
                tenantId = headerTenant;
            }
        }

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            TenantContext.TenantId = tenantId;
            context.Items[TenantIdMiddleware.ItemKey] = tenantId;
        }

        try
        {
            await _next(context);
        }
        finally
        {
            TenantContext.TenantId = previousTenant;
        }
    }
}
