using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SunBloom.Modules.Catalog.Application;

namespace SunBloom.Modules.Catalog.Endpoints;

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

    private static async Task<IResult> GetTreeAsync(
        SkillGraphService skills,
        CancellationToken ct,
        string? root = null)
    {
        var tree = await skills.GetTreeAsync(root, ct);

        return tree.Count == 0 && root is not null
            ? Results.Problem(
                title: "Skill not found",
                detail: $"No approved skill exists with slug '{root}'.",
                statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(tree);
    }

    private static async Task<IResult> GetDetailAsync(
        string slug,
        SkillGraphService skills,
        CancellationToken ct)
    {
        var detail = await skills.GetDetailAsync(slug, ct);

        return detail is null
            ? Results.Problem(
                title: "Skill not found",
                detail: $"No approved skill exists with slug '{slug}'.",
                statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(detail);
    }
}
