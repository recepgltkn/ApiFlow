using ApiFlow.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiFlow.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ApiProfile> ApiProfiles => Set<ApiProfile>();
    public DbSet<ApiEndpoint> ApiEndpoints => Set<ApiEndpoint>();
    public DbSet<SqlProfile> SqlProfiles => Set<SqlProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApiProfile>(entity =>
        {
            entity.HasIndex(profile => profile.Name).IsUnique();
            entity.Property(profile => profile.BaseUrl).HasMaxLength(300).IsRequired();
            entity.Property(profile => profile.Language).HasDefaultValue("tr");
            entity.Property(profile => profile.DisconnectSameUser).HasDefaultValue(true);
            entity.Property(profile => profile.LoginPath).HasMaxLength(300);
            entity.Property(profile => profile.LoginHttpMethod).HasMaxLength(20).HasDefaultValue("POST");
            entity.Property(profile => profile.LoginBodyTemplate).HasMaxLength(2000);
            entity.Property(profile => profile.DefaultHeadersJson).HasMaxLength(4000);
            entity.Property(profile => profile.SessionIdJsonPath).HasMaxLength(100);
        });

        modelBuilder.Entity<ApiEndpoint>(entity =>
        {
            entity.HasIndex(endpoint => new { endpoint.ProfileId, endpoint.Key }).IsUnique();
            entity.Property(endpoint => endpoint.Key).HasMaxLength(100).IsRequired();
            entity.Property(endpoint => endpoint.HttpMethod).HasMaxLength(20).IsRequired().HasDefaultValue("POST");
            entity.Property(endpoint => endpoint.RequestBodyTemplate).HasMaxLength(2000);
            entity.Property(endpoint => endpoint.HeadersJson).HasMaxLength(4000);
            entity.Property(endpoint => endpoint.ResultJsonPath).HasMaxLength(100);
            entity.Property(endpoint => endpoint.TargetTableName).HasMaxLength(128);
            entity.Property(endpoint => endpoint.Path).HasMaxLength(300).IsRequired();
            entity.HasOne(endpoint => endpoint.Profile)
                .WithMany()
                .HasForeignKey(endpoint => endpoint.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SqlProfile>(entity =>
        {
            entity.HasIndex(profile => profile.Name).IsUnique();
            entity.HasIndex(profile => profile.Key).IsUnique();
            entity.Property(profile => profile.Name).HasMaxLength(100).IsRequired();
            entity.Property(profile => profile.Key).HasMaxLength(100).IsRequired();
            entity.Property(profile => profile.Host).HasMaxLength(300).IsRequired();
            entity.Property(profile => profile.InstanceName).HasMaxLength(100);
            entity.Property(profile => profile.DatabaseName).HasMaxLength(200).IsRequired();
            entity.Property(profile => profile.Username).HasMaxLength(200).IsRequired();
            entity.Property(profile => profile.Password).HasMaxLength(300).IsRequired();
            entity.Property(profile => profile.ApplicationName).HasMaxLength(200);
            entity.Property(profile => profile.TrustServerCertificate).HasDefaultValue(true);
            entity.Property(profile => profile.Encrypt).HasDefaultValue(false);
            entity.Property(profile => profile.SchemaName).HasMaxLength(128).IsRequired();
        });
    }
}
