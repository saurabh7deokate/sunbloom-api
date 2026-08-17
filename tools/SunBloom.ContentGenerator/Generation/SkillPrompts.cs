namespace SunBloom.ContentGenerator.Generation;

internal sealed record GeneratedSkill(string Slug, string Name, string Kind, string Description);

internal sealed record GeneratedSkillSet(IReadOnlyList<GeneratedSkill> Skills);

internal sealed record GeneratedPrerequisite(string FromSlug, string ToSlug, string Rationale);

internal sealed record GeneratedPrerequisiteSet(IReadOnlyList<GeneratedPrerequisite> Prerequisites);

/// <summary>
/// Prompts and response schemas for skill generation.
/// </summary>
/// <remarks>
/// <see cref="Version" /> is recorded as provenance on every generated skill, so a batch
/// can be traced back to the exact prompt that produced it. Bump it whenever the wording
/// changes materially — a silent edit makes past content unattributable.
/// </remarks>
internal static class SkillPrompts
{
    public const string Version = "skills-v1";

    /// <summary>
    /// Shared framing. The duplicate-avoidance rule is the load-bearing part: skills are
    /// one global graph (ADR-0004), so "LINQ" and "LINQ Queries" as separate nodes would
    /// silently break every cross-path comparison the product is eventually for.
    /// </summary>
    public const string SystemInstruction = """
        You are helping build a skill taxonomy for software engineers, used to assess
        what someone knows and what they should learn next.

        Rules:
        - Skills are shared across every career path. Never invent a near-duplicate of an
          existing skill; if a concept is already in the provided list, do not emit it again
          under a different name.
        - Slugs are lowercase, hyphen-separated, stable, and specific enough to stay unique
          across the whole graph (prefer "csharp-linq" over "linq").
        - Kind is one of: Area (a grouping, never assessed directly), Technology (a named
          tool or framework), Concept (an idea or mechanism), Practice (something you do).
        - Descriptions are one sentence, concrete, and written for a practitioner. Say what
          someone who has this skill can actually do. Avoid marketing language.
        - Prefer the vocabulary practitioners actually use over textbook phrasing.
        - Emit only skills that a hiring manager would recognise as a real, separable
          competence. Do not pad the list to reach a count.
        """;

    /// <summary>Constrains the decoder to the exact shape, removing most parse failures.</summary>
    public const string SkillSetSchema = """
        {
          "type": "object",
          "properties": {
            "skills": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "slug":        { "type": "string" },
                  "name":        { "type": "string" },
                  "kind":        { "type": "string", "enum": ["Area", "Technology", "Concept", "Practice"] },
                  "description": { "type": "string" }
                },
                "required": ["slug", "name", "kind", "description"]
              }
            }
          },
          "required": ["skills"]
        }
        """;

    public const string PrerequisiteSetSchema = """
        {
          "type": "object",
          "properties": {
            "prerequisites": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "fromSlug":  { "type": "string" },
                  "toSlug":    { "type": "string" },
                  "rationale": { "type": "string" }
                },
                "required": ["fromSlug", "toSlug", "rationale"]
              }
            }
          },
          "required": ["prerequisites"]
        }
        """;

    public static string ChildrenPrompt(
        string parentName,
        string parentDescription,
        string parentKind,
        IReadOnlyList<string> existingSlugs,
        int target) =>
        $"""
         Generate the direct child skills of this node in a .NET backend engineering taxonomy.

         Parent: {parentName} ({parentKind})
         Parent description: {parentDescription}

         Produce about {target} children — fewer if the parent genuinely has fewer separable
         sub-skills. Children must be one level down only: do not emit grandchildren, and do
         not restate the parent itself.

         These slugs already exist somewhere in the graph. Do not emit any of them, and do not
         emit a near-synonym of one:
         {string.Join(", ", existingSlugs)}
         """;

    /// <summary>
    /// Prerequisites are generated separately from the hierarchy, and deliberately so:
    /// they cross containment branches, which is the whole reason typed edges exist
    /// alongside the parent tree (ADR-0004).
    /// </summary>
    public static string PrerequisitePrompt(IReadOnlyList<string> skillLines) =>
        $"""
         Below are skills in a .NET backend engineering taxonomy, as "slug — name: description".

         Identify prerequisite relationships: "fromSlug must be learned before toSlug".

         Rules:
         - Only genuine learning dependencies. If someone could reasonably learn B without
           knowing A first, it is not a prerequisite.
         - Prefer edges that cross branches of the hierarchy — those carry the most
           information. A parent is not automatically a prerequisite for its own child.
         - The result must be acyclic. Never emit an edge that closes a loop with another
           edge you are emitting.
         - Be conservative. A wrong prerequisite silently blocks a learner from a skill they
           could have started, which is worse than a missing one.
         - Give a one-line rationale for each edge, stating what the earlier skill provides
           that the later one needs.

         Skills:
         {string.Join("\n", skillLines)}
         """;
}
