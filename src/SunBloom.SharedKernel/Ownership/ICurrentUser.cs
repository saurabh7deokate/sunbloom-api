namespace SunBloom.SharedKernel.Ownership;

/// <summary>
/// The user this request is acting as.
/// </summary>
/// <remarks>
/// Lives in SharedKernel rather than the Identity module's contracts because ownership
/// is cross-cutting infrastructure, like <c>IClock</c>. If it lived in Identity, every
/// module needing to scope a query would have to depend on Identity — coupling that
/// ADR-0001 exists to prevent.
/// <para>
/// EF global query filters read <see cref="UserId" /> to scope personal data, so an
/// implementation must never guess: return <c>null</c> when unauthenticated rather than
/// a default or empty <see cref="Guid" />.
/// </para>
/// </remarks>
public interface ICurrentUser
{
    Guid? UserId { get; }

    bool IsAuthenticated { get; }
}
