using Embeddra.BuildingBlocks.Audit;
using Microsoft.AspNetCore.Mvc;

namespace Embeddra.Admin.WebApi.Controllers;

[ApiController]
public sealed class AdminActionsController : ControllerBase
{
    private readonly IAuditLogWriter _auditLogWriter;

    public AdminActionsController(IAuditLogWriter auditLogWriter)
    {
        _auditLogWriter = auditLogWriter;
    }

    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant([FromBody] TenantRequest request, CancellationToken cancellationToken)
    {
        await _auditLogWriter.WriteAsync(
            new AuditLogEntry(AuditActions.TenantCreated, ResolveActor(), request),
            cancellationToken);

        return Accepted(new { status = "queued" });
    }

    [HttpPost("api-keys")]
    public async Task<IActionResult> CreateApiKey([FromBody] ApiKeyRequest request, CancellationToken cancellationToken)
    {
        await _auditLogWriter.WriteAsync(
            new AuditLogEntry(AuditActions.ApiKeyCreated, ResolveActor(), request),
            cancellationToken);

        return Accepted(new { status = "queued" });
    }

    [HttpDelete("api-keys/{keyId}")]
    public async Task<IActionResult> RevokeApiKey(string keyId, CancellationToken cancellationToken)
    {
        await _auditLogWriter.WriteAsync(
            new AuditLogEntry(AuditActions.ApiKeyRevoked, ResolveActor(), new { keyId }),
            cancellationToken);

        return Accepted(new { status = "queued" });
    }

    [HttpPut("allowed-origins")]
    public async Task<IActionResult> UpdateAllowedOrigins([FromBody] AllowedOriginsRequest request, CancellationToken cancellationToken)
    {
        await _auditLogWriter.WriteAsync(
            new AuditLogEntry(AuditActions.AllowedOriginsUpdated, ResolveActor(), request),
            cancellationToken);

        return Accepted(new { status = "queued" });
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

public sealed record ApiKeyRequest(string Name, string? Description);

public sealed record AllowedOriginsRequest(IReadOnlyCollection<string> Origins);
