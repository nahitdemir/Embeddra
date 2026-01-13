using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace Embeddra.Search.Infrastructure.Security;

public sealed class AllowedOriginRepository
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);
    private readonly Analytics.SearchAdminDb _db;
    private readonly IMemoryCache _cache;

    public AllowedOriginRepository(Analytics.SearchAdminDb db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<bool> IsOriginAllowedAsync(string tenantId, string origin, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        var origins = await GetAllowedOriginsAsync(tenantId, cancellationToken);
        return origins.Contains(origin.Trim());
    }

    private async Task<HashSet<string>> GetAllowedOriginsAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"allowed-origins:{tenantId}";
        if (_cache.TryGetValue(cacheKey, out HashSet<string>? cached) && cached is not null)
        {
            return cached;
        }

        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT origin
            FROM allowed_origins
            WHERE tenant_id = @tenant_id;
            """;
        command.Parameters.AddWithValue("tenant_id", tenantId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                var value = reader.GetString(0);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    results.Add(value.Trim());
                }
            }
        }

        _cache.Set(cacheKey, results, CacheDuration);
        return results;
    }
}
