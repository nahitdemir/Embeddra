using System.Text.Json;
using Embeddra.Admin.Domain;
using Embeddra.Admin.Infrastructure.Persistence;
using Embeddra.Admin.WebApi.Auth;
using Embeddra.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Embeddra.Admin.WebApi.Controllers;

[ApiController]
public sealed class SearchTuningController : ControllerBase
{
    private readonly AdminDbContext _dbContext;

    public SearchTuningController(AdminDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("search-tuning/synonyms")]
    public async Task<IActionResult> GetSynonyms(CancellationToken cancellationToken)
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

        var items = await _dbContext.SearchSynonyms.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Term)
            .Select(x => new SearchSynonymDto(
                x.Term,
                ParseJsonList(x.SynonymsJson)))
            .ToListAsync(cancellationToken);

        return Ok(new { items });
    }

    [HttpPut("search-tuning/synonyms")]
    public async Task<IActionResult> UpdateSynonyms(
        [FromBody] SearchSynonymUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!AdminAuthContext.CanTenantWrite(HttpContext))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        var tenantId = RequireTenant();
        if (tenantId is null)
        {
            return BadRequest(new { error = "tenant_required" });
        }

        var items = (request.Items ?? Array.Empty<SearchSynonymDto>())
            .Select(x => new SearchSynonymInput(NormalizeKey(x.Term), NormalizeListLower(x.Synonyms)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Term) && x.Synonyms.Count > 0)
            .ToList();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _dbContext.SearchSynonyms
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var item in items)
        {
            _dbContext.SearchSynonyms.Add(new SearchSynonym
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Term = item.Term,
                SynonymsJson = JsonSerializer.Serialize(item.Synonyms),
                CreatedAt = now
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(new { count = items.Count });
    }

    [HttpGet("search-tuning/boosts")]
    public async Task<IActionResult> GetBoosts(CancellationToken cancellationToken)
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

        var items = await _dbContext.SearchBoostRules.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Field)
            .ThenBy(x => x.Value)
            .Select(x => new SearchBoostRuleDto(x.Field, x.Value, x.Weight))
            .ToListAsync(cancellationToken);

        return Ok(new { items });
    }

    [HttpPut("search-tuning/boosts")]
    public async Task<IActionResult> UpdateBoosts(
        [FromBody] SearchBoostUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!AdminAuthContext.CanTenantWrite(HttpContext))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        var tenantId = RequireTenant();
        if (tenantId is null)
        {
            return BadRequest(new { error = "tenant_required" });
        }

        var items = (request.Items ?? Array.Empty<SearchBoostRuleDto>())
            .Select(x => new SearchBoostRuleInput(
                NormalizeKey(x.Field),
                NormalizeValue(x.Value),
                x.Weight))
            .Where(x => !string.IsNullOrWhiteSpace(x.Field) && !string.IsNullOrWhiteSpace(x.Value))
            .ToList();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _dbContext.SearchBoostRules
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var item in items)
        {
            _dbContext.SearchBoostRules.Add(new SearchBoostRule
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Field = item.Field,
                Value = item.Value,
                Weight = item.Weight <= 0 ? 1 : item.Weight,
                CreatedAt = now
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(new { count = items.Count });
    }

    [HttpGet("search-tuning/pins")]
    public async Task<IActionResult> GetPins(CancellationToken cancellationToken)
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

        var items = await _dbContext.SearchPinnedResults.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Query)
            .Select(x => new SearchPinnedResultDto(
                x.Query,
                ParseJsonList(x.ProductIdsJson)))
            .ToListAsync(cancellationToken);

        return Ok(new { items });
    }

    [HttpPut("search-tuning/pins")]
    public async Task<IActionResult> UpdatePins(
        [FromBody] SearchPinnedUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!AdminAuthContext.CanTenantWrite(HttpContext))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        var tenantId = RequireTenant();
        if (tenantId is null)
        {
            return BadRequest(new { error = "tenant_required" });
        }

        var items = (request.Items ?? Array.Empty<SearchPinnedResultDto>())
            .Select(x => new SearchPinnedInput(NormalizeKey(x.Query), NormalizeListPreserveCase(x.ProductIds)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Query) && x.ProductIds.Count > 0)
            .ToList();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _dbContext.SearchPinnedResults
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var item in items)
        {
            _dbContext.SearchPinnedResults.Add(new SearchPinnedResult
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Query = item.Query,
                ProductIdsJson = JsonSerializer.Serialize(item.ProductIds),
                CreatedAt = now
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(new { count = items.Count });
    }

    private static IReadOnlyList<string> ParseJsonList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(json);
            return parsed is null
                ? Array.Empty<string>()
                : parsed.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().ToLowerInvariant();
    }

    private static string NormalizeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim();
    }

    private static IReadOnlyList<string> NormalizeListLower(IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        return values
            .Select(NormalizeKey)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> NormalizeListPreserveCase(IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        return values
            .Select(NormalizeValue)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string? RequireTenant()
    {
        var tenantId = TenantContext.TenantId;
        return string.IsNullOrWhiteSpace(tenantId) ? null : tenantId;
    }
}

public sealed record SearchSynonymDto(string Term, IReadOnlyList<string> Synonyms);

public sealed record SearchSynonymUpdateRequest(IReadOnlyList<SearchSynonymDto>? Items);

public sealed record SearchBoostRuleDto(string Field, string Value, double Weight);

public sealed record SearchBoostUpdateRequest(IReadOnlyList<SearchBoostRuleDto>? Items);

public sealed record SearchPinnedResultDto(string Query, IReadOnlyList<string> ProductIds);

public sealed record SearchPinnedUpdateRequest(IReadOnlyList<SearchPinnedResultDto>? Items);

internal sealed record SearchSynonymInput(string Term, IReadOnlyList<string> Synonyms);

internal sealed record SearchBoostRuleInput(string Field, string Value, double Weight);

internal sealed record SearchPinnedInput(string Query, IReadOnlyList<string> ProductIds);
