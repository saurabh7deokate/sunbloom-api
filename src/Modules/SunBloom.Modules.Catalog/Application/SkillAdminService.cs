using Microsoft.EntityFrameworkCore;
using SunBloom.Modules.Catalog.Contracts;
using SunBloom.Modules.Catalog.Domain;
using SunBloom.Modules.Catalog.Infrastructure;
using SunBloom.SharedKernel.Time;

namespace SunBloom.Modules.Catalog.Application;

internal enum CatalogError
{
    SlugAlreadyExists,
    SkillNotFound,
    ParentNotFound,
    RelationshipWouldCreateCycle,
    InvalidValue,
    AlreadyReviewed,
}

internal sealed record CatalogOutcome<T>(T? Value, CatalogError? Error, string? Detail)
{
    public static CatalogOutcome<T> Success(T value) => new(value, null, null);

    public static CatalogOutcome<T> Failure(CatalogError error, string detail) => new(default, error, detail);
}

/// <summary>
/// Authoring and review operations over the catalog.
/// </summary>
/// <remarks>
/// Separate from <see cref="SkillGraphService" /> because the queries differ in a way
/// that matters: learner-facing reads are filtered to approved and active content, while
/// every operation here works over *unapproved* content by definition. Sharing one
/// service would mean every query carried an "include drafts" flag, and a flag defaulted
/// wrong is how draft content reaches learners.
/// </remarks>
internal sealed class SkillAdminService(CatalogDbContext db, SkillGraphService graph, IClock clock)
{
    public async Task<CatalogOutcome<SkillAdminView>> CreateSkillAsync(
        CreateSkillRequest request,
        CancellationToken ct)
    {
        if (!Enum.TryParse<SkillKind>(request.Kind, ignoreCase: true, out var kind))
        {
            return CatalogOutcome<SkillAdminView>.Failure(
                CatalogError.InvalidValue,
                $"'{request.Kind}' is not a valid skill kind. Expected one of: {string.Join(", ", Enum.GetNames<SkillKind>())}.");
        }

        var slug = request.Slug.Trim().ToLowerInvariant();

        if (await db.Skills.AnyAsync(skill => skill.Slug == slug, ct))
        {
            return CatalogOutcome<SkillAdminView>.Failure(
                CatalogError.SlugAlreadyExists,
                $"A skill with slug '{slug}' already exists. Skills are global and shared (ADR-0004), so reuse it rather than creating a near-duplicate.");
        }

        Guid? parentId = null;

        if (!string.IsNullOrWhiteSpace(request.ParentSlug))
        {
            var parentSlug = request.ParentSlug.Trim().ToLowerInvariant();

            // Deliberately unfiltered by review state: hierarchical generation creates
            // children under a parent that may itself still be a draft.
            var parent = await db.Skills.FirstOrDefaultAsync(skill => skill.Slug == parentSlug, ct);

            if (parent is null)
            {
                return CatalogOutcome<SkillAdminView>.Failure(
                    CatalogError.ParentNotFound, $"No skill exists with slug '{parentSlug}'.");
            }

            parentId = parent.Id;
        }

        var now = clock.UtcNow;

        var provenance = request.Generation is { } generation
            ? ContentProvenance.AiGenerated(generation.Model, generation.PromptVersion, now)
            : ContentProvenance.HumanAuthored();

        var skill = Skill.Create(slug, request.Name, kind, provenance, now, request.Description, parentId);

        db.Skills.Add(skill);
        await db.SaveChangesAsync(ct);

        return CatalogOutcome<SkillAdminView>.Success(await ToViewAsync(skill, ct));
    }

    public async Task<CatalogOutcome<SkillSummary>> AddRelationshipAsync(
        CreateSkillRelationshipRequest request,
        CancellationToken ct)
    {
        if (!Enum.TryParse<SkillRelationshipType>(request.Type, ignoreCase: true, out var type))
        {
            return CatalogOutcome<SkillSummary>.Failure(
                CatalogError.InvalidValue,
                $"'{request.Type}' is not a valid relationship type. Expected one of: {string.Join(", ", Enum.GetNames<SkillRelationshipType>())}.");
        }

        var from = await FindAsync(request.FromSlug, ct);
        var to = await FindAsync(request.ToSlug, ct);

        if (from is null || to is null)
        {
            return CatalogOutcome<SkillSummary>.Failure(
                CatalogError.SkillNotFound,
                $"Unknown skill slug: {(from is null ? request.FromSlug : request.ToSlug)}.");
        }

        try
        {
            await graph.AddRelationshipAsync(from.Id, to.Id, type, ct, request.Strength ?? 1.0m);
        }
        catch (InvalidOperationException ex)
        {
            // Cycle rejection and self-reference both surface here. They are expected
            // outcomes of untrusted generated input, not faults.
            return CatalogOutcome<SkillSummary>.Failure(
                CatalogError.RelationshipWouldCreateCycle, ex.Message);
        }

        return CatalogOutcome<SkillSummary>.Success(
            new SkillSummary(from.Id, from.Slug, from.Name, from.Kind.ToString()));
    }

