using Embeddra.Admin.Domain;
using Embeddra.Admin.Infrastructure.Persistence;
using Embeddra.Admin.WebApi.Auth;
using Embeddra.Admin.Application.Services;
using Embeddra.BuildingBlocks.Audit;
using Embeddra.Contracts;
using Embeddra.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Embeddra.Admin.WebApi.Controllers;

[ApiController]
public sealed class AdminActionsController : ControllerBase
{
    private readonly AdminDbContext _dbContext;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ITenantService _tenantService;
    private readonly IApiKeyService _apiKeyService;

    public AdminActionsController(
        AdminDbContext dbContext,
        IAuditLogWriter auditLogWriter,
        ITenantService tenantService,
        IApiKeyService apiKeyService)
    {
        _dbContext = dbContext;
        _auditLogWriter = auditLogWriter;
        _tenantService = tenantService;
        _apiKeyService = apiKeyService;
    }

    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant([FromBody] TenantRequest request, CancellationToken cancellationToken)
    {
        if (!AdminAuthContext.Get(HttpContext).IsPlatform)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        if (string.IsNullOrWhiteSpace(request.TenantId) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "invalid_payload" });
        }

        var tenantId = request.TenantId.Trim();
        var name = request.Name.Trim();

        var createRequest = new CreateTenantRequest(tenantId, name);
        var result = await _tenantService.CreateTenantAsync(createRequest, cancellationToken);
        if (!result.Success)
        {
             return Conflict(new { error = "tenant_exists" });
        }

        await _auditLogWriter.WriteAsync(
            new AuditLogEntry(AuditActions.TenantCreated, ResolveActor(), new { tenantId, name }),
            cancellationToken);

        return Created($"/tenants/{tenantId}", new { tenantId, name });
    }

    [HttpPost("api-keys")]
    public async Task<IActionResult> CreateApiKey([FromBody] ApiKeyRequest request, CancellationToken cancellationToken)
    {
        if (!AdminAuthContext.CanTenantWrite(HttpContext))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "invalid_payload" });
        }

        // Validate KeyType (Controller responsibility)
        if (string.IsNullOrWhiteSpace(request.Type)) return BadRequest(new { error = "invalid_key_type" });
        
        var keyType = ApiKeyTypes.Normalize(request.Type);
        if (!string.Equals(keyType, ApiKeyTypes.SearchPublic, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "invalid_key_type" });
        }

        var tenantId = TenantContext.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return BadRequest(new { error = "tenant_required" });
        }

        if (!await _tenantService.ExistsAsync(tenantId, cancellationToken))
        {
            return NotFound(new { error = "tenant_not_found" });
        }

        var createRequest = new CreateApiKeyRequest(
            tenantId, 
            request.Name, 
            request.Description, 
            keyType, 
            request.AllowedOrigins);

        var result = await _apiKeyService.CreateApiKeyAsync(createRequest, cancellationToken);

        if (!result.Success)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = result.Error ?? "api_key_generation_failed" });
        }

        await _auditLogWriter.WriteAsync(
            new AuditLogEntry(
                AuditActions.ApiKeyCreated,
                ResolveActor(),
                new { apiKeyId = result.ApiKeyId, tenantId, name = request.Name, api_key_prefix = result.KeyPrefix }),
            cancellationToken);

        return Created(
            $"/api-keys/{result.ApiKeyId}",
            new ApiKeyCreatedResponse(result.ApiKeyId!.Value, result.PlainTextKey!, result.KeyPrefix!, keyType));
    }

    [HttpDelete("api-keys/{keyId}")]
    public async Task<IActionResult> RevokeApiKey(string keyId, CancellationToken cancellationToken)
    {
        if (!AdminAuthContext.CanTenantWrite(HttpContext))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        if (!Guid.TryParse(keyId, out var apiKeyId))
        {
            return BadRequest(new { error = "invalid_key_id" });
        }

        var tenantId = TenantContext.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return BadRequest(new { error = "tenant_required" });
        }
        
        // Ensure ApiKey exists and belongs to tenant (Service handles update, but GetByKey is needed if we want to confirm existence/ownership first OR service handles it and returns success/fail).
        // My ApiKeyService.RevokeApiKeyAsync method:
        // public async Task<bool> RevokeApiKeyAsync(Guid id, string tenantId, CancellationToken cancellationToken)
        // It fetches, checks tenant, revokes, saves. Returns true if revoked, false if not found.
        
        var success = await _apiKeyService.RevokeApiKeyAsync(tenantId, apiKeyId, cancellationToken);
        if (!success)
        {
            // Could be not found or already revoked (actually service logic: if null return false. if found but revoked, nothing happens and returns true/false? let's assume false means not found/mismatch).
            // Let's assume it means Not Found for now.
            return NotFound(new { error = "api_key_not_found" });
        }

        await _auditLogWriter.WriteAsync(
            new AuditLogEntry(
                AuditActions.ApiKeyRevoked,
                ResolveActor(),
                new { apiKeyId, tenantId }),
            cancellationToken);

        return Ok(new { status = "revoked" });
    }

    [HttpPut("allowed-origins")]
    public async Task<IActionResult> UpdateAllowedOrigins([FromBody] AllowedOriginsRequest request, CancellationToken cancellationToken)
    {
        if (!AdminAuthContext.CanTenantWrite(HttpContext))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        var tenantId = TenantContext.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return BadRequest(new { error = "tenant_required" });
        }

        var tenantExists = await _dbContext.Tenants.AnyAsync(x => x.Id == tenantId, cancellationToken);
        if (!tenantExists)
        {
            return NotFound(new { error = "tenant_not_found" });
        }

        var origins = (request.Origins ?? Array.Empty<string>())
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _dbContext.AllowedOrigins
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var origin in origins)
        {
            _dbContext.AllowedOrigins.Add(new AllowedOrigin
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Origin = origin,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            new AuditLogEntry(
                AuditActions.AllowedOriginsUpdated,
                ResolveActor(),
                new { tenantId, origins }),
            cancellationToken);

        return Ok(new { count = origins.Count });
    }

    private string ResolveActor()
    {
        if (User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity.Name))
        {
            return User.Identity.Name;
        }

        var headerActor = Request.Headers["X-Actor"].ToString();
        return string.IsNullOrWhiteSpace(headerActor) ? "system" : headerActor;
    }


}

public sealed record TenantRequest(string TenantId, string Name);

public sealed record ApiKeyRequest(string Name, string? Description, string? Type, string[]? AllowedOrigins);

public sealed record AllowedOriginsRequest(IReadOnlyCollection<string>? Origins);

public sealed record ApiKeyCreatedResponse(Guid ApiKeyId, string ApiKey, string ApiKeyPrefix, string? KeyType = null);
