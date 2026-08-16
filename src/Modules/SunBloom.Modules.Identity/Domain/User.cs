namespace SunBloom.Modules.Identity.Domain;

/// <summary>A SunBloom account.</summary>
/// <remarks>
/// Time is passed in rather than read, so account lifecycle is testable at a chosen
/// instant. See <c>NoAmbientTimeTests</c>.
/// </remarks>
internal sealed class User
{
    private User()
    {
        // EF Core materialization.
        Email = null!;
        PasswordHash = null!;
        DisplayName = null!;
        TimeZone = null!;
    }

    private User(Guid id, string email, string passwordHash, string displayName, string timeZone, DateTimeOffset now)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        DisplayName = displayName;
        TimeZone = timeZone;
        EmailConfirmed = false;
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    /// <summary>Stored as <c>citext</c>, so uniqueness is case-insensitive in the database.</summary>
    public string Email { get; private set; }

    public string PasswordHash { get; private set; }

    public string DisplayName { get; private set; }

    public string TimeZone { get; private set; }

    public bool EmailConfirmed { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static User Register(
        string email,
        string passwordHash,
        string displayName,
        string timeZone,
        DateTimeOffset now) =>
        new(Guid.CreateVersion7(), email.Trim(), passwordHash, displayName.Trim(), timeZone, now);

    /// <summary>
    /// Replaces the stored hash. Called when the hasher reports the existing hash used
    /// outdated parameters, so iteration counts improve as users log in.
    /// </summary>
    public void UpgradePasswordHash(string passwordHash, DateTimeOffset now)
    {
        PasswordHash = passwordHash;
        UpdatedAt = now;
    }
}
