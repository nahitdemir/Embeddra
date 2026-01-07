using System.Security.Cryptography;
using Embeddra.Admin.Domain;
using Embeddra.Admin.Infrastructure.Persistence;
using Embeddra.BuildingBlocks.Audit;
using Embeddra.BuildingBlocks.Authentication;
using Embeddra.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Embeddra.Admin.WebApi.Controllers;

[ApiController]
public sealed class AdminActionsController : ControllerBase
{
    private const string TenantMismatchError = "tenant_mismatch";
    private readonly AdminDbContext _dbContext;
    private readonly IAuditLogWriter _auditLogWriter;

    public AdminActionsController(AdminDbContext dbContext, IAuditLogWriter auditLogWriter)
    {
        _dbContext = dbContext;
        _auditLogWriter = auditLogWriter;
    }

    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant([FromBody] TenantRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "invalid_payload" });
        }

        var tenantId = request.TenantId.Trim();
        var name = request.Name.Trim();
        var currentTenant = TenantContext.TenantId;
        if (!string.IsNullOrWhiteSpace(currentTenant)
            && !string.Equals(currentTenant, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = TenantMismatchError });
        }

        if (await _dbContext.Tenants.AnyAsync(x => x.Id == tenantId, cancellationToken))
        {
            return Conflict(new { error = "tenant_exists" });
        }

        var tenant = new Tenant
        {
            Id = tenantId,
            Name = name,
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            new AuditLogEntry(AuditActions.TenantCreated, ResolveActor(), new { tenantId, name }),
            cancellationToken);

        return Created($"/tenants/{tenantId}", new { tenantId, name });
    }

    [HttpPost("api-keys")]
    public async Task<IActionResult> CreateApiKey([FromBody] ApiKeyRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "invalid_payload" });
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

        var (apiKey, keyHash, keyPrefix) = await GenerateUniqueApiKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "api_key_generation_failed" });
        }

        var entity = new ApiKey
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            KeyHash = keyHash,
            KeyPrefix = keyPrefix,
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.ApiKeys.Add(entity);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "api_key_conflict" });
        }

        await _auditLogWriter.WriteAsync(
            new AuditLogEntry(
                AuditActions.ApiKeyCreated,
                ResolveActor(),
                new { apiKeyId = entity.Id, tenantId, name = entity.Name, apiKey }),
            cancellationToken);

        return Created($"/api-keys/{entity.Id}", new ApiKeyCreatedResponse(entity.Id, apiKey, keyPrefix));
    }

    [HttpDelete("api-keys/{keyId}")]
    public async Task<IActionResult> RevokeApiKey(string keyId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(keyId, out var apiKeyId))
        {
            return BadRequest(new { error = "invalid_key_id" });
        }

        var tenantId = TenantContext.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return BadRequest(new { error = "tenant_required" });
        }

        var apiKey = await _dbContext.ApiKeys
            .FirstOrDefaultAsync(x => x.Id == apiKeyId && x.TenantId == tenantId, cancellationToken);

        if (apiKey is null)
        {
            return NotFound(new { error = "api_key_not_found" });
        }

        if (!string.Equals(apiKey.Status, "revoked", StringComparison.OrdinalIgnoreCase))
        {
            apiKey.Status = "revoked";
            apiKey.RevokedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
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

    private async Task<(string ApiKey, string KeyHash, string KeyPrefix)> GenerateUniqueApiKeyAsync(
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var apiKey = GenerateApiKey();
            var keyHash = ApiKeyHasher.ComputeHash(apiKey);

            var exists = await _dbContext.ApiKeys.AnyAsync(x => x.KeyHash == keyHash, cancellationToken);
            if (!exists)
            {
                var keyPrefix = ApiKeyHasher.ComputePrefix(apiKey);
                return (apiKey, keyHash, keyPrefix);
            }
        }

        return (string.Empty, string.Empty, string.Empty);
    }

    private static string GenerateApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes);
        return token.Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}

public sealed record TenantRequest(string TenantId, string Name);

public sealed record ApiKeyRequest(string Name, string? Description);

public sealed record AllowedOriginsRequest(IReadOnlyCollection<string>? Origins);

public sealed record ApiKeyCreatedResponse(Guid ApiKeyId, string ApiKey, string ApiKeyPrefix);
