using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SunBloom.SharedKernel.Modules;

namespace SunBloom.Modules.Catalog;

/// <summary>Career paths and versions, the global skill graph, topics, and resources.</summary>
public sealed class CatalogModule : IModule
{
    public string Name => "Catalog";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // Skill graph persistence arrives in sub-slice 1.3.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Skill graph endpoints arrive in sub-slice 1.3.
    }
}
