using Microsoft.Extensions.Logging;

namespace SunBloom.Modules.Identity.Application;

/// <summary>
/// Source-generated log messages: no boxing, no allocation when the level is disabled.
/// </summary>
/// <remarks>
/// User IDs only — never email addresses or tokens. ARCHITECTURE.md §4.3 forbids PII
/// in logs, and auth logs are exactly where it would otherwise leak.
/// </remarks>
internal static partial class AuthLog
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "Registered user {UserId}")]
    public static partial void UserRegistered(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Upgraded password hash for user {UserId}")]
    public static partial void PasswordHashUpgraded(ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Refresh token reuse detected for user {UserId}; revoked token family {FamilyId}")]
    public static partial void RefreshTokenReuseDetected(ILogger logger, Guid userId, Guid familyId);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "Logged out user {UserId}")]
    public static partial void UserLoggedOut(ILogger logger, Guid userId);
}
