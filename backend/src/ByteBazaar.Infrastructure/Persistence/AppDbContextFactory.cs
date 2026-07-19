using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ByteBazaar.Infrastructure.Persistence;

/// <summary>Design-time factory so `dotnet ef` works without a live database or running host.</summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5433;Database=bytebazaar;Username=bytebazaar;Password=bytebazaar_dev",
                npgsql => npgsql.ConfigureDataSource(ds => ds.EnableDynamicJson()))
            .Options;
        return new AppDbContext(options);
    }
}
