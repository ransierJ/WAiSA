using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WAiSA.Infrastructure.Data;

/// <summary>
/// Design-time factory for WAiSADbContext
/// Used by Entity Framework tools for migrations
/// </summary>
public class WAiSADbContextFactory : IDesignTimeDbContextFactory<WAiSADbContext>
{
    public WAiSADbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WAiSADbContext>();

        // Use a placeholder connection string for design-time operations
        // This is only used during migration generation, not at runtime
        optionsBuilder.UseSqlServer(
            "Server=localhost;Database=WAiSADB;Trusted_Connection=True;TrustServerCertificate=True;",
            options => options.MigrationsHistoryTable("__EFMigrationsHistory")
        );

        return new WAiSADbContext(optionsBuilder.Options);
    }
}
