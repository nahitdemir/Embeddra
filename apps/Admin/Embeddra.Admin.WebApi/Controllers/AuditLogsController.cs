using Embeddra.BuildingBlocks.Audit;
using Embeddra.BuildingBlocks.Tenancy;
using Embeddra.Admin.WebApi.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Embeddra.Admin.WebApi.Controllers;

[ApiController]
public sealed class AuditLogsController : ControllerBase
{
    private readonly AuditDbContext _auditDbContext;

    public AuditLogsController(AuditDbContext auditDbContext)
    {
        _auditDbContext = auditDbContext;
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? tenantId,
        [FromQuery] string? action,
        [FromQuery] string? actor,
        [FromQuery] string? correlationId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
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

        var resolvedTenant = ResolveTenant(true, tenantId);

        var query = _auditDbContext.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(resolvedTenant))
        {
            query = query.Where(x => x.TenantId == resolvedTenant);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var normalized = action.Trim().ToLowerInvariant();
            query = query.Where(x => x.Action.ToLower().Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(actor))
        {
            var normalized = actor.Trim().ToLowerInvariant();
            query = query.Where(x => x.Actor != null && x.Actor.ToLower().Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            var normalized = correlationId.Trim();
            query = query.Where(x => x.CorrelationId != null && x.CorrelationId.Contains(normalized));
        }

        if (from.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= to.Value);
        }

        var cappedLimit = Math.Clamp(limit, 1, 200);
        var logs = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(cappedLimit)
            .Select(x => new AuditLogSummary(
                x.Id,
                x.TenantId,
                x.Action,
                x.Actor,
                x.CorrelationId,
                x.PayloadJson,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(new { logs });
    }

    private static string? ResolveTenant(bool isPlatform, string? requestedTenant)
    {
        if (isPlatform)
        {
            return string.IsNullOrWhiteSpace(requestedTenant) ? null : requestedTenant.Trim();
        }

        var tenantId = TenantContext.TenantId;
        return string.IsNullOrWhiteSpace(tenantId) ? null : tenantId;
    }
}

public sealed record AuditLogSummary(
    Guid Id,
    string? TenantId,
    string Action,
    string? Actor,
    string? CorrelationId,
    string PayloadJson,
    DateTimeOffset CreatedAt);
