using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SunBloom.Modules.Catalog.Application;
using SunBloom.Modules.Catalog.Endpoints;
using SunBloom.Modules.Catalog.Infrastructure;
using SunBloom.SharedKernel.Modules;

namespace SunBloom.Modules.Catalog;

/// <summary>Career paths and versions, the global skill graph, topics, and resources.</summary>
public sealed class CatalogModule : IModule
{
    public string Name => "Catalog";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("SunBloomDb");

        services.AddDbContext<CatalogDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsHistoryTable("__ef_migrations_history", CatalogDbContext.Schema))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IModuleDatabaseMigrator, CatalogDatabaseMigrator>();
        services.AddScoped<IModuleSeeder, CatalogSeeder>();
        services.AddScoped<SkillGraphService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => SkillEndpoints.Map(endpoints);
}
