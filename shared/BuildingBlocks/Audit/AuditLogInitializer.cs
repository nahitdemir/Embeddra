using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Embeddra.BuildingBlocks.Audit;

public sealed class AuditLogInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditLogInitializer> _logger;

    public AuditLogInitializer(IServiceScopeFactory scopeFactory, ILogger<AuditLogInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

            const string sql = """
                CREATE TABLE IF NOT EXISTS audit_logs (
                    id uuid PRIMARY KEY,
                    tenant_id text NULL,
                    action text NOT NULL,
                    actor text NULL,
                    correlation_id text NULL,
                    payload_json jsonb NOT NULL,
                    created_at timestamptz NOT NULL
                );
                """;

            await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit log table initialization failed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
