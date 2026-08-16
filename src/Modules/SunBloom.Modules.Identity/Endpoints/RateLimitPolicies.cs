namespace SunBloom.Modules.Identity.Endpoints;

/// <summary>
/// Rate limit policy names. Defined here, applied by the host.
/// </summary>
/// <remarks>
/// ADR-0011 declines full ASP.NET Core Identity, which means no built-in account
/// lockout. Rate limiting on the auth endpoints is the partial mitigation, and is a
/// baseline control in ARCHITECTURE.md §4.3 regardless.
/// </remarks>
internal static class RateLimitPolicies
{
    public const string Auth = "auth";
}
