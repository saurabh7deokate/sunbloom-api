namespace SunBloom.Modules.Catalog.Domain;

/// <summary>A typed edge between two skills.</summary>
internal sealed class SkillRelationship
{
    private SkillRelationship()
    {
    }

    private SkillRelationship(Guid fromSkillId, Guid toSkillId, SkillRelationshipType type, decimal strength, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7();
        FromSkillId = fromSkillId;
        ToSkillId = toSkillId;
        Type = type;
        Strength = strength;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid FromSkillId { get; private set; }

    public Guid ToSkillId { get; private set; }

    public SkillRelationshipType Type { get; private set; }

    /// <summary>How strongly the relationship applies, 0.0–1.0.</summary>
    public decimal Strength { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Creates an edge. Acyclicity of <see cref="SkillRelationshipType.Prerequisite" />
    /// edges is a graph-wide invariant and cannot be checked here — the caller must
    /// consult <c>PrerequisiteGraph</c> first. <c>SkillGraphService</c> is the only
    /// sanctioned caller.
    /// </summary>
    public static SkillRelationship Create(
        Guid fromSkillId,
        Guid toSkillId,
        SkillRelationshipType type,
        DateTimeOffset now,
        decimal strength = 1.0m)
    {
        if (fromSkillId == toSkillId)
        {
            throw new InvalidOperationException("A skill cannot relate to itself.");
        }

        if (strength is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(strength), strength, "Strength must be between 0 and 1.");
        }

        return new SkillRelationship(fromSkillId, toSkillId, type, strength, now);
    }
}
