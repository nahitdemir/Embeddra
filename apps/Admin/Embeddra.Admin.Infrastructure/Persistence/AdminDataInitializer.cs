using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity;
using Embeddra.Admin.Domain;

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
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AdminUser>>();

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
                    key_type text NULL,
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

                CREATE TABLE IF NOT EXISTS admin_users (
                    id uuid PRIMARY KEY,
                    tenant_id text NOT NULL,
                    email text NOT NULL,
                    name text NOT NULL,
                    password_hash text NOT NULL,
                    role text NOT NULL,
                    status text NOT NULL,
                    created_at timestamptz NOT NULL,
                    last_login_at timestamptz NULL
                );

                CREATE TABLE IF NOT EXISTS search_events (
                    id uuid PRIMARY KEY,
                    tenant_id text NOT NULL,
                    query text NOT NULL,
                    result_count integer NOT NULL,
                    bm25_took_ms integer NULL,
                    knn_took_ms integer NULL,
                    correlation_id text NULL,
                    created_at timestamptz NOT NULL
                );

                CREATE TABLE IF NOT EXISTS search_clicks (
                    id uuid PRIMARY KEY,
                    tenant_id text NOT NULL,
                    search_id uuid NOT NULL,
                    product_id text NOT NULL,
                    created_at timestamptz NOT NULL
                );

                CREATE TABLE IF NOT EXISTS search_synonyms (
                    id uuid PRIMARY KEY,
                    tenant_id text NOT NULL,
                    term text NOT NULL,
                    synonyms_json jsonb NOT NULL,
                    created_at timestamptz NOT NULL
                );

                CREATE TABLE IF NOT EXISTS search_boosts (
                    id uuid PRIMARY KEY,
                    tenant_id text NOT NULL,
                    field text NOT NULL,
                    value text NOT NULL,
                    weight double precision NOT NULL,
                    created_at timestamptz NOT NULL
                );

                CREATE TABLE IF NOT EXISTS search_pins (
                    id uuid PRIMARY KEY,
                    tenant_id text NOT NULL,
                    query text NOT NULL,
                    product_ids_json jsonb NOT NULL,
                    created_at timestamptz NOT NULL
                );

                CREATE INDEX IF NOT EXISTS ix_api_keys_tenant_id ON api_keys (tenant_id);
                CREATE UNIQUE INDEX IF NOT EXISTS ux_api_keys_key_hash ON api_keys (key_hash);
                CREATE UNIQUE INDEX IF NOT EXISTS ux_allowed_origins_tenant_origin ON allowed_origins (tenant_id, origin);
                CREATE INDEX IF NOT EXISTS ix_ingestion_jobs_tenant_id ON ingestion_jobs (tenant_id);
                CREATE INDEX IF NOT EXISTS ix_products_raw_job_id ON products_raw (job_id);
                CREATE INDEX IF NOT EXISTS ix_products_raw_tenant_id ON products_raw (tenant_id);
                CREATE INDEX IF NOT EXISTS ix_admin_users_tenant_id ON admin_users (tenant_id);
                CREATE INDEX IF NOT EXISTS ix_admin_users_email ON admin_users (email);
                CREATE UNIQUE INDEX IF NOT EXISTS ux_admin_users_tenant_email ON admin_users (tenant_id, email);
                CREATE INDEX IF NOT EXISTS ix_search_events_tenant_id ON search_events (tenant_id);
                CREATE INDEX IF NOT EXISTS ix_search_events_query ON search_events (query);
                CREATE INDEX IF NOT EXISTS ix_search_events_created_at ON search_events (created_at);
                CREATE INDEX IF NOT EXISTS ix_search_clicks_tenant_id ON search_clicks (tenant_id);
                CREATE INDEX IF NOT EXISTS ix_search_clicks_search_id ON search_clicks (search_id);
                CREATE INDEX IF NOT EXISTS ix_search_clicks_created_at ON search_clicks (created_at);
                CREATE UNIQUE INDEX IF NOT EXISTS ux_search_synonyms_tenant_term ON search_synonyms (tenant_id, term);
                CREATE UNIQUE INDEX IF NOT EXISTS ux_search_boosts_tenant_field_value ON search_boosts (tenant_id, field, value);
                CREATE UNIQUE INDEX IF NOT EXISTS ux_search_pins_tenant_query ON search_pins (tenant_id, query);

                CREATE TABLE IF NOT EXISTS tenant_settings (
                    id uuid PRIMARY KEY,
                    tenant_id text NOT NULL,
                    setting_key text NOT NULL,
                    setting_value text NOT NULL,
                    setting_type text NOT NULL,
                    description text NULL,
                    is_sensitive boolean NOT NULL DEFAULT false,
                    created_at timestamptz NOT NULL,
                    updated_at timestamptz NOT NULL
                );

                CREATE INDEX IF NOT EXISTS ix_tenant_settings_tenant_id ON tenant_settings (tenant_id);
                CREATE UNIQUE INDEX IF NOT EXISTS ux_tenant_settings_tenant_key ON tenant_settings (tenant_id, setting_key);

                ALTER TABLE api_keys ADD COLUMN IF NOT EXISTS key_type text;
                UPDATE api_keys SET key_type = 'search_public' WHERE key_type IS NULL;

                ALTER TABLE api_keys ADD COLUMN IF NOT EXISTS allowed_origins text[];
                
                ALTER TABLE admin_users ALTER COLUMN tenant_id DROP NOT NULL;
                """;

            await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);

            await EnsurePlatformOwnerAsync(dbContext, configuration, passwordHasher, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Admin data initialization failed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task EnsurePlatformOwnerAsync(
        AdminDbContext dbContext,
        IConfiguration configuration,
        IPasswordHasher<AdminUser> passwordHasher,
        CancellationToken cancellationToken)
    {
        var platformRole = UserRole.PlatformOwner;
        var section = configuration.GetSection("Admin:PlatformOwner");
        var email = section["Email"];
        var password = section["Password"];
        var name = section["Name"] ?? "Platform Owner";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var exists = await dbContext.AdminUsers
            .AnyAsync(x => x.TenantId == null && x.Email == normalizedEmail, cancellationToken);

        if (exists)
        {
            return;
        }

        // Önce şifreyi hashle (dummy user ile)
        var tempUser = AdminUser.Create(null, normalizedEmail, name, "temp", platformRole);
        var hashedPassword = passwordHasher.HashPassword(tempUser, password);
        
        // Artık gerçek kullanıcıyı oluştur
        var user = AdminUser.Create(null, normalizedEmail, name, hashedPassword, platformRole);

        dbContext.AdminUsers.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
