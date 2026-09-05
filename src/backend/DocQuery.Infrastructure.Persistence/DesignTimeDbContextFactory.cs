using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DocQuery.Infrastructure.Persistence;

/// <summary>Used only by <c>dotnet ef</c> to build the model when adding migrations; no database is contacted.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DocQueryDbContext>
{
    public DocQueryDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DocQueryDbContext>()
            .UseNpgsql("Host=localhost;Database=docquery;Username=postgres;Password=design-time-only")
            .Options;

        return new DocQueryDbContext(options);
    }
}
