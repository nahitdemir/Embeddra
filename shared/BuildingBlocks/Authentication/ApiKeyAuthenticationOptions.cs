namespace Embeddra.BuildingBlocks.Authentication;

public sealed class ApiKeyAuthenticationOptions
{
    public string ApiKeyHeaderName { get; set; } = "X-Api-Key";
    public string TenantHeaderName { get; set; } = "X-Tenant-Id";

    public HashSet<string> AllowAnonymousPaths { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health"
    };

    public List<string> AllowAnonymousPathPrefixes { get; } = new();
}