    public async Task<CatalogOutcome<SkillAdminView>> ReviewAsync(
        string slug,
        bool approve,
        Guid reviewerId,
        string? notes,
        CancellationToken ct)
    {
        var skill = await FindAsync(slug, ct);

        if (skill is null)
        {
            return CatalogOutcome<SkillAdminView>.Failure(
                CatalogError.SkillNotFound, $"No skill exists with slug '{slug}'.");
        }

        if (skill.Provenance.ReviewState is ReviewState.Approved or ReviewState.Rejected)
        {
            return CatalogOutcome<SkillAdminView>.Failure(
                CatalogError.AlreadyReviewed,
                $"Skill '{slug}' is already {skill.Provenance.ReviewState}.");
        }

        var now = clock.UtcNow;

        if (approve)
        {
            skill.Provenance.Approve(reviewerId, now, notes);
        }
        else
        {
            skill.Provenance.Reject(reviewerId, now, notes ?? "Rejected without a note.");
        }

        await db.SaveChangesAsync(ct);

        return CatalogOutcome<SkillAdminView>.Success(await ToViewAsync(skill, ct));
    }

    /// <summary>
    /// The pending queue, shallowest containment depth first.
    /// </summary>
    /// <remarks>
    /// Top-down review is the whole point: approve the ~8 areas, then their children,
    /// then the leaves. Reviewing hundreds of flat nodes leads to rubber-stamping, which
    /// defeats the human gate that ADR-0005 depends on.
    /// </remarks>
    public async Task<PendingReviewPage> GetPendingAsync(int limit, CancellationToken ct)
    {
        var pending = await db.Skills
            .AsNoTracking()
            .Where(skill => skill.IsActive && skill.Provenance.ReviewState == ReviewState.Draft)
            .ToListAsync(ct);

        if (pending.Count == 0)
        {
            return new PendingReviewPage([], 0, 0);
        }

        var depths = await LoadDepthsAsync(ct);
        var shallowest = pending.Min(skill => depths.GetValueOrDefault(skill.Id, int.MaxValue));

        var slice = pending
            .Where(skill => depths.GetValueOrDefault(skill.Id, int.MaxValue) == shallowest)
            .OrderBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();

        var parentSlugs = await LoadParentSlugsAsync(slice, ct);

        return new PendingReviewPage(
            [.. slice.Select(skill => ToView(skill, parentSlugs.GetValueOrDefault(skill.Id)))],
            pending.Count,
            shallowest);
    }

    private Task<Skill?> FindAsync(string slug, CancellationToken ct)
    {
        var normalized = slug.Trim().ToLowerInvariant();

        return db.Skills.FirstOrDefaultAsync(skill => skill.Slug == normalized, ct);
    }

    private async Task<Dictionary<Guid, int>> LoadDepthsAsync(CancellationToken ct)
    {
        var nodes = await db.Skills
            .AsNoTracking()
            .Select(skill => new { skill.Id, skill.ParentSkillId })
            .ToListAsync(ct);

        var parents = nodes.ToDictionary(node => node.Id, node => node.ParentSkillId);
        var depths = new Dictionary<Guid, int>();

        foreach (var node in nodes)
        {
            var depth = 0;
            var current = node.ParentSkillId;
            var guard = 0;

            // The guard is belt-and-braces: the containment chain is acyclic by
            // invariant, but a corrupted row must not spin here forever.
            while (current is { } parentId && guard++ < 64)
            {
                depth++;
                current = parents.GetValueOrDefault(parentId);
            }

            depths[node.Id] = depth;
        }

        return depths;
    }

    private async Task<Dictionary<Guid, string>> LoadParentSlugsAsync(List<Skill> skills, CancellationToken ct)
    {
        var parentIds = skills
            .Where(skill => skill.ParentSkillId is not null)
            .Select(skill => skill.ParentSkillId!.Value)
            .Distinct()
            .ToList();

        if (parentIds.Count == 0)
        {
            return [];
        }

        var slugsById = await db.Skills
            .AsNoTracking()
            .Where(skill => parentIds.Contains(skill.Id))
            .ToDictionaryAsync(skill => skill.Id, skill => skill.Slug, ct);

        return skills
            .Where(skill => skill.ParentSkillId is not null
                && slugsById.ContainsKey(skill.ParentSkillId.Value))
            .ToDictionary(skill => skill.Id, skill => slugsById[skill.ParentSkillId!.Value]);
    }

    private async Task<SkillAdminView> ToViewAsync(Skill skill, CancellationToken ct)
    {
        string? parentSlug = null;

        if (skill.ParentSkillId is { } parentId)
        {
            parentSlug = await db.Skills
                .Where(candidate => candidate.Id == parentId)
                .Select(candidate => candidate.Slug)
                .FirstOrDefaultAsync(ct);
        }

        return ToView(skill, parentSlug);
    }

    private static SkillAdminView ToView(Skill skill, string? parentSlug) => new(
        skill.Id,
        skill.Slug,
        skill.Name,
        skill.Description,
        skill.Kind.ToString(),
        parentSlug,
        skill.IsActive,
        skill.Provenance.ReviewState.ToString(),
        skill.Provenance.GenerationSource.ToString(),
        skill.Provenance.GeneratorModel,
        skill.Provenance.GeneratorPromptVersion,
        skill.Provenance.GeneratedAt,
        skill.Provenance.ReviewedAt,
        skill.Provenance.ReviewNotes);
}
