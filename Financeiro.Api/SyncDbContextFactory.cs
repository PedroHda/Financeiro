using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Financeiro.Api;

public sealed class SyncDbContextFactory : IDesignTimeDbContextFactory<SyncDbContext>
{
    public SyncDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<SyncDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Financeiro") ?? "Host=localhost;Database=financeiro;Username=financeiro")
        .Options);
}
