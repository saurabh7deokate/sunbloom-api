using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SunBloom.SharedKernel.Authorization;
using SunBloom.SharedKernel.Modules;
using SunBloom.SharedKernel.Time;

namespace SunBloom.Modules.Identity.Infrastructure;

/// <summary>
/// Grants <see cref="SunBloomRoles.ContentAdmin" /> to bootstrap accounts.
/// </summary>
/// <remarks>
/// Solves the chicken-and-egg: admin write endpoints require the role, but nothing can
/// grant it until someone already has it. Emails come from configuration
/// (<c>Bootstrap:ContentAdminEmails</c>, set via user-secrets), so no privileged account
/// is hard-coded in the repository.
/// <para>
/// Grants only — never revokes. Removing an email from the list does not strip the role,
/// because a config edit should not silently de-privilege a working account.
/// </para>
/// </remarks>
internal sealed class IdentitySeeder(
    IdentityDbContext db,
    IConfiguration configuration,
    IClock clock,
    ILogger<IdentitySeeder> logger) : IModuleSeeder
{
    public const string ConfigurationKey = "Bootstrap:ContentAdminEmails";

    public string ModuleName => "Identity";

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var emails = configuration
            .GetSection(ConfigurationKey)
            .Get<string[]>() ?? [];

        if (emails.Length == 0)
        {
            return;
        }

        var normalized = emails
            .Select(email => email.Trim())
            .Where(email => email.Length > 0)
            .ToArray();

        // citext makes this comparison case-insensitive in the database.
        var users = await db.Users
            .Where(user => normalized.Contains(user.Email))
            .ToListAsync(cancellationToken);

        var now = clock.UtcNow;
        var granted = 0;

        foreach (var user in users)
        {
            if (user.GrantRole(SunBloomRoles.ContentAdmin, now))
            {
                granted++;
                IdentityLog.ContentAdminGranted(logger, user.Id);
            }
        }

        if (granted > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        // A configured email with no account is normal — the account may not exist yet.
        // Seeding runs on every Development startup, so it will be granted once it does.
        var missing = normalized.Length - users.Count;

        if (missing > 0)
        {
            IdentityLog.BootstrapAccountsMissing(logger, missing);
        }
    }
}

internal static partial class IdentityLog
{
    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Information,
        Message = "Granted ContentAdmin to user {UserId}")]
    public static partial void ContentAdminGranted(ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Information,
        Message = "{Count} bootstrap admin email(s) have no account yet; will grant once registered")]
    public static partial void BootstrapAccountsMissing(ILogger logger, int count);
}
