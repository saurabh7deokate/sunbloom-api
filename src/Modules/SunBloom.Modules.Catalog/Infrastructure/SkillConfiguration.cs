using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SunBloom.Modules.Catalog.Domain;

namespace SunBloom.Modules.Catalog.Infrastructure;

internal sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("skills");
        builder.HasKey(skill => skill.Id);

        builder.Property(skill => skill.Slug).HasMaxLength(150).IsRequired();
        builder.HasIndex(skill => skill.Slug).IsUnique();

        builder.Property(skill => skill.Name).HasMaxLength(200).IsRequired();
        builder.Property(skill => skill.Description).HasMaxLength(2000);

        // Enums as text, not ordinals: a readable database beats two saved bytes, and
        // reordering an enum can silently remap every existing row.
        builder.Property(skill => skill.Kind)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.HasOne<Skill>()
            .WithMany()
            .HasForeignKey(skill => skill.ParentSkillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(skill => skill.ParentSkillId);

        builder.Property(skill => skill.IsActive).HasDefaultValue(true);
        builder.Property(skill => skill.CreatedAt).IsRequired();
        builder.Property(skill => skill.UpdatedAt).IsRequired();

        // Column names spelled out so they match DATABASE.md rather than acquiring a
        // "provenance_" prefix from the owned-type convention.
        builder.OwnsOne(skill => skill.Provenance, provenance =>
        {
            provenance.Property(p => p.GenerationSource)
                .HasColumnName("generation_source").HasConversion<string>().HasMaxLength(20).IsRequired();
            provenance.Property(p => p.GeneratorModel).HasColumnName("generator_model").HasMaxLength(100);
            provenance.Property(p => p.GeneratorPromptVersion).HasColumnName("generator_prompt_version").HasMaxLength(50);
            provenance.Property(p => p.GeneratedAt).HasColumnName("generated_at");
            provenance.Property(p => p.ReviewState)
                .HasColumnName("review_state").HasConversion<string>().HasMaxLength(20).IsRequired();
            provenance.Property(p => p.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
            provenance.Property(p => p.ReviewedAt).HasColumnName("reviewed_at");
            provenance.Property(p => p.ReviewNotes).HasColumnName("review_notes").HasMaxLength(2000);

            // Every learner-facing query filters to approved and active, which is a
            // small subset once generation is running.
            provenance.HasIndex(p => p.ReviewState);
        });

        builder.Navigation(skill => skill.Provenance).IsRequired();
    }
}
