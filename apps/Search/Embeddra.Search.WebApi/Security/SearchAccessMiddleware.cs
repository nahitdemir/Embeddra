using Embeddra.BuildingBlocks.Authentication;
using Embeddra.BuildingBlocks.Tenancy;
using Embeddra.Search.Infrastructure.Security;
using Microsoft.AspNetCore.Http;

namespace Embeddra.Search.WebApi.Security;

public sealed class SearchAccessMiddleware
{
    private static readonly HashSet<string> ProtectedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/search",
        "/search:click",
        "/events/click"
    };

    private readonly RequestDelegate _next;

    public SearchAccessMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(
        HttpContext context,
        AllowedOriginRepository allowedOriginRepository,
        SearchRateLimiter rateLimiter)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!ProtectedPaths.Contains(path))
        {
            await _next(context);
            return;
        }

        var tenantId = TenantContext.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            await _next(context);
            return;
        }

        // Skip origin check for internal requests from Admin API
        var isInternalRequest = context.Request.Headers.TryGetValue("X-Internal-Request", out var internalReq)
            && string.Equals(internalReq.ToString(), "admin-api", StringComparison.OrdinalIgnoreCase);

        var origin = context.Request.Headers["Origin"].ToString();
        if (!string.IsNullOrWhiteSpace(origin) && !isInternalRequest)
        {
            var originTrimmed = origin.Trim();
            
            // Check specific API Key origins first
            if (context.Items.TryGetValue(ApiKeyAuthenticationContext.AllowedOriginsItemName, out var allowedOriginsObj)
                && allowedOriginsObj is string[] allowedOrigins 
                && allowedOrigins.Length > 0)
            {
                if (!allowedOrigins.Any(x => x == "*" || string.Equals(x, originTrimmed, StringComparison.OrdinalIgnoreCase)))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { error = "origin_not_allowed" });
                    return;
                }
            }
            else
            {
                // Fallback to Tenant-level origins (if any) or existing logic
                var allowed = await allowedOriginRepository.IsOriginAllowedAsync(
                    tenantId,
                    originTrimmed,
                    context.RequestAborted);
                if (!allowed)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { error = "origin_not_allowed" });
                    return;
                }
            }
        }

        var apiKeyId = context.Items[ApiKeyAuthenticationContext.ApiKeyIdItemName]?.ToString();
        var rateLimitKey = string.IsNullOrWhiteSpace(apiKeyId) ? tenantId : apiKeyId;
        if (!rateLimiter.TryConsume(rateLimitKey))
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsJsonAsync(new { error = "rate_limited" });
            return;
        }

        await _next(context);
    }
}
