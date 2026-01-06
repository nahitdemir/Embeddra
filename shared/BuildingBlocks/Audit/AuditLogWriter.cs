using Embeddra.BuildingBlocks.Correlation;
using Embeddra.BuildingBlocks.Logging;
using Embeddra.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Embeddra.BuildingBlocks.Audit;

public interface IAuditLogWriter
{
    Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken);
}

public sealed class AuditLogWriter : IAuditLogWriter
{
    private readonly AuditDbContext _dbContext;
    private readonly ILogger<AuditLogWriter> _logger;

    public AuditLogWriter(AuditDbContext dbContext, ILogger<AuditLogWriter> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            var auditLog = new AuditLogEntity
            {
                Id = Guid.NewGuid(),
                TenantId = entry.TenantId ?? TenantContext.TenantId,
                Action = entry.Action,
                Actor = entry.Actor,
                CorrelationId = entry.CorrelationId ?? CorrelationContext.CorrelationId,
                PayloadJson = SensitiveDataMasker.MaskObject(entry.Payload),
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.AuditLogs.Add(auditLog);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit log write failed");
        }
    }
}
