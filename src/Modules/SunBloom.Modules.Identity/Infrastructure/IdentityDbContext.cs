using Microsoft.EntityFrameworkCore;
using SunBloom.Modules.Identity.Domain;

namespace SunBloom.Modules.Identity.Infrastructure;

/// <summary>
/// Persistence for the Identity module. One DbContext and one schema per module (ADR-0006).
/// </summary>
/// <remarks>
/// Separate contexts make a cross-module join impossible rather than merely discouraged:
/// EF cannot express a navigation property across two contexts.
/// </remarks>
internal sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : DbContext(options)
{
    public const string Schema = "identity";

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
