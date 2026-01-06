using Microsoft.AspNetCore.Http;

namespace Embeddra.BuildingBlocks.Logging;

public interface IRequestResponseLoggingPolicy
{
    bool ShouldLogBody(HttpContext context, int statusCode);
    bool ShouldSummarizeRequestBody(HttpContext context);
    bool ShouldCaptureSearchQuery(HttpContext context);
    int SearchQueryMaxLength { get; }
}

public sealed class AdminRequestResponseLoggingPolicy : IRequestResponseLoggingPolicy
{
    private static readonly HashSet<string> SummaryPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/products:bulk",
        "/products:importCsv"
    };

    public bool ShouldLogBody(HttpContext context, int statusCode) => true;

    public bool ShouldSummarizeRequestBody(HttpContext context)
    {
        return SummaryPaths.Contains(context.Request.Path.Value ?? string.Empty);
    }

    public bool ShouldCaptureSearchQuery(HttpContext context) => false;

    public int SearchQueryMaxLength => 0;
}

public sealed class SearchRequestResponseLoggingPolicy : IRequestResponseLoggingPolicy
{
    private const double SuccessSampleRate = 0.10;

    public bool ShouldLogBody(HttpContext context, int statusCode)
    {
        if (!IsSearchPath(context))
        {
            return true;
        }

        if (statusCode >= 400)
        {
            return true;
        }

        return Random.Shared.NextDouble() < SuccessSampleRate;
    }

    public bool ShouldSummarizeRequestBody(HttpContext context) => false;

    public bool ShouldCaptureSearchQuery(HttpContext context) => IsSearchPath(context);

    public int SearchQueryMaxLength => 200;

    private static bool IsSearchPath(HttpContext context)
    {
        return string.Equals(context.Request.Path.Value, "/search", StringComparison.OrdinalIgnoreCase);
    }
}
