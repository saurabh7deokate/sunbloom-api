using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SunBloom.Modules.Catalog.Domain;

namespace SunBloom.Modules.Catalog.Infrastructure;

internal sealed class SkillRelationshipConfiguration : IEntityTypeConfiguration<SkillRelationship>
{
    public void Configure(EntityTypeBuilder<SkillRelationship> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("skill_relationships", table =>
            table.HasCheckConstraint("ck_skill_rel_no_self", "from_skill_id <> to_skill_id"));

        builder.HasKey(relationship => relationship.Id);

        builder.Property(relationship => relationship.Type)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(relationship => relationship.Strength)
            .HasPrecision(3, 2)
            .HasDefaultValue(1.0m);

        builder.HasIndex(r => new { r.FromSkillId, r.ToSkillId, r.Type })
            .IsUnique()
            .HasDatabaseName("uq_skill_rel");

        // Both directions are indexed because prerequisite traversal runs forwards
        // ("what does this need?") and backwards ("what does this unlock?" - the
        // unlockCount term in gap ranking, SCORING.md §3.2).
        builder.HasIndex(r => new { r.FromSkillId, r.Type });
        builder.HasIndex(r => new { r.ToSkillId, r.Type });

        builder.HasOne<Skill>().WithMany()
            .HasForeignKey(r => r.FromSkillId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Skill>().WithMany()
            .HasForeignKey(r => r.ToSkillId).OnDelete(DeleteBehavior.Cascade);
    }
}
