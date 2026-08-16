namespace SunBloom.Modules.Catalog.Contracts;

/// <summary>A skill reduced to what a list or chip needs.</summary>
public sealed record SkillSummary(Guid Id, string Slug, string Name, string Kind);

/// <summary>A node in the containment tree, with its children nested.</summary>
public sealed record SkillTreeNode(
    Guid Id,
    string Slug,
    string Name,
    string? Description,
    string Kind,
    IReadOnlyList<SkillTreeNode> Children);

/// <summary>
/// A single skill with the edges that cross the containment tree.
/// </summary>
/// <param name="Prerequisites">Skills that must be learned before this one.</param>
/// <param name="Unlocks">
/// Skills this one is a prerequisite for. Surfaced because it is what makes gap ranking
/// non-obvious — a moderate gap unlocking four skills outranks a larger one unlocking
/// none (SCORING.md §3.2).
/// </param>
public sealed record SkillDetail(
    Guid Id,
    string Slug,
    string Name,
    string? Description,
    string Kind,
    Guid? ParentSkillId,
    IReadOnlyList<SkillSummary> Prerequisites,
    IReadOnlyList<SkillSummary> Unlocks,
    IReadOnlyList<SkillSummary> Related);
