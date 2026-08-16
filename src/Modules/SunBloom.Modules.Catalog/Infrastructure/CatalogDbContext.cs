using Microsoft.EntityFrameworkCore;
using SunBloom.Modules.Catalog.Domain;

namespace SunBloom.Modules.Catalog.Infrastructure;

/// <summary>Persistence for the Catalog module. One context and schema per module (ADR-0006).</summary>
internal sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options)
    : DbContext(options)
{
    public const string Schema = "catalog";

    public DbSet<Skill> Skills => Set<Skill>();

    public DbSet<SkillRelationship> SkillRelationships => Set<SkillRelationship>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
