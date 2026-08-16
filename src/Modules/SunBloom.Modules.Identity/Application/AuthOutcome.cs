using SunBloom.Modules.Identity.Contracts;

namespace SunBloom.Modules.Identity.Application;

internal enum AuthError
{
    EmailAlreadyRegistered,
    InvalidCredentials,
    InvalidRefreshToken,
}

/// <summary>
/// Result of an auth operation. Failure is an expected outcome here, not an exception —
/// a wrong password is normal traffic, and exceptions for control flow would bury it.
/// </summary>
internal sealed record AuthOutcome(AuthResponse? Response, AuthError? Error)
{
    public static AuthOutcome Success(AuthResponse response) => new(response, null);

    public static AuthOutcome Failure(AuthError error) => new(null, error);
}
