using Microsoft.AspNetCore.Http;

namespace Embeddra.BuildingBlocks.Correlation;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);

        CorrelationContext.CorrelationId = correlationId;
        context.Items[ItemKey] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        try
        {
            await _next(context);
        }
        finally
        {
            CorrelationContext.CorrelationId = null;
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            var provided = values.ToString();
            if (!string.IsNullOrWhiteSpace(provided))
            {
                return provided;
            }
        }

        return Guid.NewGuid().ToString("N");
    }
}
