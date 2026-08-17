using System.Net.Mail;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SunBloom.Modules.Identity.Application;
using SunBloom.Modules.Identity.Contracts;
using SunBloom.Modules.Identity.Infrastructure;
using SunBloom.SharedKernel.Ownership;

namespace SunBloom.Modules.Identity.Endpoints;

/// <summary>
/// Authentication endpoints.
/// </summary>
/// <remarks>
/// Handlers return <c>Results&lt;...&gt;</c> unions rather than <c>IResult</c> so every
/// response type appears in the OpenAPI document. That matters more here than usual:
/// the Angular client's TypeScript types are generated from that document (ADR-0007),
/// and an untyped <c>IResult</c> produces an endpoint with no response schema at all —
/// silently defeating the generation the two-repo split depends on. The union is also
/// compile-checked, so a handler cannot return a shape it did not declare.
/// </remarks>
internal static class AuthEndpoints
{
    private const int MinimumPasswordLength = 12;

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/v1/auth")
            .WithTags("Auth")
            .RequireRateLimiting(RateLimitPolicies.Auth);

        group.MapPost("/register", RegisterAsync).WithSummary("Create an account.");
        group.MapPost("/login", LoginAsync).WithSummary("Exchange credentials for tokens.");
        group.MapPost("/refresh", RefreshAsync).WithSummary("Rotate a refresh token.");
        group.MapPost("/logout", LogoutAsync).WithSummary("Revoke the current token family.");

        group.MapGet("/me", GetCurrentUserAsync)
            .RequireAuthorization()
            .WithSummary("The authenticated user.");
    }

    private static async Task<Results<Created<AuthResponse>, ValidationProblem, ProblemHttpResult>> RegisterAsync(
        RegisterRequest request,
        AuthService auth,
        CancellationToken ct)
    {
        var errors = ValidateRegistration(request);

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var outcome = await auth.RegisterAsync(request, ct);

        return outcome.Error is null
            ? TypedResults.Created($"/api/v1/users/{outcome.Response!.User.Id}", outcome.Response)
            : ToProblem(outcome.Error.Value);
    }

    private static async Task<Results<Ok<AuthResponse>, ValidationProblem, ProblemHttpResult>> LoginAsync(
        LoginRequest request,
        AuthService auth,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["credentials"] = ["Email and password are required."],
            });
        }

        var outcome = await auth.LoginAsync(request, ct);

        return outcome.Error is null
            ? TypedResults.Ok(outcome.Response!)
            : ToProblem(outcome.Error.Value);
    }

    private static async Task<Results<Ok<AuthResponse>, ValidationProblem, ProblemHttpResult>> RefreshAsync(
        RefreshRequest request,
        AuthService auth,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(request.RefreshToken)] = ["A refresh token is required."],
            });
        }

        var outcome = await auth.RefreshAsync(request, ct);

        return outcome.Error is null
            ? TypedResults.Ok(outcome.Response!)
            : ToProblem(outcome.Error.Value);
    }

    private static async Task<NoContent> LogoutAsync(
        RefreshRequest request,
        AuthService auth,
        CancellationToken ct)
    {
        await auth.LogoutAsync(request, ct);

        // Always 204: revealing whether the token existed would leak token validity.
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<UserResponse>, UnauthorizedHttpResult>> GetCurrentUserAsync(
        ICurrentUser currentUser,
        IdentityDbContext db,
        CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId, ct);

        return user is null
            ? TypedResults.Unauthorized()
            : TypedResults.Ok(new UserResponse(
                user.Id, user.Email, user.DisplayName, user.TimeZone, user.Roles));
    }

    private static Dictionary<string, string[]> ValidateRegistration(RegisterRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(request.Email) || !IsValidEmail(request.Email))
        {
            errors[nameof(request.Email)] = ["A valid email address is required."];
        }

        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < MinimumPasswordLength)
        {
            // Length over composition rules: a long passphrase beats a short password
            // with a symbol bolted on, and composition rules push people toward the latter.
            errors[nameof(request.Password)] =
                [$"Password must be at least {MinimumPasswordLength} characters."];
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            errors[nameof(request.DisplayName)] = ["A display name is required."];
        }

        return errors;
    }

    // MailAddress is stricter and better maintained than any regex worth writing here.
    private static bool IsValidEmail(string email) => MailAddress.TryCreate(email.Trim(), out _);

    private static ProblemHttpResult ToProblem(AuthError error) => error switch
    {
        AuthError.EmailAlreadyRegistered => TypedResults.Problem(
            title: "Email already registered",
            detail: "An account already exists for this email address.",
            statusCode: StatusCodes.Status409Conflict),

        // Both credential failures return the same response so neither reveals whether
        // the account exists.
        AuthError.InvalidCredentials => TypedResults.Problem(
            title: "Invalid credentials",
            detail: "The email address or password is incorrect.",
            statusCode: StatusCodes.Status401Unauthorized),

        AuthError.InvalidRefreshToken => TypedResults.Problem(
            title: "Invalid refresh token",
            detail: "The refresh token is invalid, expired, or has already been used.",
            statusCode: StatusCodes.Status401Unauthorized),

        _ => TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError),
    };
}
