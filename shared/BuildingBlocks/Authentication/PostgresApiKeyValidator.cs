using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Embeddra.BuildingBlocks.Authentication;

public sealed class PostgresApiKeyValidator : IApiKeyValidator
{
    private readonly string _connectionString;

    public PostgresApiKeyValidator(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("AdminDb")
            ?? configuration.GetConnectionString("AuditDb")
            ?? "Host=localhost;Port=5433;Database=embeddra;Username=embeddra;Password=embeddra";
    }

    public async Task<ApiKeyValidationResult?> ValidateAsync(string apiKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var hash = ApiKeyHasher.ComputeHash(apiKey);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, tenant_id, status, revoked_at
            FROM api_keys
            WHERE key_hash = @hash
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("hash", hash);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var id = reader.GetGuid(0);
        var tenantId = reader.GetString(1);
        var status = reader.GetString(2);
        var revokedAt = reader.IsDBNull(3) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(3);

        if (!string.Equals(status, "active", StringComparison.OrdinalIgnoreCase) || revokedAt is not null)
        {
            return null;
        }

        return new ApiKeyValidationResult(id, tenantId);
    }
}
