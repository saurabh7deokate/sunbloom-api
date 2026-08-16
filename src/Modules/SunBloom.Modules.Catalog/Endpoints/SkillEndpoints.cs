using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SunBloom.Modules.Catalog.Application;
using SunBloom.Modules.Catalog.Contracts;

namespace SunBloom.Modules.Catalog.Endpoints;

/// <summary>
/// Skill graph read endpoints.
/// </summary>
/// <remarks>
/// Typed result unions so response schemas reach the OpenAPI document, from which the
/// Angular client's types are generated (ADR-0007).
/// </remarks>
internal static class SkillEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/v1/skills")
            .WithTags("Skills")
            .RequireAuthorization();

        group.MapGet("/tree", GetTreeAsync)
            .WithSummary("The skill containment tree, optionally rooted at one skill.");

        group.MapGet("/{slug}", GetDetailAsync)
            .WithSummary("One skill with its prerequisites, unlocks, and related skills.");
    }

    private static async Task<Results<Ok<IReadOnlyList<SkillTreeNode>>, ProblemHttpResult>> GetTreeAsync(
        SkillGraphService skills,
        CancellationToken ct,
        string? root = null)
    {
        var tree = await skills.GetTreeAsync(root, ct);

        return tree.Count == 0 && root is not null
            ? NotFound(root)
            : TypedResults.Ok(tree);
    }

    private static async Task<Results<Ok<SkillDetail>, ProblemHttpResult>> GetDetailAsync(
        string slug,
        SkillGraphService skills,
        CancellationToken ct)
    {
        var detail = await skills.GetDetailAsync(slug, ct);

        return detail is null ? NotFound(slug) : TypedResults.Ok(detail);
    }

    private static ProblemHttpResult NotFound(string slug) => TypedResults.Problem(
        title: "Skill not found",
        detail: $"No approved skill exists with slug '{slug}'.",
        statusCode: StatusCodes.Status404NotFound);
}
