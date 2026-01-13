using Microsoft.Extensions.Logging;
using Npgsql;

namespace Embeddra.Search.Infrastructure.Analytics;

public sealed class SearchAnalyticsWriter
{
    private readonly SearchAdminDb _db;
    private readonly ILogger<SearchAnalyticsWriter> _logger;

    public SearchAnalyticsWriter(SearchAdminDb db, ILogger<SearchAnalyticsWriter> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Guid> RecordSearchAsync(
        string tenantId,
        string query,
        int resultCount,
        int? bm25TookMs,
        int? knnTookMs,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var searchId = Guid.NewGuid();
        try
        {
            await using var connection = _db.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO search_events
                    (id, tenant_id, query, result_count, bm25_took_ms, knn_took_ms, correlation_id, created_at)
                VALUES
                    (@id, @tenant_id, @query, @result_count, @bm25_took_ms, @knn_took_ms, @correlation_id, @created_at);
                """;
            command.Parameters.AddWithValue("id", searchId);
            command.Parameters.AddWithValue("tenant_id", tenantId);
            command.Parameters.AddWithValue("query", query);
            command.Parameters.AddWithValue("result_count", resultCount);
            command.Parameters.AddWithValue("bm25_took_ms", (object?)bm25TookMs ?? DBNull.Value);
            command.Parameters.AddWithValue("knn_took_ms", (object?)knnTookMs ?? DBNull.Value);
            command.Parameters.AddWithValue("correlation_id", (object?)correlationId ?? DBNull.Value);
            command.Parameters.AddWithValue("created_at", DateTimeOffset.UtcNow);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "search_analytics_write_failed {tenant_id}", tenantId);
        }

        return searchId;
    }

    public async Task RecordClickAsync(
        string tenantId,
        Guid searchId,
        string productId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _db.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO search_clicks
                    (id, tenant_id, search_id, product_id, created_at)
                VALUES
                    (@id, @tenant_id, @search_id, @product_id, @created_at);
                """;
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("tenant_id", tenantId);
            command.Parameters.AddWithValue("search_id", searchId);
            command.Parameters.AddWithValue("product_id", productId);
            command.Parameters.AddWithValue("created_at", DateTimeOffset.UtcNow);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "search_click_write_failed {tenant_id} {search_id}", tenantId, searchId);
        }
    }
}
