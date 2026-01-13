using Embeddra.Admin.Domain;
using Microsoft.EntityFrameworkCore;

namespace Embeddra.Admin.Infrastructure.Persistence;

public sealed class AdminDbContext : DbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<AllowedOrigin> AllowedOrigins => Set<AllowedOrigin>();
    public DbSet<IngestionJob> IngestionJobs => Set<IngestionJob>();
    public DbSet<ProductRaw> ProductsRaw => Set<ProductRaw>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<SearchEvent> SearchEvents => Set<SearchEvent>();
    public DbSet<SearchClick> SearchClicks => Set<SearchClick>();
    public DbSet<SearchSynonym> SearchSynonyms => Set<SearchSynonym>();
    public DbSet<SearchBoostRule> SearchBoostRules => Set<SearchBoostRule>();
    public DbSet<SearchPinnedResult> SearchPinnedResults => Set<SearchPinnedResult>();
    public DbSet<TenantSetting> TenantSettings => Set<TenantSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Name).HasColumnName("name");
            entity.Property(x => x.Status)
                .HasColumnName("status")
                .HasConversion<string>();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.ToTable("api_keys");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.Name).HasColumnName("name");
            entity.Property(x => x.Description).HasColumnName("description");
            entity.Property(x => x.KeyType).HasColumnName("key_type");
            entity.Property(x => x.KeyHash).HasColumnName("key_hash");
            entity.Property(x => x.KeyPrefix).HasColumnName("key_prefix");
            entity.Property(x => x.AllowedOrigins).HasColumnName("allowed_origins");
            entity.Property(x => x.Status)
                .HasColumnName("status")
                .HasConversion<string>();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            entity.HasIndex(x => x.TenantId).HasDatabaseName("ix_api_keys_tenant_id");
            entity.HasIndex(x => x.KeyHash).HasDatabaseName("ux_api_keys_key_hash").IsUnique();
        });

        modelBuilder.Entity<AllowedOrigin>(entity =>
        {
            entity.ToTable("allowed_origins");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.Origin).HasColumnName("origin");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(x => new { x.TenantId, x.Origin })
                .HasDatabaseName("ux_allowed_origins_tenant_origin")
                .IsUnique();
        });

        modelBuilder.Entity<IngestionJob>(entity =>
        {
            entity.ToTable("ingestion_jobs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.SourceType)
                .HasColumnName("source_type")
                .HasConversion<string>();
            entity.Property(x => x.Status)
                .HasColumnName("status")
                .HasConversion<string>();
            entity.Property(x => x.TotalCount).HasColumnName("total_count");
            entity.Property(x => x.ProcessedCount).HasColumnName("processed_count");
            entity.Property(x => x.FailedCount).HasColumnName("failed_count");
            entity.Property(x => x.Error).HasColumnName("error");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.StartedAt).HasColumnName("started_at");
            entity.Property(x => x.CompletedAt).HasColumnName("completed_at");
            entity.HasIndex(x => x.TenantId).HasDatabaseName("ix_ingestion_jobs_tenant_id");
        });

        modelBuilder.Entity<ProductRaw>(entity =>
        {
            entity.ToTable("products_raw");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.JobId).HasColumnName("job_id");
            entity.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(x => x.JobId).HasDatabaseName("ix_products_raw_job_id");
            entity.HasIndex(x => x.TenantId).HasDatabaseName("ix_products_raw_tenant_id");
        });

        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.ToTable("admin_users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.Email).HasColumnName("email");
            entity.Property(x => x.Name).HasColumnName("name");
            entity.Property(x => x.PasswordHash).HasColumnName("password_hash");
            entity.Property(x => x.Role).HasColumnName("role");
            entity.Property(x => x.Status)
                .HasColumnName("status")
                .HasConversion<string>();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.LastLoginAt).HasColumnName("last_login_at");
            entity.HasIndex(x => x.TenantId).HasDatabaseName("ix_admin_users_tenant_id");
            entity.HasIndex(x => x.Email).HasDatabaseName("ix_admin_users_email");
            entity.HasIndex(x => new { x.TenantId, x.Email }).HasDatabaseName("ux_admin_users_tenant_email").IsUnique();
        });

        modelBuilder.Entity<SearchEvent>(entity =>
        {
            entity.ToTable("search_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.Query).HasColumnName("query");
            entity.Property(x => x.ResultCount).HasColumnName("result_count");
            entity.Property(x => x.Bm25TookMs).HasColumnName("bm25_took_ms");
            entity.Property(x => x.KnnTookMs).HasColumnName("knn_took_ms");
            entity.Property(x => x.CorrelationId).HasColumnName("correlation_id");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(x => x.TenantId).HasDatabaseName("ix_search_events_tenant_id");
            entity.HasIndex(x => x.Query).HasDatabaseName("ix_search_events_query");
            entity.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_search_events_created_at");
        });

        modelBuilder.Entity<SearchClick>(entity =>
        {
            entity.ToTable("search_clicks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.SearchId).HasColumnName("search_id");
            entity.Property(x => x.ProductId).HasColumnName("product_id");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(x => x.TenantId).HasDatabaseName("ix_search_clicks_tenant_id");
            entity.HasIndex(x => x.SearchId).HasDatabaseName("ix_search_clicks_search_id");
            entity.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_search_clicks_created_at");
        });

        modelBuilder.Entity<SearchSynonym>(entity =>
        {
            entity.ToTable("search_synonyms");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.Term).HasColumnName("term");
            entity.Property(x => x.SynonymsJson).HasColumnName("synonyms_json").HasColumnType("jsonb");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(x => new { x.TenantId, x.Term }).HasDatabaseName("ux_search_synonyms_tenant_term").IsUnique();
        });

        modelBuilder.Entity<SearchBoostRule>(entity =>
        {
            entity.ToTable("search_boosts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.Field).HasColumnName("field");
            entity.Property(x => x.Value).HasColumnName("value");
            entity.Property(x => x.Weight).HasColumnName("weight");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(x => new { x.TenantId, x.Field, x.Value })
                .HasDatabaseName("ux_search_boosts_tenant_field_value")
                .IsUnique();
        });

        modelBuilder.Entity<SearchPinnedResult>(entity =>
        {
            entity.ToTable("search_pins");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.Query).HasColumnName("query");
            entity.Property(x => x.ProductIdsJson).HasColumnName("product_ids_json").HasColumnType("jsonb");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(x => new { x.TenantId, x.Query }).HasDatabaseName("ux_search_pins_tenant_query").IsUnique();
        });

        modelBuilder.Entity<TenantSetting>(entity =>
        {
            entity.ToTable("tenant_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.Key).HasColumnName("setting_key");
            entity.Property(x => x.Value).HasColumnName("setting_value");
            entity.Property(x => x.Type)
                .HasColumnName("setting_type")
                .HasConversion<string>();
            entity.Property(x => x.Description).HasColumnName("description");
            entity.Property(x => x.IsSensitive).HasColumnName("is_sensitive");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(x => x.TenantId).HasDatabaseName("ix_tenant_settings_tenant_id");
            entity.HasIndex(x => new { x.TenantId, x.Key })
                .HasDatabaseName("ux_tenant_settings_tenant_key")
                .IsUnique();
        });
    }
}
