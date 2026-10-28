using Microsoft.EntityFrameworkCore;

using MessageBrokerDemo.Model.Content;
using MessageBrokerDemo.Model.Job;
using MessageBrokerDemo.Model.Outbox;
using MessageBrokerDemo.Model.Validation;

namespace MessageBrokerDemo.Data.Persistence;

public sealed class WCMSDBContext(DbContextOptions<WCMSDBContext> options) : DbContext(options)
{
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<ContentItem> ContentItems => Set<ContentItem>();
    public DbSet<ValidationError> ValidationErrors => Set<ValidationError>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Job>(entity =>
        {
            entity.ToTable("jobs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedOnAdd();   // incremental id as primary key 
            entity.Property(x => x.TenantId).HasMaxLength(128).IsRequired();            
            entity.Property(x => x.StagedObjectPath).HasMaxLength(1024).IsRequired();
            entity.Property(x => x.CorrelationId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<ContentItem>(entity =>
        {
            entity.ToTable("content_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MetadataJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<ValidationError>(entity =>
        {
            entity.ToTable("validation_errors");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(1024).IsRequired();
        });

        modelBuilder.Entity<OutboxEvent>(entity =>
        {
            entity.ToTable("outbox_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.LastError).HasMaxLength(1024);
            entity.HasIndex(x => new { x.PublishedAtUtc, x.OccurredAtUtc });
        });

        base.OnModelCreating(modelBuilder);
    }
}