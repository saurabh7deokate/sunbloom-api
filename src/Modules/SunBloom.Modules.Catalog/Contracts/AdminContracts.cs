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

/// <param name="Slug">The rejected slug — still occupied, so it can never be recreated.</param>
/// <param name="Notes">Why it was rejected. Fed back into the next generation prompt.</param>
public sealed record RejectedSkill(string Slug, string Name, string? Notes);

/// <summary>
/// Every slug in the graph, whatever its review state, plus why the rejected ones were.
/// </summary>
/// <remarks>
/// The generator needs this to avoid re-proposing content. Approved and pending skills
/// are already reachable through the tree and the review queue, but *rejected* ones are
/// in neither — so without this a rejected skill is proposed again on every run. The API
/// blocks the write with a 409, so nothing corrupts, but each repeat silently costs a
/// slot in the batch and a moment of the reviewer's attention.
/// <para>
/// Returning the rejection notes is what turns review into a feedback loop: the reason a
/// human gave for rejecting something becomes an instruction in the next prompt, instead
/// of a note nobody reads again.
/// </para>
/// </remarks>
public sealed record CatalogSlugIndex(
    IReadOnlyList<string> AllSlugs,
    IReadOnlyList<RejectedSkill> Rejected);
