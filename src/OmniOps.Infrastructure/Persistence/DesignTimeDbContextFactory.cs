using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OmniOps.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the model without starting the API. The connection string is only used
/// by commands that touch a database (e.g. <c>database update</c>); set OMNIOPS_DESIGN_CONNECTION for those.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("OMNIOPS_DESIGN_CONNECTION") ?? "Host=localhost;Database=omniops")
            .Options);
}
