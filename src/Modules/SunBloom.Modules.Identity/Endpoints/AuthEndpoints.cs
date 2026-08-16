using System.Net.Mail;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SunBloom.Modules.Identity.Application;
using SunBloom.Modules.Identity.Contracts;
using SunBloom.Modules.Identity.Infrastructure;
using SunBloom.SharedKernel.Ownership;

namespace SunBloom.Modules.Identity.Endpoints;

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

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        AuthService auth,
        CancellationToken ct)
    {
        var errors = ValidateRegistration(request);

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var outcome = await auth.RegisterAsync(request, ct);

        return outcome.Error is null
            ? Results.Created($"/api/v1/users/{outcome.Response!.User.Id}", outcome.Response)
            : ToProblem(outcome.Error.Value);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        AuthService auth,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["credentials"] = ["Email and password are required."],
            });
        }

        var outcome = await auth.LoginAsync(request, ct);

        return outcome.Error is null
            ? Results.Ok(outcome.Response)
            : ToProblem(outcome.Error.Value);
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request,
        AuthService auth,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.RefreshToken)] = ["A refresh token is required."],
            });
        }

        var outcome = await auth.RefreshAsync(request, ct);

        return outcome.Error is null
            ? Results.Ok(outcome.Response)
            : ToProblem(outcome.Error.Value);
    }

    private static async Task<IResult> LogoutAsync(
        RefreshRequest request,
        AuthService auth,
        CancellationToken ct)
    {
        await auth.LogoutAsync(request, ct);

        // Always 204: revealing whether the token existed would leak token validity.
        return Results.NoContent();
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ICurrentUser currentUser,
        IdentityDbContext db,
        CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        var user = await db.Users
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new UserResponse(
                candidate.Id, candidate.Email, candidate.DisplayName, candidate.TimeZone))
            .FirstOrDefaultAsync(ct);

        return user is null ? Results.Unauthorized() : Results.Ok(user);
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

    private static bool IsValidEmail(string email)
    {
        // MailAddress is stricter and better maintained than any regex worth writing here.
        return MailAddress.TryCreate(email.Trim(), out _);
    }

    private static IResult ToProblem(AuthError error) => error switch
    {
        AuthError.EmailAlreadyRegistered => Results.Problem(
            title: "Email already registered",
            detail: "An account already exists for this email address.",
            statusCode: StatusCodes.Status409Conflict),

        // Both credential failures return the same response so neither reveals whether
        // the account exists.
        AuthError.InvalidCredentials => Results.Problem(
            title: "Invalid credentials",
            detail: "The email address or password is incorrect.",
            statusCode: StatusCodes.Status401Unauthorized),

        AuthError.InvalidRefreshToken => Results.Problem(
            title: "Invalid refresh token",
            detail: "The refresh token is invalid, expired, or has already been used.",
            statusCode: StatusCodes.Status401Unauthorized),

        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
    };
}
