using Microsoft.Extensions.Configuration;

namespace Embeddra.Search.WebApi.Security;

public sealed class SearchRateLimitOptions
{
    public bool Enabled { get; init; } = true;
    public int RequestsPerMinute { get; init; } = 120;
    public int WindowSeconds { get; init; } = 60;

    public TimeSpan Window => TimeSpan.FromSeconds(Math.Max(WindowSeconds, 1));

    public static SearchRateLimitOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("Search:RateLimit");
        var enabled = section.GetValue("Enabled", true);
        var requestsPerMinute = section.GetValue("RequestsPerMinute", 120);
        var windowSeconds = section.GetValue("WindowSeconds", 60);

        return new SearchRateLimitOptions
        {
            Enabled = enabled,
            RequestsPerMinute = Math.Max(requestsPerMinute, 1),
            WindowSeconds = Math.Max(windowSeconds, 1)
        };
    }
}
