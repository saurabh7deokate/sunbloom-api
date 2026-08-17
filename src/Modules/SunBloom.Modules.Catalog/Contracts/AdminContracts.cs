namespace SunBloom.Modules.Catalog.Contracts;

/// <summary>
/// Where a piece of generated content came from.
/// </summary>
/// <remarks>
/// Omit on a request to mark content human-authored, which is served immediately.
/// Supply it and the content enters as a draft requiring review (ADR-0005). This is why
/// provenance is a request field rather than something the server infers: the server
/// cannot tell a hand-written skill from a generated one, and guessing wrong would
/// either bypass review or bury hand-authored content in a queue.
/// </remarks>
public sealed record AiGenerationInfo(string Model, string PromptVersion);

public sealed record CreateSkillRequest(
    string Slug,
    string Name,
    string Kind,
    string? Description,
    string? ParentSlug,
    AiGenerationInfo? Generation);

public sealed record CreateSkillRelationshipRequest(
    string FromSlug,
    string ToSlug,
    string Type,
    decimal? Strength);

public sealed record ReviewDecisionRequest(string? Notes);

/// <summary>A skill as an administrator sees it — including unapproved content and provenance.</summary>
public sealed record SkillAdminView(
    Guid Id,
    string Slug,
    string Name,
    string? Description,
    string Kind,
    string? ParentSlug,
    bool IsActive,
    string ReviewState,
    string GenerationSource,
    string? GeneratorModel,
    string? GeneratorPromptVersion,
    DateTimeOffset? GeneratedAt,
    DateTimeOffset? ReviewedAt,
    string? ReviewNotes);

/// <summary>
/// One page of the review queue.
/// </summary>
/// <param name="Depth">
/// Containment depth of the shallowest pending item. Review proceeds top-down — areas
/// before their children — so a queue is worked one depth at a time (ADR-0005).
/// </param>
public sealed record PendingReviewPage(
    IReadOnlyList<SkillAdminView> Items,
    int TotalPending,
    int Depth);
