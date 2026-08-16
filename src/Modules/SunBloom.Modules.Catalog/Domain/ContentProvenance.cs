namespace SunBloom.Modules.Catalog.Domain;

/// <summary>
/// Where a piece of content came from and whether a human has approved it.
/// </summary>
/// <remarks>
/// Content is AI-generated and human-reviewed (ADR-0005). These fields describe content
/// at the moment of generation, so they cannot be reconstructed later — they belong in
/// the schema from the first migration rather than being added when review tooling
/// arrives in sub-slice 1.5.
/// </remarks>
internal sealed class ContentProvenance
{
    private ContentProvenance()
    {
    }

    public GenerationSource GenerationSource { get; private set; }

    public string? GeneratorModel { get; private set; }

    public string? GeneratorPromptVersion { get; private set; }

    public DateTimeOffset? GeneratedAt { get; private set; }

    public ReviewState ReviewState { get; private set; }

    public Guid? ReviewedByUserId { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public string? ReviewNotes { get; private set; }

    /// <summary>Authored by a person, so it needs no review to be served.</summary>
    public static ContentProvenance HumanAuthored() => new()
    {
        GenerationSource = GenerationSource.Human,
        ReviewState = ReviewState.Approved,
    };

    /// <summary>Machine-generated. Enters as a draft and is not served until approved.</summary>
    public static ContentProvenance AiGenerated(string model, string promptVersion, DateTimeOffset generatedAt) => new()
    {
        GenerationSource = GenerationSource.Ai,
        GeneratorModel = model,
        GeneratorPromptVersion = promptVersion,
        GeneratedAt = generatedAt,
        ReviewState = ReviewState.Draft,
    };

    public void Approve(Guid reviewerId, DateTimeOffset now, string? notes = null)
    {
        ReviewState = ReviewState.Approved;
        ReviewedByUserId = reviewerId;
        ReviewedAt = now;
        ReviewNotes = notes;
    }

    public void Reject(Guid reviewerId, DateTimeOffset now, string notes)
    {
        ReviewState = ReviewState.Rejected;
        ReviewedByUserId = reviewerId;
        ReviewedAt = now;
        ReviewNotes = notes;
    }
}
