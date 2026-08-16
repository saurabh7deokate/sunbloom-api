namespace SunBloom.Modules.Catalog.Domain;

/// <summary>
/// A node in the single global skill graph.
/// </summary>
/// <remarks>
/// Skills are shared by every career path, never owned by one (ADR-0004). A path
/// references skills through requirements; it does not define them. Without this,
/// generated content produces "LINQ", "LINQ Queries", and "Language Integrated Query"
/// across three paths and every cross-path comparison silently compares nothing.
/// </remarks>
internal sealed class Skill
{
    private Skill()
    {
        Slug = null!;
        Name = null!;
        Provenance = null!;
    }

    private Skill(string slug, string name, string? description, SkillKind kind, Guid? parentSkillId, ContentProvenance provenance, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7();
        Slug = slug;
        Name = name;
        Description = description;
        Kind = kind;
        ParentSkillId = parentSkillId;
        Provenance = provenance;
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    /// <summary>Canonical identity, e.g. <c>csharp-linq</c>. Unique across the graph.</summary>
    public string Slug { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public SkillKind Kind { get; private set; }

    /// <summary>The single containment parent. Null for a root area.</summary>
    public Guid? ParentSkillId { get; private set; }

    public ContentProvenance Provenance { get; private set; }

    /// <summary>
    /// Soft delete. Skills are never hard-deleted: evidence rows reference them, and a
    /// dangling reference would silently corrupt every score derived from it.
    /// </summary>
    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Skill Create(
        string slug,
        string name,
        SkillKind kind,
        ContentProvenance provenance,
        DateTimeOffset now,
        string? description = null,
        Guid? parentSkillId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Skill(slug.Trim().ToLowerInvariant(), name.Trim(), description, kind, parentSkillId, provenance, now);
    }

    public void MoveTo(Guid? newParentSkillId, DateTimeOffset now)
    {
        if (newParentSkillId == Id)
        {
            throw new InvalidOperationException("A skill cannot be its own parent.");
        }

        ParentSkillId = newParentSkillId;
        UpdatedAt = now;
    }

    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        UpdatedAt = now;
    }
}
