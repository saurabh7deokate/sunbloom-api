using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SunBloom.Modules.Identity.Domain;
using SunBloom.SharedKernel.Time;

namespace SunBloom.Modules.Identity.Application;

/// <summary>Issues access tokens and opaque refresh tokens.</summary>
internal sealed class TokenService(IOptions<JwtOptions> options, IClock clock)
{
    private readonly JwtOptions _options = options.Value;

    public int AccessTokenLifetimeSeconds => _options.AccessTokenMinutes * 60;

    public DateTimeOffset RefreshTokenExpiry => clock.UtcNow.AddDays(_options.RefreshTokenDays);

    public string CreateAccessToken(User user)
    {
        var now = clock.UtcNow;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddMinutes(_options.AccessTokenMinutes).UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.DisplayName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>
    /// Creates an opaque refresh token. The caller returns <c>Token</c> to the client
    /// exactly once and persists only <c>Hash</c>.
    /// </summary>
    public static (string Token, string Hash) CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Base64UrlEncoder.Encode(bytes);

        return (token, HashRefreshToken(token));
    }

    /// <summary>
    /// SHA-256, unsalted and deliberately so: the token is 256 bits of cryptographic
    /// randomness, not a password, so it is not brute-forceable and the lookup must be
    /// a fast, deterministic index probe.
    /// </summary>
    public static string HashRefreshToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
