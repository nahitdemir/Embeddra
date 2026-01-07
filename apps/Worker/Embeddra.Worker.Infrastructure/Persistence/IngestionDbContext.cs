using Microsoft.EntityFrameworkCore;

namespace Embeddra.Worker.Infrastructure.Persistence;

public sealed class IngestionDbContext : DbContext
{
    public IngestionDbContext(DbContextOptions<IngestionDbContext> options)
        : base(options)
    {
    }

    public DbSet<IngestionJob> IngestionJobs => Set<IngestionJob>();
    public DbSet<ProductRaw> ProductsRaw => Set<ProductRaw>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
