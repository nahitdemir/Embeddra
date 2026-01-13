using System.Text;
using System.Text.Json;
using Embeddra.Admin.Domain;
using Embeddra.Admin.Infrastructure.Persistence;
using Embeddra.Admin.WebApi.Auth;
using Embeddra.BuildingBlocks.Tenancy;
using Embeddra.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Embeddra.Admin.WebApi.Controllers;

[ApiController]
public sealed class SearchPreviewController : ControllerBase
{
    private readonly AdminDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SearchPreviewController> _logger;

    public SearchPreviewController(
        AdminDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<SearchPreviewController> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("search/preview")]
    public async Task<IActionResult> SearchPreview([FromBody] SearchPreviewRequest request, CancellationToken cancellationToken)
    {
        if (!AdminAuthContext.CanTenantRead(HttpContext))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        var tenantId = RequireTenant();
        if (tenantId is null)
        {
            return BadRequest(new { error = "tenant_required" });
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = "query_required" });
        }

        // Get API key from database if apiKeyId is provided
        ApiKey? apiKey = null;
        if (request.ApiKeyId.HasValue)
        {
            apiKey = await _dbContext.ApiKeys.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.ApiKeyId.Value && x.TenantId == tenantId, cancellationToken);

            if (apiKey == null || apiKey.IsRevoked)
            {
                return BadRequest(new { error = "invalid_api_key" });
            }
        }
        else
        {
            // Fallback: Get first active search_public key for tenant
            apiKey = await _dbContext.ApiKeys.AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.TenantId == tenantId
                        && x.Status == ApiKeyStatus.Active
                        && (x.KeyType == ApiKeyTypes.SearchPublic || x.KeyType == null),
                    cancellationToken);

            if (apiKey == null)
            {
                return BadRequest(new { error = "no_api_key_found" });
            }
        }

        // Note: We can't retrieve the full API key (only hash is stored)
        // So we'll proxy to Search API, but Search API needs the full key
        // For preview, we'll use a different approach: make the request with tenant context
        // and let Search API handle authentication via its own middleware

        // In Docker, use service name (search-api), in local dev use localhost
        // Docker Compose sets SEARCH_API_BASE_URL=http://search-api:5222
        // Try both Configuration and Environment variable (Configuration may not always read env vars directly)
        var searchApiBaseUrl = _configuration["SEARCH_API_BASE_URL"] 
            ?? Environment.GetEnvironmentVariable("SEARCH_API_BASE_URL")
            ?? _configuration["SearchApi:BaseUrl"] 
            ?? "http://localhost:5222"; // Fallback for local development

        var searchRequest = new
        {
            query = request.Query.Trim(),
            size = request.Size ?? 12,
            brands = request.Brands,
            categories = request.Categories,
            inStock = request.InStock,
            priceMin = request.PriceMin,
            priceMax = request.PriceMax,
            knnK = request.KnnK,
            knnCandidates = request.KnnCandidates,
        };

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
            client.DefaultRequestHeaders.Add("X-Internal-Request", "admin-api"); // Signal to Search API that this is an internal request
            
            var json = JsonSerializer.Serialize(searchRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(
                $"{searchApiBaseUrl.TrimEnd('/')}/search",
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Search API error: {StatusCode} {Error}", response.StatusCode, errorText);
                return StatusCode((int)response.StatusCode, new { error = "search_api_error", message = errorText });
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // Search API returns the response directly, we just need to transform it slightly
            // to match frontend expectations (camelCase property names)
            var searchResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);

            // Transform Search API response to match frontend expectations
            var transformedResponse = new
            {
                searchId = searchResponse.TryGetProperty("searchId", out var sid) && sid.ValueKind == JsonValueKind.String ? sid.GetString() : null,
                total = searchResponse.TryGetProperty("total", out var tot) && tot.ValueKind == JsonValueKind.Number ? tot.GetInt64() : (long?)null,
                results = searchResponse.TryGetProperty("results", out var res) && res.ValueKind == JsonValueKind.Array
                    ? TransformResults(res) 
                    : Array.Empty<object>(),
                facets = searchResponse.TryGetProperty("facets", out var fac) && fac.ValueKind == JsonValueKind.Object
                    ? TransformFacets(fac) 
                    : new { brands = Array.Empty<object>(), categories = Array.Empty<object>(), priceRanges = Array.Empty<object>(), inStock = Array.Empty<object>() }
            };

            return Ok(transformedResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying search request to Search API");
            return StatusCode(500, new { error = "search_proxy_error", message = ex.Message });
        }
    }

    [HttpPost("search/preview:click")]
    public async Task<IActionResult> RegisterClick([FromBody] SearchPreviewClickRequest request, CancellationToken cancellationToken)
    {
        if (!AdminAuthContext.CanTenantRead(HttpContext))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        var tenantId = RequireTenant();
        if (tenantId is null)
        {
            return BadRequest(new { error = "tenant_required" });
        }

        if (string.IsNullOrWhiteSpace(request.SearchId) || string.IsNullOrWhiteSpace(request.ProductId))
        {
            return BadRequest(new { error = "search_id_and_product_id_required" });
        }

        // In Docker, use service name (search-api), in local dev use localhost
        // Docker Compose sets SEARCH_API_BASE_URL=http://search-api:5222
        // Try both Configuration and Environment variable (Configuration may not always read env vars directly)
        var searchApiBaseUrl = _configuration["SEARCH_API_BASE_URL"] 
            ?? Environment.GetEnvironmentVariable("SEARCH_API_BASE_URL")
            ?? _configuration["SearchApi:BaseUrl"] 
            ?? "http://localhost:5222"; // Fallback for local development

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
            client.DefaultRequestHeaders.Add("X-Internal-Request", "admin-api"); // Signal to Search API that this is an internal request

            var clickRequest = new
            {
                searchId = request.SearchId,
                productId = request.ProductId,
            };

            var json = JsonSerializer.Serialize(clickRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(
                $"{searchApiBaseUrl.TrimEnd('/')}/search:click",
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Search API click error: {StatusCode} {Error}", response.StatusCode, errorText);
                return StatusCode((int)response.StatusCode, new { error = "click_error", message = errorText });
            }

            return Accepted(new { status = "recorded" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying click to Search API");
            return StatusCode(500, new { error = "click_proxy_error", message = ex.Message });
        }
    }

    private static object[] TransformResults(JsonElement resultsElement)
    {
        if (resultsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<object>();
        }

        return resultsElement.EnumerateArray()
            .Select(hit => new
            {
                id = hit.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() ?? "" : "",
                score = hit.TryGetProperty("score", out var score) && score.ValueKind == JsonValueKind.Number ? score.GetDouble() : 0.0,
                source = hit.TryGetProperty("source", out var source) && source.ValueKind == JsonValueKind.Object
                    ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(source.GetRawText()) ?? new Dictionary<string, JsonElement>()
                    : new Dictionary<string, JsonElement>()
            })
            .ToArray();
    }

    private static object TransformFacets(JsonElement facetsElement)
    {
        var brands = facetsElement.TryGetProperty("brands", out var b) && b.ValueKind == JsonValueKind.Array
            ? b.EnumerateArray().Select(x => new { 
                key = x.TryGetProperty("key", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() ?? "" : "", 
                count = x.TryGetProperty("count", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt64() : 0L 
            }).ToArray()
            : Array.Empty<object>();

        var categories = facetsElement.TryGetProperty("categories", out var cat) && cat.ValueKind == JsonValueKind.Array
            ? cat.EnumerateArray().Select(x => new { 
                key = x.TryGetProperty("key", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() ?? "" : "", 
                count = x.TryGetProperty("count", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt64() : 0L 
            }).ToArray()
            : Array.Empty<object>();

        var priceRanges = facetsElement.TryGetProperty("priceRanges", out var pr) && pr.ValueKind == JsonValueKind.Array
            ? pr.EnumerateArray().Select(x => new { 
                key = x.TryGetProperty("key", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() ?? "" : "", 
                from = x.TryGetProperty("from", out var f) && f.ValueKind == JsonValueKind.Number ? f.GetDouble() : (double?)null, 
                to = x.TryGetProperty("to", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetDouble() : (double?)null, 
                count = x.TryGetProperty("count", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt64() : 0L 
            }).ToArray()
            : Array.Empty<object>();

        var inStock = facetsElement.TryGetProperty("inStock", out var stock) && stock.ValueKind == JsonValueKind.Array
            ? stock.EnumerateArray().Select(x => new { 
                key = x.TryGetProperty("key", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() ?? "" : "", 
                count = x.TryGetProperty("count", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt64() : 0L 
            }).ToArray()
            : Array.Empty<object>();

        return new { brands, categories, priceRanges, inStock };
    }

    private string? RequireTenant()
    {
        var tenantId = TenantContext.TenantId;
        return string.IsNullOrWhiteSpace(tenantId) ? null : tenantId;
    }
}

public sealed record SearchPreviewRequest(
    string Query,
    int? Size,
    IReadOnlyList<string>? Brands,
    IReadOnlyList<string>? Categories,
    bool? InStock,
    decimal? PriceMin,
    decimal? PriceMax,
    int? KnnK,
    int? KnnCandidates,
    Guid? ApiKeyId);

public sealed record SearchPreviewClickRequest(string SearchId, string ProductId);
