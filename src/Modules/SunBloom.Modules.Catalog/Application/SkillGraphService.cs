using Microsoft.EntityFrameworkCore;
using SunBloom.Modules.Catalog.Contracts;
using SunBloom.Modules.Catalog.Domain;
using SunBloom.Modules.Catalog.Infrastructure;
using SunBloom.SharedKernel.Time;

namespace SunBloom.Modules.Catalog.Application;

/// <summary>Reads and mutations over the skill graph.</summary>
internal sealed class SkillGraphService(CatalogDbContext db, IClock clock)
{
    /// <summary>
    /// The containment tree. With no root, returns every approved root and its
    /// descendants; with a root slug, returns that subtree only.
    /// </summary>
    public async Task<IReadOnlyList<SkillTreeNode>> GetTreeAsync(string? rootSlug, CancellationToken ct)
    {
        var skills = rootSlug is null
            ? await ApprovedSkills().ToListAsync(ct)
            : await GetSubtreeAsync(rootSlug, ct);

        if (skills.Count == 0)
        {
            return [];
        }

        var rootParentId = rootSlug is null
            ? null
            : skills.FirstOrDefault(s => string.Equals(s.Slug, rootSlug, StringComparison.OrdinalIgnoreCase))?.ParentSkillId;

        return BuildTree(skills, rootParentId);
    }

    /// <summary>
    /// Descendants of a skill, via a recursive CTE.
    /// </summary>
    /// <remarks>
    /// Done in the database rather than by loading the table and filtering in memory:
    /// the graph is expected to reach several hundred nodes per career path, and a
    /// subtree is usually a small fraction of it. Adjacency plus a recursive CTE also
    /// avoids the closure table until traversal is measured slow (DATABASE.md §3.2).
    /// </remarks>
    public async Task<List<Skill>> GetSubtreeAsync(string rootSlug, CancellationToken ct)
    {
        // Alias to "Value" because EF's scalar SqlQuery binds a single column by that name.
        var ids = await db.Database
            .SqlQuery<Guid>($"""
                WITH RECURSIVE subtree AS (
                    SELECT id
                    FROM catalog.skills
                    WHERE slug = {rootSlug} AND is_active AND review_state = 'Approved'

                    UNION ALL

                    SELECT child.id
                    FROM catalog.skills child
                    JOIN subtree parent ON child.parent_skill_id = parent.id
                    WHERE child.is_active AND child.review_state = 'Approved'
                )
                SELECT id AS "Value" FROM subtree
                """)
            .ToListAsync(ct);

        if (ids.Count == 0)
        {
            return [];
        }

        return await ApprovedSkills().Where(skill => ids.Contains(skill.Id)).ToListAsync(ct);
    }

    public async Task<SkillDetail?> GetDetailAsync(string slug, CancellationToken ct)
    {
        // Normalized once, outside the expression tree: EF cannot translate
        // string.Equals with a StringComparison, and slugs are stored lowercase.
        var normalizedSlug = slug.Trim().ToLowerInvariant();

        var skill = await ApprovedSkills()
            .FirstOrDefaultAsync(candidate => candidate.Slug == normalizedSlug, ct);

        if (skill is null)
        {
            return null;
        }

        var edges = await db.SkillRelationships
            .Where(edge => edge.FromSkillId == skill.Id || edge.ToSkillId == skill.Id)
            .ToListAsync(ct);

        var relatedIds = edges
            .SelectMany(edge => new[] { edge.FromSkillId, edge.ToSkillId })
            .Where(id => id != skill.Id)
            .Distinct()
            .ToList();

        var lookup = await ApprovedSkills()
            .Where(candidate => relatedIds.Contains(candidate.Id))
            .ToDictionaryAsync(candidate => candidate.Id, ct);

        IReadOnlyList<SkillSummary> Summaries(IEnumerable<Guid> ids) =>
            [.. ids.Where(lookup.ContainsKey).Select(id => ToSummary(lookup[id]))];

        return new SkillDetail(
            skill.Id,
            skill.Slug,
            skill.Name,
            skill.Description,
            skill.Kind.ToString(),
            skill.ParentSkillId,
            Prerequisites: Summaries(edges
                .Where(e => e.Type == SkillRelationshipType.Prerequisite && e.ToSkillId == skill.Id)
                .Select(e => e.FromSkillId)),
            Unlocks: Summaries(edges
                .Where(e => e.Type == SkillRelationshipType.Prerequisite && e.FromSkillId == skill.Id)
                .Select(e => e.ToSkillId)),
            Related: Summaries(edges
                .Where(e => e.Type is SkillRelationshipType.Related or SkillRelationshipType.Alternative)
                .Select(e => e.FromSkillId == skill.Id ? e.ToSkillId : e.FromSkillId)));
    }

