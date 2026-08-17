using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SunBloom.Modules.Identity.Contracts;
using SunBloom.Modules.Identity.Domain;
using SunBloom.Modules.Identity.Infrastructure;
using SunBloom.SharedKernel.Time;

namespace SunBloom.Modules.Identity.Application;

/// <summary>Registration, login, refresh-token rotation, and logout.</summary>
internal sealed class AuthService(
    IdentityDbContext db,
    IPasswordHasher<User> passwordHasher,
    TokenService tokenService,
    IClock clock,
    ILogger<AuthService> logger)
{
    /// <summary>
    /// A stand-in used to spend the same CPU time when an email is not found, so login
    /// timing does not reveal which addresses are registered.
    /// </summary>
    private static readonly User TimingDecoyUser =
        User.Register("decoy@sunbloom.invalid", "unused", "decoy", "UTC", DateTimeOffset.UnixEpoch);

    private static readonly Lazy<string> TimingDecoyHash = new(() =>
        new PasswordHasher<User>().HashPassword(TimingDecoyUser, "a-password-nobody-uses"));

    public async Task<AuthOutcome> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();

        // citext makes this comparison case-insensitive; the unique index is the real guard.
        if (await db.Users.AnyAsync(user => user.Email == email, ct))
        {
            return AuthOutcome.Failure(AuthError.EmailAlreadyRegistered);
        }

        var now = clock.UtcNow;
        var user = User.Register(
            email,
            passwordHash: string.Empty,
            request.DisplayName,
            string.IsNullOrWhiteSpace(request.TimeZone) ? "UTC" : request.TimeZone,
            now);

        user.UpgradePasswordHash(passwordHasher.HashPassword(user, request.Password), now);

        db.Users.Add(user);

        var response = await IssueNewSessionAsync(user, now, ct);

        AuthLog.UserRegistered(logger, user.Id);

        return AuthOutcome.Success(response);
    }

    public async Task<AuthOutcome> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();
        var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Email == email, ct);

        if (user is null || !user.IsActive)
        {
            // Spend comparable time before failing, then give the same answer either way.
            // Distinguishing "no such account" from "wrong password" enables enumeration,
            // and distinguishing "disabled" leaks account state to an attacker.
            passwordHasher.VerifyHashedPassword(TimingDecoyUser, TimingDecoyHash.Value, request.Password);
            return AuthOutcome.Failure(AuthError.InvalidCredentials);
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return AuthOutcome.Failure(AuthError.InvalidCredentials);
        }

        var now = clock.UtcNow;

        // The hasher reports when a stored hash used weaker parameters than the current
        // defaults. Rehashing on successful login upgrades accounts over time.
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.UpgradePasswordHash(passwordHasher.HashPassword(user, request.Password), now);
            AuthLog.PasswordHashUpgraded(logger, user.Id);
        }

        var response = await IssueNewSessionAsync(user, now, ct);

        return AuthOutcome.Success(response);
    }

    public async Task<AuthOutcome> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        var hash = TokenService.HashRefreshToken(request.RefreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(candidate => candidate.TokenHash == hash, ct);

        if (token is null)
        {
            return AuthOutcome.Failure(AuthError.InvalidRefreshToken);
        }

        var now = clock.UtcNow;

        // A token that was already rotated is being presented again. The legitimate
        // holder would have discarded it, so treat this as theft and revoke the whole
        // family rather than just this link.
        if (token.RevokedAt is not null)
        {
            await RevokeFamilyAsync(token.FamilyId, now, ct);

            AuthLog.RefreshTokenReuseDetected(logger, token.UserId, token.FamilyId);

            return AuthOutcome.Failure(AuthError.InvalidRefreshToken);
        }

        if (!token.IsActive(now))
        {
            return AuthOutcome.Failure(AuthError.InvalidRefreshToken);
        }

        var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Id == token.UserId, ct);

        if (user is null || !user.IsActive)
        {
            return AuthOutcome.Failure(AuthError.InvalidRefreshToken);
        }

        var (rawToken, newHash) = TokenService.CreateRefreshToken();
        var replacement = RefreshToken.IssueInFamily(
            user.Id, newHash, token.FamilyId, tokenService.RefreshTokenExpiry, now);

        db.RefreshTokens.Add(replacement);
        token.RotateTo(replacement, now);

        await db.SaveChangesAsync(ct);

        return AuthOutcome.Success(BuildResponse(user, rawToken));
    }

    /// <summary>Revokes the whole family, so every device from that login is signed out.</summary>
    public async Task LogoutAsync(RefreshRequest request, CancellationToken ct)
    {
        var hash = TokenService.HashRefreshToken(request.RefreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(candidate => candidate.TokenHash == hash, ct);

        if (token is null)
        {
            // Nothing to revoke. Succeed anyway — logout must not reveal token validity.
            return;
        }

        await RevokeFamilyAsync(token.FamilyId, clock.UtcNow, ct);

        AuthLog.UserLoggedOut(logger, token.UserId);
    }

    private async Task<AuthResponse> IssueNewSessionAsync(User user, DateTimeOffset now, CancellationToken ct)
    {
        var (rawToken, hash) = TokenService.CreateRefreshToken();

        db.RefreshTokens.Add(
            RefreshToken.IssueNewFamily(user.Id, hash, tokenService.RefreshTokenExpiry, now));

        await db.SaveChangesAsync(ct);

        return BuildResponse(user, rawToken);
    }

    private AuthResponse BuildResponse(User user, string rawRefreshToken) =>
        new(
            tokenService.CreateAccessToken(user),
            rawRefreshToken,
            tokenService.AccessTokenLifetimeSeconds,
            new UserResponse(user.Id, user.Email, user.DisplayName, user.TimeZone, user.Roles));

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken ct)
    {
        var family = await db.RefreshTokens
            .Where(candidate => candidate.FamilyId == familyId && candidate.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var member in family)
        {
            member.Revoke(now);
        }

        await db.SaveChangesAsync(ct);
    }
}
