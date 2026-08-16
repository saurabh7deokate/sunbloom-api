namespace SunBloom.Modules.Identity.Contracts;

/// <summary>Public request and response shapes for authentication.</summary>
/// <remarks>
/// These are the module's HTTP contract and are generated into the Angular client from
/// OpenAPI (ADR-0007). They are deliberately separate from the domain entities — no EF
/// type is ever serialized.
/// </remarks>
public sealed record RegisterRequest(
    string Email,
    string Password,
    string DisplayName,
    string? TimeZone);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

/// <summary>
/// Issued on register, login, and refresh.
/// </summary>
/// <param name="AccessToken">Short-lived JWT.</param>
/// <param name="RefreshToken">
/// Opaque, single-use. Returned in full exactly once — only its hash is stored, so it
/// cannot be recovered afterwards.
/// </param>
/// <param name="ExpiresInSeconds">Lifetime of <paramref name="AccessToken" />.</param>
public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds,
    UserResponse User);

public sealed record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string TimeZone);
