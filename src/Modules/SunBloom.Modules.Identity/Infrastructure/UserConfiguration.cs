using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SunBloom.Modules.Identity.Domain;

namespace SunBloom.Modules.Identity.Infrastructure;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("users");
        builder.HasKey(user => user.Id);

        // citext makes uniqueness case-insensitive in the database rather than relying
        // on every call site to normalize first. DATABASE.md §2.
        builder.Property(user => user.Email)
            .HasColumnType("citext")
            .IsRequired();

        builder.HasIndex(user => user.Email).IsUnique();

        builder.Property(user => user.PasswordHash).IsRequired();
        builder.Property(user => user.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(user => user.TimeZone).HasMaxLength(100).IsRequired().HasDefaultValue("UTC");

        builder.Property(user => user.EmailConfirmed).HasDefaultValue(false);
        builder.Property(user => user.IsActive).HasDefaultValue(true);

        builder.Property(user => user.CreatedAt).IsRequired();
        builder.Property(user => user.UpdatedAt).IsRequired();
    }
}
