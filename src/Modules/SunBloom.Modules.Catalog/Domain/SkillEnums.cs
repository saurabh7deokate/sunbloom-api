namespace SunBloom.Modules.Catalog.Domain;

/// <summary>What kind of thing a skill node is. Shapes how the tree is presented.</summary>
internal enum SkillKind
{
    /// <summary>A grouping, e.g. "Backend Development". Never assessed directly.</summary>
    Area,

    /// <summary>A named technology, e.g. "ASP.NET Core".</summary>
    Technology,

    /// <summary>An idea or mechanism, e.g. "Dependency Injection".</summary>
    Concept,

    /// <summary>Something you do, e.g. "Writing Integration Tests".</summary>
    Practice,
}

/// <summary>
/// Relationships that are not containment.
/// </summary>
/// <remarks>
/// Containment is <c>Skill.ParentSkillId</c> — a strict tree. These edges cross it:
/// "async/await" is a prerequisite for "ASP.NET Core performance" despite living in a
/// different branch, and a tree cannot express that. See ADR-0004.
/// </remarks>
internal enum SkillRelationshipType
{
    /// <summary>Must be learned first. These edges must form a DAG.</summary>
    Prerequisite,

    /// <summary>Symmetric association. May cycle freely.</summary>
    Related,

    /// <summary>Interchangeable choice, e.g. NUnit vs xUnit. Symmetric.</summary>
    Alternative,
}

/// <summary>Who produced a piece of content.</summary>
internal enum GenerationSource
{
    Human,
    Ai,
}

/// <summary>
/// Review lifecycle. Only <see cref="Approved" /> content is served to learners.
/// </summary>
internal enum ReviewState
{
    Draft,
    InReview,
    Approved,
    Rejected,
}