    /// <summary>
    /// Adds an edge, rejecting a prerequisite that would create a cycle.
    /// </summary>
    /// <remarks>
    /// The check reads the whole prerequisite edge set, which is fine at this scale and
    /// far simpler than an incremental structure. PostgreSQL cannot enforce acyclicity
    /// declaratively, so this is the only guard.
    /// </remarks>
    public async Task<SkillRelationship> AddRelationshipAsync(
        Guid fromSkillId,
        Guid toSkillId,
        SkillRelationshipType type,
        CancellationToken ct,
        decimal strength = 1.0m)
    {
        if (type == SkillRelationshipType.Prerequisite)
        {
            var edges = await LoadPrerequisiteEdgesAsync(ct);

            if (PrerequisiteGraph.WouldCreateCycle(edges, fromSkillId, toSkillId))
            {
                throw new InvalidOperationException(
                    $"Adding prerequisite {fromSkillId} -> {toSkillId} would create a cycle. "
                    + "Prerequisite edges must form a DAG or nothing in the loop is ever unblocked.");
            }
        }

        var relationship = SkillRelationship.Create(fromSkillId, toSkillId, type, clock.UtcNow, strength);

        db.SkillRelationships.Add(relationship);
        await db.SaveChangesAsync(ct);

        return relationship;
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LoadPrerequisiteEdgesAsync(CancellationToken ct)
    {
        var edges = await db.SkillRelationships
            .Where(edge => edge.Type == SkillRelationshipType.Prerequisite)
            .Select(edge => new { edge.FromSkillId, edge.ToSkillId })
            .ToListAsync(ct);

        return edges
            .GroupBy(edge => edge.FromSkillId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Guid>)[.. group.Select(edge => edge.ToSkillId)]);
    }

    private IQueryable<Skill> ApprovedSkills() =>
        db.Skills
            .AsNoTracking()
            .Where(skill => skill.IsActive && skill.Provenance.ReviewState == ReviewState.Approved);

    private static SkillSummary ToSummary(Skill skill) =>
        new(skill.Id, skill.Slug, skill.Name, skill.Kind.ToString());

    /// <summary>Assembles nested nodes from a flat list in one pass.</summary>
    private static IReadOnlyList<SkillTreeNode> BuildTree(List<Skill> skills, Guid? rootParentId)
    {
        var byParent = skills
            .GroupBy(skill => skill.ParentSkillId)
            .ToDictionary(group => group.Key ?? Guid.Empty, group => group.ToList());

        var presentIds = skills.Select(skill => skill.Id).ToHashSet();

        // A node whose parent was filtered out (unapproved, inactive, or outside the
        // requested subtree) is treated as a root, so filtering never silently drops
        // an entire branch.
        var roots = skills
            .Where(skill => skill.ParentSkillId == rootParentId
                || skill.ParentSkillId is null
                || !presentIds.Contains(skill.ParentSkillId.Value))
            .OrderBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return [.. roots.Select(root => ToNode(root, byParent))];
    }

    private static SkillTreeNode ToNode(Skill skill, Dictionary<Guid, List<Skill>> byParent)
    {
        var children = byParent.TryGetValue(skill.Id, out var found)
            ? found.OrderBy(child => child.Name, StringComparer.OrdinalIgnoreCase)
                .Select(child => ToNode(child, byParent))
                .ToList()
            : [];

        return new SkillTreeNode(
            skill.Id, skill.Slug, skill.Name, skill.Description, skill.Kind.ToString(), children);
    }
}
