namespace SunBloom.SharedKernel.Ownership;

/// <summary>
/// Marks an entity as personal data belonging to exactly one user.
/// </summary>
/// <remarks>
/// SunBloom uses per-user ownership rather than tenant isolation (ADR-0003). Every
/// entity implementing this interface must have an EF Core global query filter bound
/// to the current user — asserted by <c>OwnershipTests</c>, so a new personal entity
/// cannot be added without ownership enforcement even by an author who has never read
/// the ADR.
/// </remarks>
public interface IOwnedByUser
{
    Guid OwnerUserId { get; }
}
