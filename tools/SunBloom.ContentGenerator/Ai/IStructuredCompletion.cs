namespace SunBloom.ContentGenerator.Ai;

/// <summary>
/// The single AI primitive: a prompt plus a schema in, validated typed output out.
/// </summary>
/// <remarks>
/// ARCHITECTURE.md §3.2 rejects a seven-method <c>IAIService</c> — that shape changes
/// every time a use case is added, which defeats the abstraction. Provider independence
/// belongs here, one level lower: swapping Gemini for something else touches one class,
/// while use cases above stay untouched because each owns its own prompt and output type.
/// <para>
/// This lives in the generator tool, not the API. Content is generated offline and
/// human-reviewed (ADR-0005), so no AI runs on the request path until Phase 5.
/// </para>
/// </remarks>
internal interface IStructuredCompletion
{
    /// <summary>Model identifier, recorded as provenance on everything it produces.</summary>
    string ModelId { get; }

    /// <summary>
    /// Completes a prompt and deserializes the result against <paramref name="jsonSchema" />.
    /// </summary>
    /// <returns>
    /// A failed result rather than a throw when the model returns something unusable —
    /// malformed JSON, schema mismatch, a safety block. Bad model output is an expected
    /// outcome of a generation run, not an exceptional one.
    /// </returns>
    Task<CompletionResult<T>> CompleteAsync<T>(
        string systemInstruction,
        string prompt,
        string jsonSchema,
        CancellationToken cancellationToken);
}

internal sealed record CompletionResult<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;

    public static CompletionResult<T> Success(T value) => new(value, null);

    public static CompletionResult<T> Failure(string error) => new(default, error);
}
