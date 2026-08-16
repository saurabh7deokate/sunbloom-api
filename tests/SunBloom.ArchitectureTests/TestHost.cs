using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SunBloom.Api.Modules;
using SunBloom.SharedKernel.Ownership;
using SunBloom.SharedKernel.Time;

namespace SunBloom.ArchitectureTests;

/// <summary>
/// A service provider built through the real module registration path.
/// </summary>
/// <remarks>
/// Tests inspect the EF model, which is built from configuration without opening a
/// connection — so the connection string below never has to point at a real database.
/// <para>
/// Going through <c>ModuleRegistry</c> rather than constructing contexts by hand means
/// a module added in a later slice is covered automatically.
/// </para>
/// </remarks>
internal static class TestHost
{
    public static IServiceProvider Services { get; } = Build();

    private static ServiceProvider Build()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Never connected to. EF builds the model from configuration alone.
                ["ConnectionStrings:SunBloomDb"] =
                    "Host=localhost;Port=5433;Database=sunbloom_model_only;Username=none;Password=none",
                ["Jwt:Issuer"] = "sunbloom-tests",
                ["Jwt:Audience"] = "sunbloom-tests",
                ["Jwt:SigningKey"] = new string('k', 48),
            })
            .Build();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ICurrentUser, NullCurrentUser>();

        foreach (var module in ModuleRegistry.All)
        {
            module.AddServices(services, configuration);
        }

        return services.BuildServiceProvider();
    }

    /// <summary>Stands in for the request-scoped user, which does not exist in these tests.</summary>
    private sealed class NullCurrentUser : ICurrentUser
    {
        public Guid? UserId => null;

        public bool IsAuthenticated => false;
    }
}
