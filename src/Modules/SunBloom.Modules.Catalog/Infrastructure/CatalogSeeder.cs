using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SunBloom.Modules.Catalog.Domain;
using SunBloom.SharedKernel.Modules;
using SunBloom.SharedKernel.Time;

namespace SunBloom.Modules.Catalog.Infrastructure;

/// <summary>Seeds the hand-authored .NET skill graph. Idempotent by slug.</summary>
internal sealed class CatalogSeeder(CatalogDbContext db, IClock clock, ILogger<CatalogSeeder> logger)
    : IModuleSeeder
{
    public string ModuleName => "Catalog";

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        var existingSlugs = await db.Skills
            .Select(skill => skill.Slug)
            .ToDictionaryAsync(slug => slug, StringComparer.Ordinal, cancellationToken);

        var added = await SeedSkillsAsync(existingSlugs, now, cancellationToken);
        var idsBySlug = await db.Skills.ToDictionaryAsync(s => s.Slug, s => s.Id, StringComparer.Ordinal, cancellationToken);

        ValidateSeedIsAcyclic(idsBySlug);

        var addedEdges = await SeedEdgesAsync(idsBySlug, now, cancellationToken);

        if (added > 0 || addedEdges > 0)
        {
            CatalogLog.Seeded(logger, added, addedEdges);
        }
    }

    private async Task<int> SeedSkillsAsync(
        Dictionary<string, string> existingSlugs,
        DateTimeOffset now,
        CancellationToken ct)
    {
        // Two passes: parents must exist before children can reference their IDs.
        var pending = DotNetSkillSeed.Skills.Where(seed => !existingSlugs.ContainsKey(seed.Slug)).ToList();

        if (pending.Count == 0)
        {
            return 0;
        }

        var idsBySlug = await db.Skills.ToDictionaryAsync(s => s.Slug, s => s.Id, StringComparer.Ordinal, ct);
        var remaining = new List<DotNetSkillSeed.SeedSkill>(pending);

        while (remaining.Count > 0)
        {
            var placeable = remaining
                .Where(seed => seed.ParentSlug is null || idsBySlug.ContainsKey(seed.ParentSlug))
                .ToList();

            if (placeable.Count == 0)
            {
                throw new InvalidOperationException(
                    "Seed data references parent slugs that do not exist: "
                    + string.Join(", ", remaining.Select(seed => $"{seed.Slug} -> {seed.ParentSlug}")));
            }

            foreach (var seed in placeable)
            {
                var skill = Skill.Create(
                    seed.Slug,
                    seed.Name,
                    seed.Kind,
                    ContentProvenance.HumanAuthored(),
                    now,
                    seed.Description,
                    seed.ParentSlug is null ? null : idsBySlug[seed.ParentSlug]);

                db.Skills.Add(skill);
                idsBySlug[seed.Slug] = skill.Id;
                remaining.Remove(seed);
            }
        }

        await db.SaveChangesAsync(ct);

        return pending.Count;
    }

    private async Task<int> SeedEdgesAsync(
        Dictionary<string, Guid> idsBySlug,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var existing = await db.SkillRelationships
            .Select(edge => new { edge.FromSkillId, edge.ToSkillId, edge.Type })
            .ToListAsync(ct);

        var known = existing
            .Select(edge => (edge.FromSkillId, edge.ToSkillId, edge.Type))
            .ToHashSet();

        var added = 0;

        foreach (var seed in DotNetSkillSeed.Edges)
        {
            if (!idsBySlug.TryGetValue(seed.FromSlug, out var from)
                || !idsBySlug.TryGetValue(seed.ToSlug, out var to)
                || known.Contains((from, to, seed.Type)))
            {
                continue;
            }

            db.SkillRelationships.Add(SkillRelationship.Create(from, to, seed.Type, now));
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return added;
    }

    /// <summary>
    /// Checks the seed prerequisites as a set before writing.
    /// </summary>
    /// <remarks>
    /// Per-edge checks in <c>SkillGraphService</c> cannot catch a batch that is only
    /// collectively cyclic — each edge is individually fine, and the cycle closes only
    /// once all of them exist.
    /// </remarks>
    private static void ValidateSeedIsAcyclic(Dictionary<string, Guid> idsBySlug)
    {
        var edges = DotNetSkillSeed.Edges
            .Where(edge => edge.Type == SkillRelationshipType.Prerequisite)
            .Where(edge => idsBySlug.ContainsKey(edge.FromSlug) && idsBySlug.ContainsKey(edge.ToSlug))
            .GroupBy(edge => idsBySlug[edge.FromSlug])
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Guid>)[.. group.Select(edge => idsBySlug[edge.ToSlug])]);

        var cycle = PrerequisiteGraph.FindAnyCycle(edges);

        if (cycle.Count > 0)
        {
            var slugsById = idsBySlug.ToDictionary(pair => pair.Value, pair => pair.Key);
            var path = string.Join(" -> ", cycle.Select(id => slugsById.GetValueOrDefault(id, id.ToString())));

            throw new InvalidOperationException($"Seed prerequisite edges contain a cycle: {path}");
        }
    }
}

internal static partial class CatalogLog
{
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "Catalog seeded: {SkillCount} skills, {EdgeCount} relationships")]
    public static partial void Seeded(ILogger logger, int skillCount, int edgeCount);
}
