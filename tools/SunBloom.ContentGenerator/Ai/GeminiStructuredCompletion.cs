using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace SunBloom.ContentGenerator.Ai;

internal sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Recorded as provenance on every skill this produces.</summary>
    public string Model { get; set; } = "gemini-2.5-flash";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    /// <summary>Attempts per prompt. Free tiers rate-limit aggressively; back off and retry.</summary>
    public int MaxAttempts { get; set; } = 4;
}

/// <summary>
/// Gemini implementation of <see cref="IStructuredCompletion" />.
/// </summary>
/// <remarks>
/// Uses Gemini's native <c>responseSchema</c> so the model is constrained to the shape
/// rather than merely asked for it — that removes most parse failures at the source.
/// The response is still validated on arrival, because a constrained decoder is not a
/// guarantee and unvalidated model output is exactly what ARCHITECTURE.md §3.3 forbids.
/// <para>
/// Raw HTTP rather than an SDK: one endpoint, one request shape, and no dependency to
/// keep current.
/// </para>
/// </remarks>
internal sealed class GeminiStructuredCompletion(
    HttpClient http,
    GeminiOptions options,
    ILogger<GeminiStructuredCompletion> logger) : IStructuredCompletion
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string ModelId => options.Model;

    public async Task<CompletionResult<T>> CompleteAsync<T>(
        string systemInstruction,
        string prompt,
        string jsonSchema,
        CancellationToken cancellationToken)
    {
        var url = $"{options.BaseUrl}/models/{options.Model}:generateContent?key={options.ApiKey}";

        var body = new
        {
            system_instruction = new { parts = new[] { new { text = systemInstruction } } },
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = JsonNode.Parse(jsonSchema),
            },
        };

        for (var attempt = 1; attempt <= options.MaxAttempts; attempt++)
        {
            var outcome = await AttemptAsync<T>(url, body, attempt, cancellationToken);

            if (outcome.Completed)
            {
                return outcome.Result;
            }

            // Exponential backoff. Free tiers are per-minute, so the last waits are long
            // enough to cross a quota window rather than burning the remaining attempts.
            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt) * 2);
            GeneratorLog.RetryingAfterBackoff(logger, attempt, options.MaxAttempts, delay.TotalSeconds, outcome.Result.Error ?? "unknown");
            await Task.Delay(delay, cancellationToken);
        }

        return CompletionResult<T>.Failure($"Gave up after {options.MaxAttempts} attempts.");
    }

    private async Task<(bool Completed, CompletionResult<T> Result)> AttemptAsync<T>(
        string url,
        object body,
        int attempt,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await http.PostAsJsonAsync(url, body, Json, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return (false, CompletionResult<T>.Failure($"Network error: {ex.Message}"));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (false, CompletionResult<T>.Failure("Request timed out."));
        }

        using (response)
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var message = $"HTTP {(int)response.StatusCode}: {Truncate(payload, 300)}";

                // 429 and 5xx are worth retrying; a 400 or 403 means the request or key
                // is wrong and will fail identically every time.
                var retryable = (int)response.StatusCode == 429 || (int)response.StatusCode >= 500;

                return (!retryable, CompletionResult<T>.Failure(message));
            }

            return (true, Parse<T>(payload, attempt));
        }
    }

    private CompletionResult<T> Parse<T>(string payload, int attempt)
    {
        try
        {
            var root = JsonNode.Parse(payload);
            var candidate = root?["candidates"]?[0];

            // A safety block returns 200 with no content parts — treat it as a failure
            // with its reason rather than a null-reference surprise downstream.
            var finishReason = candidate?["finishReason"]?.GetValue<string>();
            var text = candidate?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(text))
            {
                return CompletionResult<T>.Failure(
                    $"No content returned (finishReason: {finishReason ?? "none"}).");
            }

            var value = JsonSerializer.Deserialize<T>(text, Json);

            return value is null
                ? CompletionResult<T>.Failure("Model returned JSON null.")
                : CompletionResult<T>.Success(value);
        }
        catch (JsonException ex)
        {
            GeneratorLog.UnparseableResponse(logger, attempt, ex.Message);

            return CompletionResult<T>.Failure($"Unparseable response: {ex.Message}");
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : string.Concat(value.AsSpan(0, max), "…");
}
