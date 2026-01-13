using Embeddra.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Embeddra.BuildingBlocks.Authentication;

public sealed class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IApiKeyValidator _validator;
    private readonly ApiKeyAuthenticationOptions _options;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;

    public ApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        IApiKeyValidator validator,
        ApiKeyAuthenticationOptions options,
        ILogger<ApiKeyAuthenticationMiddleware> logger)
    {
        _next = next;
        _validator = validator;
        _options = options;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            await _next(context);
            return;
        }

        if (ShouldSkip(context.Request.Path.Value))
        {
            await _next(context);
            return;
        }

        // Allow internal requests from Admin API (for preview/search proxy)
        if (context.Request.Headers.TryGetValue("X-Internal-Request", out var internalRequest)
            && string.Equals(internalRequest.ToString(), "admin-api", StringComparison.OrdinalIgnoreCase))
        {
            // Set tenant from X-Tenant-Id header if present
            var internalTenant = GetHeader(context, _options.TenantHeaderName);
            if (!string.IsNullOrWhiteSpace(internalTenant))
            {
                var prevTenant = TenantContext.TenantId;
                TenantContext.TenantId = internalTenant;
                context.Items["TenantId"] = internalTenant;
                context.Items[ApiKeyAuthenticationContext.ApiKeyIdItemName] = "internal-admin-api";
                context.Items[ApiKeyAuthenticationContext.RoleItemName] = "internal";
                
                try
                {
                    await _next(context);
                }
                finally
                {
                    TenantContext.TenantId = prevTenant;
                }
                return;
            }
            
            // If no tenant header, still allow the request but don't set tenant context
            context.Items[ApiKeyAuthenticationContext.ApiKeyIdItemName] = "internal-admin-api";
            context.Items[ApiKeyAuthenticationContext.RoleItemName] = "internal";
            await _next(context);
            return;
        }

        if (_options.AllowBearerToken && context.User.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(_options.ApiKeyHeaderName, out var apiKeyValues))
        {
            await RejectAsync(context, StatusCodes.Status401Unauthorized, "missing_api_key");
            return;
        }

        var apiKey = apiKeyValues.ToString();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            await RejectAsync(context, StatusCodes.Status401Unauthorized, "missing_api_key");
            return;
        }

        if (!string.IsNullOrWhiteSpace(_options.PlatformApiKey)
            && string.Equals(apiKey, _options.PlatformApiKey, StringComparison.Ordinal))
        {
            await HandlePlatformKeyAsync(context);
            return;
        }

        var validationResult = await _validator.ValidateAsync(apiKey, context.RequestAborted);
        if (validationResult is null)
        {
            await RejectAsync(context, StatusCodes.Status401Unauthorized, "invalid_api_key");
            return;
        }

        if (_options.AllowedKeyTypes.Count > 0
            && !_options.AllowedKeyTypes.Contains(validationResult.KeyType))
        {
            await RejectAsync(context, StatusCodes.Status403Forbidden, "api_key_scope_not_allowed");
            return;
        }

        var requestedTenant = GetHeader(context, _options.TenantHeaderName);
        if (!string.IsNullOrWhiteSpace(requestedTenant)
            && !string.Equals(requestedTenant, validationResult.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            await RejectAsync(context, StatusCodes.Status403Forbidden, "tenant_mismatch");
            return;
        }

        var previousTenant = TenantContext.TenantId;
        TenantContext.TenantId = validationResult.TenantId;
        context.Items[TenantIdMiddleware.ItemKey] = validationResult.TenantId;
        context.Items[ApiKeyAuthenticationContext.ApiKeyIdItemName] = validationResult.ApiKeyId;
        context.Items[ApiKeyAuthenticationContext.ApiKeyTypeItemName] = validationResult.KeyType;
        context.Items[ApiKeyAuthenticationContext.RoleItemName] = "api_key";
        context.Items[ApiKeyAuthenticationContext.AllowedOriginsItemName] = validationResult.AllowedOrigins;

        try
        {
            await _next(context);
        }
        finally
        {
            TenantContext.TenantId = previousTenant;
        }
    }

    private bool ShouldSkip(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (_options.AllowAnonymousPaths.Contains(path))
        {
            return true;
        }

        foreach (var prefix in _options.AllowAnonymousPathPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetHeader(HttpContext context, string headerName)
    {
        if (context.Request.Headers.TryGetValue(headerName, out var values))
        {
            var provided = values.ToString();
            return string.IsNullOrWhiteSpace(provided) ? null : provided;
        }

        return null;
    }

    private async Task RejectAsync(HttpContext context, int statusCode, string reason)
    {
        _logger.LogWarning("api_key_rejected {status_code} {reason}", statusCode, reason);
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { error = reason });
    }

    private async Task HandlePlatformKeyAsync(HttpContext context)
    {
        var requestedTenant = GetHeader(context, _options.TenantHeaderName);
        var previousTenant = TenantContext.TenantId;

        if (!string.IsNullOrWhiteSpace(requestedTenant))
        {
            TenantContext.TenantId = requestedTenant;
            context.Items[TenantIdMiddleware.ItemKey] = requestedTenant;
        }

        context.Items[ApiKeyAuthenticationContext.ApiKeyIdItemName] = "platform";
        context.Items[ApiKeyAuthenticationContext.PlatformKeyItemName] = true;
        context.Items[ApiKeyAuthenticationContext.RoleItemName] = "platform";

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
