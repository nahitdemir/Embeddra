using Embeddra.Admin.Domain;
using Embeddra.Admin.Infrastructure.Persistence;
using Embeddra.Admin.WebApi.Auth;
using Embeddra.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Embeddra.Admin.WebApi.Controllers;

[ApiController]
public sealed class AdminQueryController : ControllerBase
{
    private readonly AdminDbContext _dbContext;

    public AdminQueryController(AdminDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("tenants")]
    public async Task<IActionResult> GetTenants([FromQuery] string? tenantId, CancellationToken cancellationToken)
    {
        var auth = AdminAuthContext.Get(HttpContext);
        if (!auth.IsPlatform)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        if (!AdminAuthContext.CanTenantRead(HttpContext))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        var query = _dbContext.Tenants.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            query = query.Where(x => x.Id == tenantId.Trim());
        }

        var tenants = await query
            .Select(x => new TenantSummary(x.Id, x.Name, x.Status, x.CreatedAt))
            .ToListAsync(cancellationToken);
        return Ok(new { tenants });
    }

    [HttpGet("api-keys")]
    public async Task<IActionResult> GetApiKeys(CancellationToken cancellationToken)
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

        var apiKeys = await _dbContext.ApiKeys.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ApiKeySummary(
                x.Id,
                x.Name,
                x.Description,
                x.KeyType,
                x.KeyPrefix ?? string.Empty,
                x.Status,
                x.CreatedAt,
                x.RevokedAt))
            .ToListAsync(cancellationToken);

        return Ok(new { apiKeys });
    }

    [HttpGet("allowed-origins")]
    public async Task<IActionResult> GetAllowedOrigins(CancellationToken cancellationToken)
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

        var origins = await _dbContext.AllowedOrigins.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Origin)
            .Select(x => x.Origin)
            .ToListAsync(cancellationToken);

        return Ok(new { origins });
    }

    [HttpGet("ingestion-jobs")]
    public async Task<IActionResult> GetIngestionJobs(
        [FromQuery] int limit = 20,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
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

        var cappedLimit = Math.Clamp(limit, 1, 100);
        var query = _dbContext.IngestionJobs.AsNoTracking()
            .Where(x => x.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<IngestionJobStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(x => x.Status == parsedStatus);
        }

        var jobs = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(cappedLimit)
            .Select(x => new IngestionJobSummary(
                x.Id,
                x.SourceType.ToString(),
                x.Status.ToString(),
                x.TotalCount ?? 0,
                x.ProcessedCount,
                x.FailedCount,
                x.CreatedAt,
                x.StartedAt,
                x.CompletedAt,
                x.Error))
            .ToListAsync(cancellationToken);

        return Ok(new { jobs });
    }

    private string? RequireTenant()
    {
        var tenantId = TenantContext.TenantId;
        return string.IsNullOrWhiteSpace(tenantId) ? null : tenantId;
    }
}

public sealed record TenantSummary(string Id, string Name, TenantStatus Status, DateTimeOffset CreatedAt);

public sealed record ApiKeySummary(
    Guid Id,
    string Name,
    string? Description,
    string? KeyType,
    string KeyPrefix,
    ApiKeyStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt);

public sealed record IngestionJobSummary(
    Guid Id,
    string SourceType,
    string Status,
    int TotalCount,
    int ProcessedCount,
    int FailedCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Error);
