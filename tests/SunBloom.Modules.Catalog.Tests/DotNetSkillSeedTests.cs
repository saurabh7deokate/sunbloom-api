using SunBloom.Modules.Catalog.Domain;
using SunBloom.Modules.Catalog.Infrastructure;

namespace SunBloom.Modules.Catalog.Tests;

/// <summary>
/// Guards the hand-authored seed graph.
/// </summary>
/// <remarks>
/// The seeder validates at startup, but a broken seed would only surface as a failed
/// launch. These tests fail in CI instead, and will matter more as the graph grows from
/// ~35 nodes toward the several hundred a real career path needs.
/// </remarks>
public class DotNetSkillSeedTests
{
    private static readonly HashSet<string> Slugs =
        DotNetSkillSeed.Skills.Select(skill => skill.Slug).ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Slugs_are_unique()
    {
        var duplicates = DotNetSkillSeed.Skills
            .GroupBy(skill => skill.Slug, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.True(duplicates.Count == 0, $"Duplicate slugs: {string.Join(", ", duplicates)}");
    }

    [Fact]
    public void Every_parent_slug_exists()
    {
        var missing = DotNetSkillSeed.Skills
            .Where(skill => skill.ParentSlug is not null && !Slugs.Contains(skill.ParentSlug))
            .Select(skill => $"{skill.Slug} -> {skill.ParentSlug}")
            .ToList();

        Assert.True(missing.Count == 0, $"Skills reference missing parents: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Every_edge_references_a_known_skill()
    {
        var missing = DotNetSkillSeed.Edges
            .Where(edge => !Slugs.Contains(edge.FromSlug) || !Slugs.Contains(edge.ToSlug))
            .Select(edge => $"{edge.FromSlug} -> {edge.ToSlug}")
            .ToList();

        Assert.True(missing.Count == 0, $"Edges reference unknown skills: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Exactly_one_root_exists()
    {
        var roots = DotNetSkillSeed.Skills.Where(skill => skill.ParentSlug is null).ToList();

        Assert.Single(roots);
        Assert.Equal(DotNetSkillSeed.RootSlug, roots[0].Slug);
    }

    [Fact]
    public void Containment_hierarchy_has_no_cycles()
    {
        var parents = DotNetSkillSeed.Skills
            .Where(skill => skill.ParentSlug is not null)
            .ToDictionary(skill => skill.Slug, skill => skill.ParentSlug!, StringComparer.Ordinal);

        foreach (var slug in parents.Keys)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var current = slug;

            while (parents.TryGetValue(current, out var parent))
            {
                Assert.True(seen.Add(current), $"Containment cycle involving '{slug}'.");
                current = parent;
            }
        }
    }

    [Fact]
    public void Prerequisite_edges_are_acyclic()
    {
        // The check that matters. Individually valid edges can still be collectively
        // cyclic, which is why this runs over the whole set.
        var ids = Slugs.ToDictionary(slug => slug, _ => Guid.CreateVersion7(), StringComparer.Ordinal);

        var edges = DotNetSkillSeed.Edges
            .Where(edge => edge.Type == SkillRelationshipType.Prerequisite)
            .GroupBy(edge => ids[edge.FromSlug])
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Guid>)[.. group.Select(edge => ids[edge.ToSlug])]);

        var cycle = PrerequisiteGraph.FindAnyCycle(edges);
        var slugsById = ids.ToDictionary(pair => pair.Value, pair => pair.Key);

        Assert.True(
            cycle.Count == 0,
            $"Prerequisite cycle: {string.Join(" -> ", cycle.Select(id => slugsById[id]))}");
    }

    [Fact]
    public void No_skill_is_its_own_prerequisite()
    {
        var selfEdges = DotNetSkillSeed.Edges
            .Where(edge => string.Equals(edge.FromSlug, edge.ToSlug, StringComparison.Ordinal))
            .Select(edge => edge.FromSlug)
            .ToList();

        Assert.True(selfEdges.Count == 0, $"Self-referencing edges: {string.Join(", ", selfEdges)}");
    }

    [Fact]
    public void Seed_is_large_enough_to_exercise_gap_ranking()
    {
        // Sub-slice 1.8 ranks gaps using unlockCount. A trivial graph would make the
        // ranking look sensible for the wrong reason.
        Assert.True(DotNetSkillSeed.Skills.Count >= 30);
        Assert.True(
            DotNetSkillSeed.Edges.Count(edge => edge.Type == SkillRelationshipType.Prerequisite) >= 15);
    }
}
