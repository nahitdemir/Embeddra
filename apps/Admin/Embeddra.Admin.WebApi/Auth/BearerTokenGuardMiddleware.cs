using Microsoft.AspNetCore.Http;

namespace Embeddra.Admin.WebApi.Auth;

public sealed class BearerTokenGuardMiddleware
{
    private readonly RequestDelegate _next;

    public BearerTokenGuardMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        if (HasBearerToken(context) && context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "invalid_token" });
            return;
        }

        await _next(context);
    }

    private static bool HasBearerToken(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("Authorization", out var values))
        {
            return false;
        }

        var header = values.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
    }
}
