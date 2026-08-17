using Microsoft.Extensions.Logging;

namespace SunBloom.ContentGenerator;

/// <summary>Source-generated log messages for the generator CLI.</summary>
internal static partial class GeneratorLog
{
    [LoggerMessage(EventId = 3000, Level = LogLevel.Warning,
        Message = "Attempt {Attempt}/{MaxAttempts} failed ({Reason}); retrying in {Seconds:F0}s")]
    public static partial void RetryingAfterBackoff(
        ILogger logger, int attempt, int maxAttempts, double seconds, string reason);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Warning,
        Message = "Attempt {Attempt}: model returned unparseable JSON — {Reason}")]
    public static partial void UnparseableResponse(ILogger logger, int attempt, string reason);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Error,
        Message = "Sign-in failed with HTTP {StatusCode}. Check SunBloom:Email and SunBloom:Password in user-secrets.")]
    public static partial void SignInFailed(ILogger logger, int statusCode);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Error,
        Message = "Account {Email} signed in but lacks the ContentAdmin role. Add it to Bootstrap:ContentAdminEmails and restart the API.")]
    public static partial void MissingContentAdminRole(ILogger logger, string email);

    [LoggerMessage(EventId = 3010, Level = LogLevel.Information,
        Message = "Generating children of '{ParentSlug}' ({ExistingCount} skills already in the graph)")]
    public static partial void GeneratingChildren(ILogger logger, string parentSlug, int existingCount);

    [LoggerMessage(EventId = 3011, Level = LogLevel.Information,
        Message = "Model proposed {Count} skill(s)")]
    public static partial void ModelProposed(ILogger logger, int count);

    [LoggerMessage(EventId = 3014, Level = LogLevel.Information,
        Message = "Feeding back {Count} previous rejection(s) so they are not re-proposed")]
    public static partial void FeedingBackRejections(ILogger logger, int count);

    [LoggerMessage(EventId = 3012, Level = LogLevel.Information,
        Message = "  created {Slug} ({ReviewState})")]
    public static partial void SkillCreated(ILogger logger, string slug, string reviewState);

    [LoggerMessage(EventId = 3013, Level = LogLevel.Warning,
        Message = "  skipped {Slug}: {Reason}")]
    public static partial void SkillSkipped(ILogger logger, string slug, string reason);

    [LoggerMessage(EventId = 3020, Level = LogLevel.Information,
        Message = "Proposing prerequisites across {Count} approved skill(s)")]
    public static partial void GeneratingPrerequisites(ILogger logger, int count);

    [LoggerMessage(EventId = 3021, Level = LogLevel.Information,
        Message = "  {FromSlug} -> {ToSlug}")]
    public static partial void PrerequisiteAdded(ILogger logger, string fromSlug, string toSlug);

    [LoggerMessage(EventId = 3022, Level = LogLevel.Warning,
        Message = "  rejected {FromSlug} -> {ToSlug}: would create a cycle")]
    public static partial void PrerequisiteCycleRejected(ILogger logger, string fromSlug, string toSlug);

    [LoggerMessage(EventId = 3030, Level = LogLevel.Information,
        Message = "Done: {Created} created, {Skipped} skipped, {Rejected} rejected as cyclic. Review at /admin/skills/pending.")]
    public static partial void RunSummary(ILogger logger, int created, int skipped, int rejected);

    [LoggerMessage(EventId = 3031, Level = LogLevel.Error, Message = "{Message}")]
    public static partial void Fatal(ILogger logger, string message);
}
