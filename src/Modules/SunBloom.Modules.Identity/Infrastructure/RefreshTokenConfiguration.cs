using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SunBloom.Modules.Identity.Domain;

namespace SunBloom.Modules.Identity.Infrastructure;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("refresh_tokens");
        builder.HasKey(token => token.Id);

        // Only the hash is stored, so a database leak yields no usable tokens.
        builder.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(token => token.TokenHash).IsUnique();

        builder.Property(token => token.FamilyId).IsRequired();

        // Reuse detection revokes an entire family at once, so this is a hot lookup.
        builder.HasIndex(token => token.FamilyId);

        builder.Property(token => token.ExpiresAt).IsRequired();
        builder.Property(token => token.CreatedAt).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Only unrevoked tokens are ever queried for validation.
        builder.HasIndex(token => token.UserId)
            .HasFilter("revoked_at IS NULL")
            .HasDatabaseName("ix_refresh_tokens_user_active");
    }
}
