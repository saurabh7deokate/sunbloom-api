using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SunBloom.Modules.Catalog.Application;
using SunBloom.Modules.Catalog.Contracts;
using SunBloom.SharedKernel.Authorization;
using SunBloom.SharedKernel.Ownership;

namespace SunBloom.Modules.Catalog.Endpoints;

/// <summary>
/// Content authoring and review. Requires the ContentAdmin role.
/// </summary>
/// <remarks>
/// The content generator writes through these endpoints rather than the database
/// directly, so generated content passes the same validation as anything else —
/// prerequisite cycle rejection above all. One write path, one set of invariants.
/// </remarks>
internal static class SkillAdminEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/v1/admin/skills")
            .WithTags("Skill administration")
            .RequireAuthorization(SunBloomPolicies.ContentAdmin);

        group.MapPost("/", CreateSkillAsync)
            .WithSummary("Create a skill. Omit generation info to mark it human-authored.");

        group.MapPost("/relationships", AddRelationshipAsync)
            .WithSummary("Relate two skills. Prerequisite edges that would create a cycle are rejected.");

        group.MapGet("/pending", GetPendingAsync)
            .WithSummary("The review queue, shallowest containment depth first.");

        group.MapPost("/{slug}/approve", ApproveAsync).WithSummary("Approve a draft skill.");
        group.MapPost("/{slug}/reject", RejectAsync).WithSummary("Reject a draft skill.");
    }

    private static async Task<Results<Created<SkillAdminView>, ProblemHttpResult>> CreateSkillAsync(
        CreateSkillRequest request,
        SkillAdminService admin,
        CancellationToken ct)
    {
        var outcome = await admin.CreateSkillAsync(request, ct);

        return outcome.Error is null
            ? TypedResults.Created($"/api/v1/skills/{outcome.Value!.Slug}", outcome.Value)
            : ToProblem(outcome.Error.Value, outcome.Detail);
    }

    private static async Task<Results<Ok<SkillSummary>, ProblemHttpResult>> AddRelationshipAsync(
        CreateSkillRelationshipRequest request,
        SkillAdminService admin,
        CancellationToken ct)
    {
        var outcome = await admin.AddRelationshipAsync(request, ct);

        return outcome.Error is null
            ? TypedResults.Ok(outcome.Value!)
            : ToProblem(outcome.Error.Value, outcome.Detail);
    }

    private static async Task<Ok<PendingReviewPage>> GetPendingAsync(
        SkillAdminService admin,
        CancellationToken ct,
        int limit = 50) =>
        TypedResults.Ok(await admin.GetPendingAsync(Math.Clamp(limit, 1, 200), ct));

    private static Task<Results<Ok<SkillAdminView>, ProblemHttpResult>> ApproveAsync(
        string slug,
        ReviewDecisionRequest? request,
        SkillAdminService admin,
        ICurrentUser currentUser,
        CancellationToken ct) =>
        ReviewAsync(slug, approve: true, request, admin, currentUser, ct);

    private static Task<Results<Ok<SkillAdminView>, ProblemHttpResult>> RejectAsync(
        string slug,
        ReviewDecisionRequest? request,
        SkillAdminService admin,
        ICurrentUser currentUser,
        CancellationToken ct) =>
        ReviewAsync(slug, approve: false, request, admin, currentUser, ct);

    private static async Task<Results<Ok<SkillAdminView>, ProblemHttpResult>> ReviewAsync(
        string slug,
        bool approve,
        ReviewDecisionRequest? request,
        SkillAdminService admin,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        // The policy guarantees an authenticated principal, so this is defensive only.
        if (currentUser.UserId is not { } reviewerId)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized);
        }

        var outcome = await admin.ReviewAsync(slug, approve, reviewerId, request?.Notes, ct);

        return outcome.Error is null
            ? TypedResults.Ok(outcome.Value!)
            : ToProblem(outcome.Error.Value, outcome.Detail);
    }

    private static ProblemHttpResult ToProblem(CatalogError error, string? detail) => error switch
    {
        CatalogError.SlugAlreadyExists => TypedResults.Problem(
            title: "Slug already exists", detail: detail, statusCode: StatusCodes.Status409Conflict),

        CatalogError.AlreadyReviewed => TypedResults.Problem(
            title: "Already reviewed", detail: detail, statusCode: StatusCodes.Status409Conflict),

        // A cycle is a client error, not a server fault: the generator proposed an edge
        // the graph cannot accept, and the message tells it which.
        CatalogError.RelationshipWouldCreateCycle => TypedResults.Problem(
            title: "Relationship would create a cycle",
            detail: detail,
            statusCode: StatusCodes.Status422UnprocessableEntity),

        CatalogError.SkillNotFound or CatalogError.ParentNotFound => TypedResults.Problem(
            title: "Skill not found", detail: detail, statusCode: StatusCodes.Status404NotFound),

        CatalogError.InvalidValue => TypedResults.Problem(
            title: "Invalid value", detail: detail, statusCode: StatusCodes.Status400BadRequest),

        _ => TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError),
    };
}
