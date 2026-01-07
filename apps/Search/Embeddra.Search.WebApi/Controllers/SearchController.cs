using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Embeddra.BuildingBlocks.Tenancy;
using Embeddra.Search.Application.Embedding;
using Microsoft.AspNetCore.Mvc;

namespace Embeddra.Search.WebApi.Controllers;

[ApiController]
public sealed class SearchController : ControllerBase
{
    private const string IndexPrefix = "products";
    private const int DefaultSize = 10;
    private const int MaxSize = 50;
    private const int DefaultFacetSize = 10;
    private const int DefaultKnnMultiplier = 3;
    private const int MaxKnnK = 200;
    private const int MaxKnnCandidates = 1000;
    private const int RrfConstant = 60;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEmbeddingClient _embeddingClient;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        IHttpClientFactory httpClientFactory,
        IEmbeddingClient embeddingClient,
        ILogger<SearchController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _embeddingClient = embeddingClient;
        _logger = logger;
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] SearchRequest request, CancellationToken cancellationToken)
    {
        var tenantId = TenantContext.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return BadRequest(new { error = "tenant_required" });
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = "query_required" });
        }

        var indexName = ResolveIndexName(tenantId);
        var size = NormalizeSize(request.Size);
        var bm25Size = Math.Min(Math.Max(size * 2, size), MaxKnnK);
        var knnK = NormalizeKnnK(request.KnnK, size);
        var knnCandidates = NormalizeKnnCandidates(request.KnnCandidates, knnK);
        var filters = BuildFilters(tenantId, request);

        var stopwatch = Stopwatch.StartNew();
        var bm25Task = ExecuteBm25SearchAsync(indexName, request.Query!, bm25Size, filters, cancellationToken);

        var embeddings = await _embeddingClient.EmbedAsync(new[] { request.Query! }, cancellationToken);
        if (embeddings.Count == 0)
        {
            return StatusCode(500, new { error = "embedding_failed" });
        }

        var knnTask = ExecuteKnnSearchAsync(indexName, embeddings[0], knnK, knnCandidates, filters, cancellationToken);
        await Task.WhenAll(bm25Task, knnTask);

        var bm25Result = await bm25Task;
        var knnResult = await knnTask;

        var mergedHits = MergeHits(bm25Result.Hits, knnResult.Hits, size);
        stopwatch.Stop();

        var total = bm25Result.Total ?? mergedHits.Count;
        var noResult = mergedHits.Count == 0;

        _logger.LogInformation(
            "search_metrics {duration_ms} {bm25_took_ms} {knn_took_ms} {result_count} {no_result} {index_name}",
            stopwatch.ElapsedMilliseconds,
            bm25Result.TookMs,
            knnResult.TookMs,
            mergedHits.Count,
            noResult,
            indexName);

        var response = new SearchResponse(
            bm25Result.TookMs,
            knnResult.TookMs,
            total,
            mergedHits,
            bm25Result.Facets);

        return Ok(response);
    }

    private async Task<ElasticsearchSearchResult> ExecuteBm25SearchAsync(
        string indexName,
        string query,
        int size,
        IReadOnlyList<object> filters,
        CancellationToken cancellationToken)
    {
        var payload = BuildBm25Query(query, size, filters, DefaultFacetSize);
        var body = await SendSearchAsync(indexName, payload, cancellationToken);
        return ParseBm25Response(body);
    }

    private async Task<ElasticsearchKnnResult> ExecuteKnnSearchAsync(
        string indexName,
        float[] vector,
        int k,
        int candidates,
        IReadOnlyList<object> filters,
        CancellationToken cancellationToken)
    {
        var payload = BuildKnnQuery(vector, k, candidates, filters);
        var body = await SendSearchAsync(indexName, payload, cancellationToken);
        return ParseKnnResponse(body);
    }

    private async Task<string> SendSearchAsync(
        string indexName,
        object payload,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("elasticsearch");
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"/{indexName}/_search", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Elasticsearch search failed {(int)response.StatusCode}: {body}");
        }

        return body;
    }

    private static object BuildBm25Query(
        string query,
        int size,
        IReadOnlyList<object> filters,
        int facetSize)
    {
        return new Dictionary<string, object?>
        {
            ["track_total_hits"] = true,
            ["size"] = size,
            ["query"] = new Dictionary<string, object?>
            {
                ["bool"] = new Dictionary<string, object?>
                {
                    ["must"] = new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["query_string"] = new Dictionary<string, object?>
                            {
                                ["query"] = query
                            }
                        }
                    },
                    ["filter"] = filters
                }
            },
            ["aggs"] = BuildAggregations(facetSize),
            ["_source"] = true
        };
    }

    private static object BuildKnnQuery(
        float[] vector,
        int k,
        int candidates,
        IReadOnlyList<object> filters)
    {
        return new Dictionary<string, object?>
        {
            ["size"] = k,
            ["knn"] = new Dictionary<string, object?>
            {
                ["field"] = "embedding",
                ["query_vector"] = vector,
                ["k"] = k,
                ["num_candidates"] = candidates,
                ["filter"] = new Dictionary<string, object?>
                {
                    ["bool"] = new Dictionary<string, object?>
                    {
                        ["filter"] = filters
                    }
                }
            },
            ["_source"] = true
        };
    }

    private static object BuildAggregations(int facetSize)
    {
        return new Dictionary<string, object?>
        {
            ["brand"] = new Dictionary<string, object?>
            {
                ["terms"] = new Dictionary<string, object?>
                {
                    ["field"] = "brand",
                    ["size"] = facetSize
                }
            },
            ["category"] = new Dictionary<string, object?>
            {
                ["terms"] = new Dictionary<string, object?>
                {
                    ["field"] = "category",
                    ["size"] = facetSize
                }
            },
            ["in_stock"] = new Dictionary<string, object?>
            {
                ["terms"] = new Dictionary<string, object?>
                {
                    ["field"] = "in_stock"
                }
            },
            ["price_ranges"] = new Dictionary<string, object?>
            {
                ["range"] = new Dictionary<string, object?>
                {
                    ["field"] = "price",
                    ["ranges"] = new object[]
                    {
                        new Dictionary<string, object?> { ["to"] = 50, ["key"] = "0-50" },
                        new Dictionary<string, object?> { ["from"] = 50, ["to"] = 100, ["key"] = "50-100" },
                        new Dictionary<string, object?> { ["from"] = 100, ["to"] = 250, ["key"] = "100-250" },
                        new Dictionary<string, object?> { ["from"] = 250, ["to"] = 500, ["key"] = "250-500" },
                        new Dictionary<string, object?> { ["from"] = 500, ["to"] = 1000, ["key"] = "500-1000" },
                        new Dictionary<string, object?> { ["from"] = 1000, ["key"] = "1000+" }
                    }
                }
            }
        };
    }

    private static List<SearchHit> MergeHits(
        IReadOnlyList<ElasticsearchHit> bm25Hits,
        IReadOnlyList<ElasticsearchHit> knnHits,
        int size)
    {
        var scores = new Dictionary<string, SearchHitBuilder>(StringComparer.Ordinal);
        AddRrfScores(scores, bm25Hits);
        AddRrfScores(scores, knnHits);

        return scores.Values
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .Take(size)
            .Select(x => new SearchHit(x.Id, x.Score, x.Source))
            .ToList();
    }

    private static void AddRrfScores(
        Dictionary<string, SearchHitBuilder> scores,
        IReadOnlyList<ElasticsearchHit> hits)
    {
        for (var i = 0; i < hits.Count; i++)
        {
            var rank = i + 1;
            var rrfScore = 1.0 / (RrfConstant + rank);
            var hit = hits[i];

            if (!scores.TryGetValue(hit.Id, out var builder))
            {
                builder = new SearchHitBuilder(hit.Id);
                scores[hit.Id] = builder;
            }

            builder.Score += rrfScore;
            builder.SetSourceIfMissing(hit.Source);
        }
    }

    private static ElasticsearchSearchResult ParseBm25Response(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        var hits = ParseHits(root);
        var facets = ParseFacets(root);
        var took = TryGetTook(root);
        var total = TryGetTotal(root);

        return new ElasticsearchSearchResult(took, total, hits, facets);
    }

    private static ElasticsearchKnnResult ParseKnnResponse(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        var hits = ParseHits(root);
        var took = TryGetTook(root);

        return new ElasticsearchKnnResult(took, hits);
    }

    private static List<ElasticsearchHit> ParseHits(JsonElement root)
    {
        var results = new List<ElasticsearchHit>();

        if (!root.TryGetProperty("hits", out var hitsElement))
        {
            return results;
        }

        if (!hitsElement.TryGetProperty("hits", out var hitsArray)
            || hitsArray.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var hit in hitsArray.EnumerateArray())
        {
            if (!hit.TryGetProperty("_id", out var idElement)
                || idElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var id = idElement.GetString();
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var source = ExtractSource(hit, id);
            results.Add(new ElasticsearchHit(id, source));
        }

        return results;
    }

    private static JsonElement ExtractSource(JsonElement hit, string id)
    {
        if (hit.TryGetProperty("_source", out var source)
            && source.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            return source.Clone();
        }

        return JsonSerializer.SerializeToElement(new Dictionary<string, object?> { ["product_id"] = id });
    }

    private static SearchFacets ParseFacets(JsonElement root)
    {
        if (!root.TryGetProperty("aggregations", out var aggsElement)
            || aggsElement.ValueKind != JsonValueKind.Object)
        {
            return SearchFacets.Empty;
        }

        return new SearchFacets(
            ParseTermsAgg(aggsElement, "brand"),
            ParseTermsAgg(aggsElement, "category"),
            ParseRangeAgg(aggsElement, "price_ranges"),
            ParseTermsAgg(aggsElement, "in_stock"));
    }

    private static List<FacetBucket> ParseTermsAgg(JsonElement aggsElement, string name)
    {
        var results = new List<FacetBucket>();
        if (!aggsElement.TryGetProperty(name, out var aggElement))
        {
            return results;
        }

        if (!aggElement.TryGetProperty("buckets", out var bucketsElement)
            || bucketsElement.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var bucket in bucketsElement.EnumerateArray())
        {
            if (!bucket.TryGetProperty("key", out var keyElement))
            {
                continue;
            }

            var key = keyElement.ValueKind == JsonValueKind.String
                ? keyElement.GetString()
                : keyElement.ToString();

            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var count = bucket.TryGetProperty("doc_count", out var countElement)
                && countElement.ValueKind == JsonValueKind.Number
                    ? countElement.GetInt64()
                    : 0;

            results.Add(new FacetBucket(key, count));
        }

        return results;
    }

    private static List<RangeFacetBucket> ParseRangeAgg(JsonElement aggsElement, string name)
    {
        var results = new List<RangeFacetBucket>();
        if (!aggsElement.TryGetProperty(name, out var aggElement))
        {
            return results;
        }

        if (!aggElement.TryGetProperty("buckets", out var bucketsElement)
            || bucketsElement.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var bucket in bucketsElement.EnumerateArray())
        {
            var key = bucket.TryGetProperty("key", out var keyElement)
                ? keyElement.GetString()
                : null;

            var from = bucket.TryGetProperty("from", out var fromElement)
                && fromElement.ValueKind == JsonValueKind.Number
                    ? fromElement.GetDouble()
                    : (double?)null;

            var to = bucket.TryGetProperty("to", out var toElement)
                && toElement.ValueKind == JsonValueKind.Number
                    ? toElement.GetDouble()
                    : (double?)null;

            var count = bucket.TryGetProperty("doc_count", out var countElement)
                && countElement.ValueKind == JsonValueKind.Number
                    ? countElement.GetInt64()
                    : 0;

            var label = string.IsNullOrWhiteSpace(key)
                ? BuildRangeKey(from, to)
                : key;

            results.Add(new RangeFacetBucket(label, from, to, count));
        }

        return results;
    }

    private static string BuildRangeKey(double? from, double? to)
    {
        if (from.HasValue && to.HasValue)
        {
            return $"{from:0.##}-{to:0.##}";
        }

        if (from.HasValue)
        {
            return $"{from:0.##}+";
        }

        if (to.HasValue)
        {
            return $"0-{to:0.##}";
        }

        return "unknown";
    }

    private static long? TryGetTook(JsonElement root)
    {
        if (root.TryGetProperty("took", out var tookElement)
            && tookElement.ValueKind == JsonValueKind.Number)
        {
            return tookElement.GetInt64();
        }

        return null;
    }

    private static long? TryGetTotal(JsonElement root)
    {
        if (root.TryGetProperty("hits", out var hitsElement)
            && hitsElement.TryGetProperty("total", out var totalElement))
        {
            if (totalElement.ValueKind == JsonValueKind.Object
                && totalElement.TryGetProperty("value", out var valueElement)
                && valueElement.ValueKind == JsonValueKind.Number)
            {
                return valueElement.GetInt64();
            }

            if (totalElement.ValueKind == JsonValueKind.Number)
            {
                return totalElement.GetInt64();
            }
        }

        return null;
    }

    private static int NormalizeSize(int? size)
    {
        var value = size ?? DefaultSize;
        if (value <= 0)
        {
            value = DefaultSize;
        }

        return Math.Min(value, MaxSize);
    }

    private static int NormalizeKnnK(int? knnK, int size)
    {
        var value = knnK ?? Math.Max(size * DefaultKnnMultiplier, size);
        if (value <= 0)
        {
            value = Math.Max(size * DefaultKnnMultiplier, size);
        }

        return Math.Min(value, MaxKnnK);
    }

    private static int NormalizeKnnCandidates(int? candidates, int knnK)
    {
        var value = candidates ?? Math.Max(knnK * 2, knnK);
        if (value <= 0)
        {
            value = Math.Max(knnK * 2, knnK);
        }

        if (value < knnK)
        {
            value = knnK;
        }

        return Math.Min(value, MaxKnnCandidates);
    }

    private static List<object> BuildFilters(string tenantId, SearchRequest request)
    {
        var filters = new List<object>
        {
            new Dictionary<string, object?>
            {
                ["term"] = new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantId
                }
            }
        };

        if (request.Brands is { Count: > 0 })
        {
            filters.Add(new Dictionary<string, object?>
            {
                ["terms"] = new Dictionary<string, object?>
                {
                    ["brand"] = request.Brands
                }
            });
        }

        if (request.Categories is { Count: > 0 })
        {
            filters.Add(new Dictionary<string, object?>
            {
                ["terms"] = new Dictionary<string, object?>
                {
                    ["category"] = request.Categories
                }
            });
        }

        if (request.InStock.HasValue)
        {
            filters.Add(new Dictionary<string, object?>
            {
                ["term"] = new Dictionary<string, object?>
                {
                    ["in_stock"] = request.InStock.Value
                }
            });
        }

        if (request.PriceMin.HasValue || request.PriceMax.HasValue)
        {
            var range = new Dictionary<string, object?>();
            if (request.PriceMin.HasValue)
            {
                range["gte"] = request.PriceMin.Value;
            }

            if (request.PriceMax.HasValue)
            {
                range["lte"] = request.PriceMax.Value;
            }

            filters.Add(new Dictionary<string, object?>
            {
                ["range"] = new Dictionary<string, object?>
                {
                    ["price"] = range
                }
            });
        }

        return filters;
    }

    private static string ResolveIndexName(string tenantId)
    {
        var normalized = NormalizeIndexSegment(tenantId);
        return string.IsNullOrWhiteSpace(normalized)
            ? $"{IndexPrefix}-default"
            : $"{IndexPrefix}-{normalized}";
    }

    private static string NormalizeIndexSegment(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var trimmed = input.Trim().ToLowerInvariant();
        var builder = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (ch is >= 'a' and <= 'z' || ch is >= '0' and <= '9' || ch == '-' || ch == '_')
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append('-');
            }
        }

        var sanitized = builder.ToString().Trim('-', '_');
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return string.Empty;
        }

        return sanitized.Length > 60 ? sanitized[..60] : sanitized;
    }

    private sealed record ElasticsearchSearchResult(
        long? TookMs,
        long? Total,
        IReadOnlyList<ElasticsearchHit> Hits,
        SearchFacets Facets);

    private sealed record ElasticsearchKnnResult(long? TookMs, IReadOnlyList<ElasticsearchHit> Hits);

    private sealed record ElasticsearchHit(string Id, JsonElement Source);

    private sealed class SearchHitBuilder
    {
        private bool _hasSource;

        public SearchHitBuilder(string id)
        {
            Id = id;
        }

        public string Id { get; }

        public double Score { get; set; }

        public JsonElement Source { get; private set; }

        public void SetSourceIfMissing(JsonElement source)
        {
            if (_hasSource)
            {
                return;
            }

            Source = source;
            _hasSource = true;
        }
    }
}

public sealed record SearchRequest(
    string? Query,
    int? Size,
    IReadOnlyList<string>? Brands,
    IReadOnlyList<string>? Categories,
    bool? InStock,
    decimal? PriceMin,
    decimal? PriceMax,
    int? KnnK,
    int? KnnCandidates);

public sealed record SearchResponse(
    long? TookMs,
    long? KnnTookMs,
    long? Total,
    IReadOnlyList<SearchHit> Results,
    SearchFacets Facets);

public sealed record SearchHit(string Id, double Score, JsonElement Source);

public sealed record SearchFacets(
    IReadOnlyList<FacetBucket> Brands,
    IReadOnlyList<FacetBucket> Categories,
    IReadOnlyList<RangeFacetBucket> PriceRanges,
    IReadOnlyList<FacetBucket> InStock)
{
    public static SearchFacets Empty { get; } = new(
        Array.Empty<FacetBucket>(),
        Array.Empty<FacetBucket>(),
        Array.Empty<RangeFacetBucket>(),
        Array.Empty<FacetBucket>());
}

public sealed record FacetBucket(string Key, long Count);

public sealed record RangeFacetBucket(string Key, double? From, double? To, long Count);
