using WAiSA.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace WAiSA.Infrastructure.Data;

/// <summary>
/// Entity Framework DbContext for SQL Database
/// Handles structured data: devices, audit logs, and system configuration
/// </summary>
public class WAiSADbContext : DbContext
{
    public WAiSADbContext(DbContextOptions<WAiSADbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Device registrations
    /// </summary>
    public DbSet<Device> Devices { get; set; } = null!;

    /// <summary>
    /// Audit logs
    /// </summary>
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;

    /// <summary>
    /// System configuration
    /// </summary>
    public DbSet<SystemConfiguration> SystemConfigurations { get; set; } = null!;

    /// <summary>
    /// Windows Agents
    /// </summary>
    public DbSet<Agent> Agents { get; set; } = null!;

    /// <summary>
    /// Command execution queue
    /// </summary>
    public DbSet<CommandQueue> CommandQueues { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Device entity configuration
        modelBuilder.Entity<Device>(entity =>
        {
            entity.ToTable("Devices");
            entity.HasKey(e => e.Id);

            // DeviceId must be unique
            entity.HasIndex(e => e.DeviceId)
                .IsUnique();

            // Required fields
            entity.Property(e => e.DeviceId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.DeviceName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.HostName)
                .HasMaxLength(200);

            entity.Property(e => e.OperatingSystem)
                .HasMaxLength(100);

            entity.Property(e => e.OSVersion)
                .HasMaxLength(50);

            entity.Property(e => e.Manufacturer)
                .HasMaxLength(100);

            entity.Property(e => e.Model)
                .HasMaxLength(200);

            entity.Property(e => e.Architecture)
                .HasMaxLength(20);

            entity.Property(e => e.IpAddress)
                .HasMaxLength(45); // IPv6 support

            entity.Property(e => e.MacAddress)
                .HasMaxLength(17);

            // Index for active devices
            entity.HasIndex(e => e.IsActive);

            // Index for last seen queries
            entity.HasIndex(e => e.LastSeenAt);
        });

        // AuditLog entity configuration
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(e => e.Id);

            // Required fields
            entity.Property(e => e.Category)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.ActionType)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(e => e.Actor)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Severity)
                .HasMaxLength(20);

            entity.Property(e => e.IpAddress)
                .HasMaxLength(45);

            entity.Property(e => e.CorrelationId)
                .HasMaxLength(50);

            // Indexes for common queries
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.Severity);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.CorrelationId);

            // Foreign key relationship
            entity.HasOne(e => e.Device)
                .WithMany(d => d.AuditLogs)
                .HasForeignKey(e => e.DeviceId)
                .OnDelete(DeleteBehavior.SetNull); // Keep audit logs even if device is deleted
        });

        // SystemConfiguration entity configuration
        modelBuilder.Entity<SystemConfiguration>(entity =>
        {
            entity.ToTable("SystemConfigurations");
            entity.HasKey(e => e.Id);

            // Key must be unique
            entity.HasIndex(e => e.Key)
                .IsUnique();

            // Required fields
            entity.Property(e => e.Key)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Value)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasMaxLength(500);

            entity.Property(e => e.Category)
                .HasMaxLength(50);

            entity.Property(e => e.DataType)
                .HasMaxLength(20);

            entity.Property(e => e.ModifiedBy)
                .HasMaxLength(100);

            // Indexes for queries
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.IsFeatureFlag);
        });

        // Agent entity configuration
        modelBuilder.Entity<Agent>(entity =>
        {
            entity.ToTable("Agents");
            entity.HasKey(e => e.Id);

            // AgentId must be unique
            entity.HasIndex(e => e.AgentId)
                .IsUnique();

            // Required fields
            entity.Property(e => e.AgentId)
                .IsRequired();

            entity.Property(e => e.ComputerName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.ApiKeyHash)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(e => e.Version)
                .HasMaxLength(20);

            entity.Property(e => e.OsVersion)
                .HasMaxLength(100);

            entity.Property(e => e.InstallationKey)
                .HasMaxLength(100);

            // Indexes for common queries
            entity.HasIndex(e => e.ComputerName);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.LastHeartbeat);
            entity.HasIndex(e => e.IsEnabled);
        });

        // CommandQueue entity configuration
        modelBuilder.Entity<CommandQueue>(entity =>
        {
            entity.ToTable("CommandQueue");
            entity.HasKey(e => e.Id);

            // CommandId must be unique
            entity.HasIndex(e => e.CommandId)
                .IsUnique();

            // Required fields
            entity.Property(e => e.CommandId)
                .IsRequired();

            entity.Property(e => e.AgentId)
                .IsRequired();

            entity.Property(e => e.Command)
                .IsRequired();

            entity.Property(e => e.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(e => e.ExecutionContext)
                .HasMaxLength(500);

            entity.Property(e => e.InitiatedBy)
                .HasMaxLength(100);

            entity.Property(e => e.ChatSessionId)
                .HasMaxLength(100);

            entity.Property(e => e.ApprovedBy)
                .HasMaxLength(100);

            // Indexes for common queries
            entity.HasIndex(e => e.AgentId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.ChatSessionId);
            entity.HasIndex(e => new { e.AgentId, e.Status }); // Composite index for pending commands

            // Foreign key relationship
            entity.HasOne(e => e.Agent)
                .WithMany(a => a.Commands)
                .HasForeignKey(e => e.AgentId)
                .HasPrincipalKey(a => a.AgentId)
                .OnDelete(DeleteBehavior.Cascade); // Delete commands if agent is deleted
        });
    }
}
