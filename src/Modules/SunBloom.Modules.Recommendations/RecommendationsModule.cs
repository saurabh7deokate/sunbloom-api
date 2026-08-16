using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SunBloom.SharedKernel.Modules;

namespace SunBloom.Modules.Recommendations;

/// <summary>Gap ranking, prerequisite sequencing, and the daily plan.</summary>
public sealed class RecommendationsModule : IModule
{
    public string Name => "Recommendations";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // Gap ranking arrives in sub-slice 1.8.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Plan endpoints arrive in sub-slice 1.9.
    }
}
