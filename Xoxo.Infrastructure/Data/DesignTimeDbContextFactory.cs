using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Xoxo.Infrastructure.Data;

/// <summary>
/// Used only by `dotnet ef` to build the model when adding migrations.
/// It never connects, so a placeholder connection string is fine; real
/// connection strings come from configuration / user-secrets at runtime.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=design-time;Database=Caskly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new AppDbContext(options);
    }
}
