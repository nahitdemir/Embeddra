namespace Embeddra.BuildingBlocks.Authentication;

public sealed class ApiKeyAuthenticationOptions
{
    public string ApiKeyHeaderName { get; set; } = "X-Api-Key";
    public string TenantHeaderName { get; set; } = "X-Tenant-Id";
    public string? PlatformApiKey { get; set; }
    public bool AllowBearerToken { get; set; }
    public HashSet<string> AllowedKeyTypes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> AllowAnonymousPaths { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health"
    };

    public List<string> AllowAnonymousPathPrefixes { get; } = new();
}
