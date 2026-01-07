using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Embeddra.Admin.Infrastructure.Persistence;

public sealed class AdminDataInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AdminDataInitializer> _logger;

    public AdminDataInitializer(IServiceScopeFactory scopeFactory, ILogger<AdminDataInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AdminDbContext>();

            const string sql = """
                CREATE TABLE IF NOT EXISTS tenants (
                    id text PRIMARY KEY,
                    name text NOT NULL,
                    status text NOT NULL,
                    created_at timestamptz NOT NULL
                );

                CREATE TABLE IF NOT EXISTS api_keys (
                    id uuid PRIMARY KEY,
                    tenant_id text NOT NULL,
                    name text NOT NULL,
                    description text NULL,
                    key_hash text NULL,
                    key_prefix text NULL,
                    status text NOT NULL,
                    created_at timestamptz NOT NULL,
                    revoked_at timestamptz NULL
                );

                CREATE TABLE IF NOT EXISTS allowed_origins (
                    id uuid PRIMARY KEY,
                    tenant_id text NOT NULL,
                    origin text NOT NULL,
                    created_at timestamptz NOT NULL
                );

                CREATE TABLE IF NOT EXISTS ingestion_jobs (
                    id uuid PRIMARY KEY,
                    tenant_id text NOT NULL,
                    source_type text NOT NULL,
                    status text NOT NULL,
                    total_count integer NULL,
                    processed_count integer NOT NULL,
                    failed_count integer NOT NULL,
                    error text NULL,
                    created_at timestamptz NOT NULL,
                    started_at timestamptz NULL,
                    completed_at timestamptz NULL
                );

                CREATE TABLE IF NOT EXISTS products_raw (
                    id uuid PRIMARY KEY,
                    tenant_id text NOT NULL,
                    job_id uuid NOT NULL,
                    payload_json jsonb NOT NULL,
                    created_at timestamptz NOT NULL
                );

                CREATE INDEX IF NOT EXISTS ix_api_keys_tenant_id ON api_keys (tenant_id);
                CREATE UNIQUE INDEX IF NOT EXISTS ux_api_keys_key_hash ON api_keys (key_hash);
                CREATE UNIQUE INDEX IF NOT EXISTS ux_allowed_origins_tenant_origin ON allowed_origins (tenant_id, origin);
                CREATE INDEX IF NOT EXISTS ix_ingestion_jobs_tenant_id ON ingestion_jobs (tenant_id);
                CREATE INDEX IF NOT EXISTS ix_products_raw_job_id ON products_raw (job_id);
                CREATE INDEX IF NOT EXISTS ix_products_raw_tenant_id ON products_raw (tenant_id);
                """;

            await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Admin data initialization failed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
