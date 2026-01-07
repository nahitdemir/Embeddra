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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Name).HasColumnName("name");
            entity.Property(x => x.Status).HasColumnName("status");
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
            entity.Property(x => x.KeyHash).HasColumnName("key_hash");
            entity.Property(x => x.KeyPrefix).HasColumnName("key_prefix");
            entity.Property(x => x.Status).HasColumnName("status");
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
    }
}
