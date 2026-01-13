using System.Text.Json;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Embeddra.Search.Infrastructure.Tuning;

public sealed class SearchTuningRepository
{
    private readonly Analytics.SearchAdminDb _db;
    private readonly ILogger<SearchTuningRepository> _logger;

    public SearchTuningRepository(Analytics.SearchAdminDb db, ILogger<SearchTuningRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SearchTuningConfig> GetTuningAsync(string tenantId, CancellationToken cancellationToken)
    {
        var synonyms = new List<SearchSynonym>();
        var boosts = new List<SearchBoostRule>();
        var pins = new List<SearchPinnedResult>();

        try
        {
            await using var connection = _db.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT term, synonyms_json
                    FROM search_synonyms
                    WHERE tenant_id = @tenant_id;
                    """;
                command.Parameters.AddWithValue("tenant_id", tenantId);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var term = reader.GetString(0);
                    var json = reader.GetString(1);
                    var list = ParseJsonList(json);
                    if (list.Count > 0)
                    {
                        synonyms.Add(new SearchSynonym(term, list));
                    }
                }
            }

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT field, value, weight
                    FROM search_boosts
                    WHERE tenant_id = @tenant_id;
                    """;
                command.Parameters.AddWithValue("tenant_id", tenantId);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var field = reader.GetString(0);
                    var value = reader.GetString(1);
                    var weight = reader.GetDouble(2);
                    boosts.Add(new SearchBoostRule(field, value, weight));
                }
            }

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT query, product_ids_json
                    FROM search_pins
                    WHERE tenant_id = @tenant_id;
                    """;
                command.Parameters.AddWithValue("tenant_id", tenantId);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var query = reader.GetString(0);
                    var json = reader.GetString(1);
                    var list = ParseJsonList(json);
                    if (list.Count > 0)
                    {
                        pins.Add(new SearchPinnedResult(query, list));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "search_tuning_load_failed {tenant_id}", tenantId);
        }

        return new SearchTuningConfig(synonyms, boosts, pins);
    }

    private static List<string> ParseJsonList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(json);
            return parsed?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }
}

public sealed record SearchTuningConfig(
    IReadOnlyList<SearchSynonym> Synonyms,
    IReadOnlyList<SearchBoostRule> Boosts,
    IReadOnlyList<SearchPinnedResult> Pins);

public sealed record SearchSynonym(string Term, IReadOnlyList<string> Synonyms);

public sealed record SearchBoostRule(string Field, string Value, double Weight);

public sealed record SearchPinnedResult(string Query, IReadOnlyList<string> ProductIds);
