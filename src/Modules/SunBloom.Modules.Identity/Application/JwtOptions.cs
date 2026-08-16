using System.ComponentModel.DataAnnotations;

namespace SunBloom.Modules.Identity.Application;

/// <summary>
/// JWT settings. The signing key comes from user-secrets or the environment — never
/// from appsettings.json.
/// </summary>
internal sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "sunbloom";

    [Required]
    public string Audience { get; set; } = "sunbloom";

    /// <summary>At least 32 bytes; HMAC-SHA256 offers no more strength beyond the key length.</summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Short by design: revocation of access tokens is not possible, so lifetime is the control.</summary>
    [Range(1, 60)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 14;
}
