using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SunBloom.Modules.Identity.Application;
using SunBloom.Modules.Identity.Domain;
using SunBloom.Modules.Identity.Endpoints;
using SunBloom.Modules.Identity.Infrastructure;
using SunBloom.SharedKernel.Authorization;
using SunBloom.SharedKernel.Modules;

namespace SunBloom.Modules.Identity;

/// <summary>Accounts, authentication, tokens, and user profile.</summary>
public sealed class IdentityModule : IModule
{
    public string Name => "Identity";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("SunBloomDb");

        services.AddDbContext<IdentityDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql
                // Each module keeps its own migrations history inside its own schema.
                // A shared history table would let one module's migrations appear to
                // another as already applied.
                .MigrationsHistoryTable("__ef_migrations_history", IdentityDbContext.Schema))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IModuleDatabaseMigrator, IdentityDatabaseMigrator>();
        services.AddScoped<IModuleSeeder, IdentitySeeder>();

        // PasswordHasher only - not the full ASP.NET Core Identity stack. See ADR-0011.
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        services.AddScoped<TokenService>();
        services.AddScoped<AuthService>();

        // ValidateOnStart turns a missing signing key into a startup failure rather
        // than a 500 on the first login attempt.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        AddJwtAuthentication(services, configuration);

        // The module owns its own rate limit policy; the host only calls AddRateLimiter.
        services.Configure<RateLimiterOptions>(options =>
            options.AddPolicy(RateLimitPolicies.Auth, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString()
                        ?? context.Request.Headers.Host.ToString(),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    })));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => AuthEndpoints.Map(endpoints);

    /// <summary>
    /// The Identity module owns the bearer scheme, so the host does not need to know
    /// how tokens are validated — only to call UseAuthentication in the right order.
    /// </summary>
    private static void AddJwtAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keep the JWT's own claim names. Without this, ASP.NET rewrites "sub"
                // to a long WS-Federation URI and ICurrentUser silently finds nothing.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            string.IsNullOrEmpty(jwt.SigningKey)
                                // Startup validation reports the real problem; this
                                // placeholder only stops key construction throwing first.
                                ? new string('0', 32)
                                : jwt.SigningKey)),
                    ValidateLifetime = true,

                    // Default is five minutes, which would keep expired tokens working
                    // long past their stated lifetime.
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Name,

                    // Must match the claim type TokenService writes; the default is a
                    // long WS-Federation URI that our short "role" claims would not hit.
                    RoleClaimType = TokenService.RoleClaimType,
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(SunBloomPolicies.ContentAdmin, policy =>
                policy.RequireAuthenticatedUser().RequireRole(SunBloomRoles.ContentAdmin));
    }
}
