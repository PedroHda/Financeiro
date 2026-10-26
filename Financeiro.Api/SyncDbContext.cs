using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Financeiro.Api;

public sealed class SyncDbContext(DbContextOptions<SyncDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<SyncWorkspace> Workspaces => Set<SyncWorkspace>();
    public DbSet<StoredRecord> Records => Set<StoredRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<SyncWorkspace>().HasKey(w => w.UserId);
        builder.Entity<SyncWorkspace>().Property(w => w.Revision).IsConcurrencyToken();
        builder.Entity<SyncWorkspace>().HasOne<IdentityUser>().WithMany().HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<StoredRecord>().HasKey(r => new { r.UserId, r.Id });
        builder.Entity<StoredRecord>().HasIndex(r => new { r.UserId, r.Revision }).IsUnique();
        builder.Entity<StoredRecord>().HasOne<IdentityUser>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<StoredRecord>().Property(r => r.Table).HasMaxLength(40);
    }
}

public sealed class SyncWorkspace
{
    public string UserId { get; set; } = "";
    public long Revision { get; set; }
}

public sealed class StoredRecord
{
    public string UserId { get; set; } = "";
    public Guid Id { get; set; }
    public string Table { get; set; } = "";
    public long Revision { get; set; }
    public string? Payload { get; set; }
    public bool Deleted { get; set; }
}
