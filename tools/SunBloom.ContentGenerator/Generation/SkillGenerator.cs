using Microsoft.Extensions.Logging;
using SunBloom.ContentGenerator.Ai;
using SunBloom.ContentGenerator.Api;

namespace SunBloom.ContentGenerator.Generation;

internal sealed record RunTotals(int Created, int Skipped, int CycleRejected);

/// <summary>
/// Orchestrates hierarchical, reviewable content generation.
/// </summary>
/// <remarks>
/// Generation is deliberately one level at a time (ADR-0005): generate the children of a
/// node, stop, let a human approve them, then generate the next level under what was
/// approved. The alternative — emitting a whole tree in one pass — produces hundreds of
/// nodes to review at once, which leads to rubber-stamping and defeats the human gate the
/// whole content strategy rests on. It also means a bad branch is caught before its
/// subtree exists.
/// </remarks>
internal sealed class SkillGenerator(
    IStructuredCompletion completion,
    SunBloomAdminClient api,
    ILogger<SkillGenerator> logger)
{
    public async Task<RunTotals> GenerateChildrenAsync(string parentSlug, int target, CancellationToken ct)
    {
        var existing = await api.GetAllSkillsAsync(ct);
        var parent = existing.FirstOrDefault(skill =>
            string.Equals(skill.Slug, parentSlug, StringComparison.OrdinalIgnoreCase));

        if (parent is null)
        {
            GeneratorLog.Fatal(logger, $"No skill with slug '{parentSlug}'. Create it first, or check the spelling.");

            return new RunTotals(0, 0, 0);
        }

        GeneratorLog.GeneratingChildren(logger, parentSlug, existing.Count);

        var prompt = SkillPrompts.ChildrenPrompt(
            parent.Name,
            parent.Description ?? parent.Name,
            parent.Kind,
            [.. existing.Select(skill => skill.Slug)],
            target);

        var result = await completion.CompleteAsync<GeneratedSkillSet>(
            SkillPrompts.SystemInstruction, prompt, SkillPrompts.SkillSetSchema, ct);

        if (!result.IsSuccess || result.Value is null)
        {
            GeneratorLog.Fatal(logger, $"Generation failed: {result.Error}");

            return new RunTotals(0, 0, 0);
        }

        GeneratorLog.ModelProposed(logger, result.Value.Skills.Count);

        var created = 0;
        var skipped = 0;

        foreach (var skill in result.Value.Skills)
        {
            // The API is the authority on duplicates — it holds the unique index. Checking
            // locally first would still race, and would duplicate the rule in two places.
            var (createdSkill, outcome) = await api.CreateSkillAsync(
                skill.Slug, skill.Name, skill.Kind, skill.Description,
                parent.Slug, completion.ModelId, SkillPrompts.Version, ct);

            if (createdSkill is not null)
            {
                created++;
                GeneratorLog.SkillCreated(logger, createdSkill.Slug, createdSkill.ReviewState);
            }
            else
            {
                skipped++;
                GeneratorLog.SkillSkipped(
                    logger,
                    skill.Slug,
                    outcome.IsDuplicate ? "already exists" : outcome.Detail ?? $"HTTP {outcome.StatusCode}");
            }
        }

        GeneratorLog.RunSummary(logger, created, skipped, 0);

        return new RunTotals(created, skipped, 0);
    }

    /// <summary>
    /// Proposes prerequisite edges across approved skills.
    /// </summary>
    /// <remarks>
    /// Runs after approval rather than alongside creation, because prerequisites cross
    /// containment branches and only make sense once both endpoints exist and are real.
    /// Cycles are rejected by the API, not filtered here — the graph is the authority,
    /// and a model asked not to produce cycles will still occasionally produce them.
    /// </remarks>
    public async Task<RunTotals> GeneratePrerequisitesAsync(CancellationToken ct)
    {
        var skills = await api.GetAllSkillsAsync(ct);

        // Areas are groupings, never assessed directly, so a prerequisite on one carries
        // no meaning for a learner.
        var assessable = skills
            .Where(skill => !string.Equals(skill.Kind, "Area", StringComparison.Ordinal))
            .ToList();

        if (assessable.Count < 2)
        {
            GeneratorLog.Fatal(logger, "Need at least two non-Area skills before prerequisites are meaningful.");

            return new RunTotals(0, 0, 0);
        }

        GeneratorLog.GeneratingPrerequisites(logger, assessable.Count);

        var lines = assessable
            .Select(skill => $"{skill.Slug} — {skill.Name}: {skill.Description ?? "(no description)"}")
            .ToList();

        var result = await completion.CompleteAsync<GeneratedPrerequisiteSet>(
            SkillPrompts.SystemInstruction, SkillPrompts.PrerequisitePrompt(lines),
            SkillPrompts.PrerequisiteSetSchema, ct);

        if (!result.IsSuccess || result.Value is null)
        {
            GeneratorLog.Fatal(logger, $"Generation failed: {result.Error}");

            return new RunTotals(0, 0, 0);
        }

        GeneratorLog.ModelProposed(logger, result.Value.Prerequisites.Count);

        var known = assessable.Select(skill => skill.Slug).ToHashSet(StringComparer.Ordinal);
        var added = 0;
        var skipped = 0;
        var rejected = 0;

        foreach (var edge in result.Value.Prerequisites)
        {
            if (!known.Contains(edge.FromSlug) || !known.Contains(edge.ToSlug))
            {
                // The model invented a slug. Common enough to expect, cheap to drop.
                skipped++;
                GeneratorLog.SkillSkipped(logger, $"{edge.FromSlug} -> {edge.ToSlug}", "unknown slug");
                continue;
            }

            var outcome = await api.AddPrerequisiteAsync(edge.FromSlug, edge.ToSlug, ct);

            if (outcome.Success)
            {
                added++;
                GeneratorLog.PrerequisiteAdded(logger, edge.FromSlug, edge.ToSlug);
            }
            else if (outcome.IsCycleRejection)
            {
                rejected++;
                GeneratorLog.PrerequisiteCycleRejected(logger, edge.FromSlug, edge.ToSlug);
            }
            else
            {
                skipped++;
                GeneratorLog.SkillSkipped(
                    logger, $"{edge.FromSlug} -> {edge.ToSlug}",
                    outcome.Detail ?? $"HTTP {outcome.StatusCode}");
            }
        }

        GeneratorLog.RunSummary(logger, added, skipped, rejected);

        return new RunTotals(added, skipped, rejected);
    }
}
