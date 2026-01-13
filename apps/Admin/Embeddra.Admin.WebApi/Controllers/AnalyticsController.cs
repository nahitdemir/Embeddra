using System.Text.Json.Serialization;
using Embeddra.Admin.Application.Settings;
using Embeddra.Admin.Application.Services;
using Embeddra.Admin.Infrastructure.Persistence;
using Embeddra.Admin.WebApi.Auth;
using Embeddra.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Embeddra.Admin.WebApi.Controllers;

[ApiController]
public sealed class AnalyticsController : ControllerBase
{
    private readonly AdminDbContext _dbContext;
    private readonly ITenantSettingsService _settingsService;

    public AnalyticsController(AdminDbContext dbContext, ITenantSettingsService settingsService)
    {
        _dbContext = dbContext;
        _settingsService = settingsService;
    }

    [HttpGet("analytics/summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] string? tenantId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        if (!AdminAuthContext.CanTenantRead(HttpContext))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        var resolvedTenant = ResolveTenant(AdminAuthContext.Get(HttpContext).IsPlatform, tenantId);
        if (string.IsNullOrWhiteSpace(resolvedTenant))
        {
            return BadRequest(new { error = "tenant_required" });
        }

        var eventsQuery = _dbContext.SearchEvents.AsNoTracking()
            .Where(x => x.TenantId == resolvedTenant);

        if (from.HasValue)
        {
            eventsQuery = eventsQuery.Where(x => x.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            eventsQuery = eventsQuery.Where(x => x.CreatedAt <= to.Value);
        }

        var totalSearches = await eventsQuery.CountAsync(cancellationToken);
        var noResultCount = await eventsQuery
            .Where(x => x.ResultCount == 0)
            .CountAsync(cancellationToken);

        var clicksQuery = _dbContext.SearchClicks.AsNoTracking()
            .Where(x => x.TenantId == resolvedTenant);

        if (from.HasValue)
        {
            clicksQuery = clicksQuery.Where(x => x.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            clicksQuery = clicksQuery.Where(x => x.CreatedAt <= to.Value);
        }

        var clickedSearches = await clicksQuery
            .Select(x => x.SearchId)
            .Distinct()
            .CountAsync(cancellationToken);

        var clickCount = await clicksQuery.CountAsync(cancellationToken);

        var noResultRate = totalSearches == 0
            ? 0
            : (double)noResultCount / totalSearches;
        var clickThroughRate = totalSearches == 0
            ? 0
            : (double)clickedSearches / totalSearches;

        return Ok(new
        {
            total_searches = totalSearches,
            no_result_count = noResultCount,
            no_result_rate = noResultRate,
            click_count = clickCount,
            click_through_rate = clickThroughRate
        });
    }

    [HttpGet("analytics/top-queries")]
    public async Task<IActionResult> GetTopQueries(
        [FromQuery] string? tenantId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (!AdminAuthContext.CanTenantRead(HttpContext))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        var resolvedTenant = ResolveTenant(AdminAuthContext.Get(HttpContext).IsPlatform, tenantId);
        if (string.IsNullOrWhiteSpace(resolvedTenant))
        {
            return BadRequest(new { error = "tenant_required" });
        }

        // Default limit'i settings'ten al, yoksa 10 kullan
        var defaultLimit = await _settingsService.GetSettingIntAsync(
            resolvedTenant,
            SettingKeys.SearchDefaultLimit,
            defaultValue: 10,
            cancellationToken) ?? 10;

        // Request'ten gelen limit varsa onu kullan, yoksa default'u kullan
        var effectiveLimit = limit > 0 ? limit : defaultLimit;
        var maxLimit = await _settingsService.GetSettingIntAsync(
            resolvedTenant,
            SettingKeys.SearchMaxResults,
            defaultValue: 50,
            cancellationToken) ?? 50;

        var cappedLimit = Math.Clamp(effectiveLimit, 1, maxLimit);
        var query = _dbContext.SearchEvents.AsNoTracking()
            .Where(x => x.TenantId == resolvedTenant && !string.IsNullOrWhiteSpace(x.Query));

        if (from.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= to.Value);
        }

        var results = await query
            .GroupBy(x => x.Query)
            .Select(group => new TopQuerySummary(
                group.Key ?? string.Empty,
                group.Count(),
                group.Count(x => x.ResultCount == 0)))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Query)
            .Take(cappedLimit)
            .ToListAsync(cancellationToken);

        return Ok(new { queries = results });
    }

    private string? ResolveTenant(bool isPlatform, string? requestedTenant)
    {
        if (isPlatform)
        {
            return string.IsNullOrWhiteSpace(requestedTenant) ? null : requestedTenant.Trim();
        }

        var tenantId = TenantContext.TenantId;
        return string.IsNullOrWhiteSpace(tenantId) ? null : tenantId;
    }
}

public sealed record TopQuerySummary(
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("noResultCount")] int NoResultCount);
