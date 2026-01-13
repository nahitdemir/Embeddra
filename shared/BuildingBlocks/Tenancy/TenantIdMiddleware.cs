using Microsoft.AspNetCore.Http;

namespace Embeddra.BuildingBlocks.Tenancy;

public sealed class TenantIdMiddleware
{
    public const string HeaderName = "X-Tenant-Id";
    public const string ItemKey = "TenantId";

    private readonly RequestDelegate _next;

    public TenantIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        var previousTenant = TenantContext.TenantId;
        if (context.User.Identity?.IsAuthenticated == true)
        {
            if (!string.IsNullOrWhiteSpace(previousTenant))
            {
                context.Items[ItemKey] = previousTenant;
            }

            await _next(context);
            return;
        }

        var tenantId = GetTenantId(context);

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            TenantContext.TenantId = tenantId;
            context.Items[ItemKey] = tenantId;
        }
        else if (!string.IsNullOrWhiteSpace(previousTenant))
        {
            context.Items[ItemKey] = previousTenant;
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

    private static string? GetTenantId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            var provided = values.ToString();
            if (!string.IsNullOrWhiteSpace(provided))
            {
                return provided;
            }
        }

        return null;
    }
}
