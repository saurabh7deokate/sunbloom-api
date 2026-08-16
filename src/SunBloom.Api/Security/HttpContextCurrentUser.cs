using System.Security.Claims;
using SunBloom.SharedKernel.Ownership;

namespace SunBloom.Api.Security;

/// <summary>Reads the acting user from the validated bearer token.</summary>
/// <remarks>
/// Returns <c>null</c> rather than <see cref="Guid.Empty" /> when unauthenticated.
/// EF global query filters will use this to scope personal data, and a filter comparing
/// against an empty Guid would silently match nothing instead of failing loudly.
/// </remarks>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    // Literal rather than JwtRegisteredClaimNames.Sub: the host does not reference the
    // JWT package directly, and relying on a transitive reference is fragile.
    private const string SubjectClaim = "sub";

    public Guid? UserId
    {
        get
        {
            var principal = accessor.HttpContext?.User;

            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var value = principal.FindFirstValue(SubjectClaim)
                ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => UserId is not null;
}
