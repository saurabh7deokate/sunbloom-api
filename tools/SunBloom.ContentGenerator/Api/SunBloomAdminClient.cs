using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SunBloom.ContentGenerator.Api;

internal sealed class SunBloomOptions
{
    public const string SectionName = "SunBloom";

    public string BaseUrl { get; set; } = "http://localhost:5078";

    /// <summary>A ContentAdmin account. Credentials come from user-secrets, never the repo.</summary>
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

internal sealed record CreatedSkill(string Slug, string ReviewState);

internal sealed record AdminCallResult(bool Success, int StatusCode, string? Detail)
{
    /// <summary>422 means the graph rejected the edge — a cycle. Expected, not a fault.</summary>
    public bool IsCycleRejection => StatusCode == 422;

    /// <summary>409 means the slug is taken. Also expected: the model proposed a duplicate.</summary>
    public bool IsDuplicate => StatusCode == 409;
}

/// <summary>
/// Talks to the SunBloom admin API.
/// </summary>
/// <remarks>
/// The generator writes through HTTP rather than the database so generated content passes
/// exactly the same validation as any other write — prerequisite cycle rejection above
/// all. A direct database path would let a bad batch bypass the one invariant the
/// recommendation engine depends on.
/// </remarks>
internal sealed class SunBloomAdminClient(
    HttpClient http,
    SunBloomOptions options,
    ILogger<SunBloomAdminClient> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private string? _accessToken;

    public async Task<bool> SignInAsync(CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(
            $"{options.BaseUrl}/api/v1/auth/login",
            new { email = options.Email, password = options.Password },
            Json,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            GeneratorLog.SignInFailed(logger, (int)response.StatusCode);

            return false;
        }

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json, ct);
        _accessToken = payload.GetProperty("accessToken").GetString();

        var roles = payload.GetProperty("user").GetProperty("roles")
            .EnumerateArray().Select(role => role.GetString()!).ToArray();

        if (!roles.Contains("ContentAdmin", StringComparer.Ordinal))
        {
            // Failing here beats discovering it as a wall of 403s mid-generation.
            GeneratorLog.MissingContentAdminRole(logger, options.Email);

            return false;
        }

        return true;
    }

    public async Task<IReadOnlyList<SkillNode>> GetAllSkillsAsync(CancellationToken ct)
    {
        // The admin queue plus the approved tree together cover every skill: pending
        // shows drafts, the tree shows approved. Duplicate avoidance needs both.
        var approved = await GetApprovedAsync(ct);
        var pending = await GetPendingAsync(ct);

        return [.. approved.Concat(pending).DistinctBy(skill => skill.Slug, StringComparer.Ordinal)];
    }

    public async Task<(CreatedSkill? Created, AdminCallResult Result)> CreateSkillAsync(
        string slug,
        string name,
        string kind,
        string description,
        string? parentSlug,
        string model,
        string promptVersion,
        CancellationToken ct)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            "/api/v1/admin/skills",
            new
            {
                slug,
                name,
                kind,
                description,
                parentSlug,
                generation = new { model, promptVersion },
            },
            ct);

        var result = await ToResultAsync(response, ct);

        if (!result.Success)
        {
            return (null, result);
        }

        var created = await response.Content.ReadFromJsonAsync<JsonElement>(Json, ct);

        return (
            new CreatedSkill(
                created.GetProperty("slug").GetString()!,
                created.GetProperty("reviewState").GetString()!),
            result);
    }

    public async Task<AdminCallResult> AddPrerequisiteAsync(string fromSlug, string toSlug, CancellationToken ct)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            "/api/v1/admin/skills/relationships",
            new { fromSlug, toSlug, type = "Prerequisite" },
            ct);

        return await ToResultAsync(response, ct);
    }

    private async Task<IReadOnlyList<SkillNode>> GetApprovedAsync(CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/v1/skills/tree", null, ct);

        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var tree = await response.Content.ReadFromJsonAsync<JsonElement>(Json, ct);
        var flat = new List<SkillNode>();

        Flatten(tree, flat);

        return flat;
    }

    private async Task<IReadOnlyList<SkillNode>> GetPendingAsync(CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/v1/admin/skills/pending?limit=200", null, ct);

        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Json, ct);

        return [.. page.GetProperty("items").EnumerateArray().Select(item => new SkillNode(
            item.GetProperty("slug").GetString()!,
            item.GetProperty("name").GetString()!,
            item.GetProperty("kind").GetString()!,
            item.TryGetProperty("description", out var description) ? description.GetString() : null))];
    }

    private static void Flatten(JsonElement nodes, List<SkillNode> into)
    {
        foreach (var node in nodes.EnumerateArray())
        {
            into.Add(new SkillNode(
                node.GetProperty("slug").GetString()!,
                node.GetProperty("name").GetString()!,
                node.GetProperty("kind").GetString()!,
                node.TryGetProperty("description", out var description) ? description.GetString() : null));

            if (node.TryGetProperty("children", out var children))
            {
                Flatten(children, into);
            }
        }
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, $"{options.BaseUrl}{path}");

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        if (_accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        return http.SendAsync(request, ct);
    }

    private static async Task<AdminCallResult> ToResultAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return new AdminCallResult(true, (int)response.StatusCode, null);
        }

        var detail = await response.Content.ReadAsStringAsync(ct);

        // Problem Details carries the useful message; fall back to the raw body.
        try
        {
            var problem = JsonSerializer.Deserialize<JsonElement>(detail, Json);

            if (problem.TryGetProperty("detail", out var value))
            {
                detail = value.GetString() ?? detail;
            }
        }
        catch (JsonException)
        {
            // Not Problem Details — keep the raw body.
        }

        return new AdminCallResult(false, (int)response.StatusCode, detail);
    }
}

internal sealed record SkillNode(string Slug, string Name, string Kind, string? Description);
