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
        var tenantId = GetTenantId(context);

        TenantContext.TenantId = tenantId;
        context.Items[ItemKey] = tenantId;

        try
        {
            await _next(context);
        }
        finally
        {
            TenantContext.TenantId = null;
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
