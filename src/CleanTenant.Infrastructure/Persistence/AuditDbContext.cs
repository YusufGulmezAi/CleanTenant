using CleanTenant.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CleanTenant.Infrastructure.Persistence;

/// <summary>
/// Audit veritabanı DbContext'i — ayrı PostgreSQL instance'ı kullanır.
/// 
/// <para><b>AYRI VERİTABANI NEDENLERİ:</b></para>
/// <list type="bullet">
///   <item>Yoğun INSERT'ler ana veritabanının performansını etkilemesin</item>
///   <item>Farklı yedekleme ve retention (saklama) politikası uygulanabilsin</item>
///   <item>Yasal uyumluluk: Audit verisi bağımsız saklanmalı</item>
///   <item>Büyüme hızı: Audit verileri çok hızlı büyür</item>
/// </list>
/// </summary>
public class AuditDbContext : DbContext, IAuditDbContext
{
    public AuditDbContext(DbContextOptions<AuditDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SecurityLog> SecurityLogs => Set<SecurityLog>();
    public DbSet<CleanTenant.Domain.Email.EmailLog> EmailLogs => Set<CleanTenant.Domain.Email.EmailLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Audit DB'de de tüm DateTime'lar UTC olarak saklanır
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>();

        configurationBuilder.Properties<DateTime?>()
            .HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ================================================================
        // AUDIT LOG TABLOSU
        // ================================================================
        modelBuilder.Entity<AuditLog>(builder =>
        {
            builder.ToTable("AuditLogs");
            builder.HasKey(a => a.Id);

            builder.Property(a => a.UserId).IsRequired().HasMaxLength(256);
            builder.Property(a => a.UserEmail).IsRequired().HasMaxLength(256);
            builder.Property(a => a.IpAddress).IsRequired().HasMaxLength(50);
            builder.Property(a => a.UserAgent).HasMaxLength(500);
            builder.Property(a => a.EntityName).IsRequired().HasMaxLength(200);
            builder.Property(a => a.EntityId).IsRequired().HasMaxLength(100);
            builder.Property(a => a.Action).IsRequired().HasMaxLength(20);

            // JSONB: PostgreSQL'de sorgulanabilir JSON
            builder.Property(a => a.OldValues).HasColumnType("jsonb");
            builder.Property(a => a.NewValues).HasColumnType("jsonb");

            // String array → PostgreSQL text[]
            builder.Property(a => a.AffectedColumns)
                .HasColumnType("text[]");

            // Performans index'leri — en sık yapılan sorgular için
            builder.HasIndex(a => a.Timestamp)
                .HasDatabaseName("IX_AuditLogs_Timestamp");

            builder.HasIndex(a => a.EntityName)
                .HasDatabaseName("IX_AuditLogs_EntityName");

            builder.HasIndex(a => new { a.EntityName, a.EntityId })
                .HasDatabaseName("IX_AuditLogs_Entity");

            builder.HasIndex(a => a.UserId)
                .HasDatabaseName("IX_AuditLogs_UserId");

            builder.HasIndex(a => a.TenantId)
                .HasDatabaseName("IX_AuditLogs_TenantId");
        });

        // ================================================================
        // SECURITY LOG TABLOSU
        // ================================================================
        modelBuilder.Entity<SecurityLog>(builder =>
        {
            builder.ToTable("SecurityLogs");
            builder.HasKey(s => s.Id);

            builder.Property(s => s.UserEmail).HasMaxLength(256);
            builder.Property(s => s.IpAddress).IsRequired().HasMaxLength(50);
            builder.Property(s => s.UserAgent).HasMaxLength(500);
            builder.Property(s => s.EventType).IsRequired().HasMaxLength(100);
            builder.Property(s => s.Description).HasMaxLength(1000);
            builder.Property(s => s.Details).HasColumnType("jsonb");

            builder.HasIndex(s => s.Timestamp)
                .HasDatabaseName("IX_SecurityLogs_Timestamp");

            builder.HasIndex(s => s.EventType)
                .HasDatabaseName("IX_SecurityLogs_EventType");

            builder.HasIndex(s => s.UserId)
                .HasDatabaseName("IX_SecurityLogs_UserId");

            builder.HasIndex(s => s.IpAddress)
                .HasDatabaseName("IX_SecurityLogs_IpAddress");
        });
    }
}
