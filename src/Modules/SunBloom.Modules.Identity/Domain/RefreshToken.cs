namespace SunBloom.Modules.Identity.Domain;

/// <summary>
/// One link in a refresh-token rotation chain.
/// </summary>
/// <remarks>
/// Only the SHA-256 hash of the token is stored, so a database leak does not yield
/// usable tokens. Every token issued from the same original login shares a
/// <see cref="FamilyId" />: if a token that has already been rotated is presented
/// again, it was almost certainly stolen, and the whole family is revoked rather than
/// just that one link.
/// </remarks>
internal sealed class RefreshToken
{
    private RefreshToken() => TokenHash = null!;

    private RefreshToken(Guid userId, string tokenHash, Guid familyId, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        TokenHash = tokenHash;
        FamilyId = familyId;
        ExpiresAt = expiresAt;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; }

    /// <summary>Groups every token descended from one login.</summary>
    public Guid FamilyId { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>The token that superseded this one, when it was rotated normally.</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Starts a new family. Used at login.</summary>
    public static RefreshToken IssueNewFamily(Guid userId, string tokenHash, DateTimeOffset expiresAt, DateTimeOffset now) =>
        new(userId, tokenHash, Guid.CreateVersion7(), expiresAt, now);

    /// <summary>Continues an existing family. Used when rotating.</summary>
    public static RefreshToken IssueInFamily(Guid userId, string tokenHash, Guid familyId, DateTimeOffset expiresAt, DateTimeOffset now) =>
        new(userId, tokenHash, familyId, expiresAt, now);

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    public void RotateTo(RefreshToken replacement, DateTimeOffset now)
    {
        Revoke(now);
        ReplacedByTokenId = replacement.Id;
    }
}
