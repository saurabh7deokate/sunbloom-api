using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SunBloom.SharedKernel.Modules;

namespace SunBloom.Modules.Competency;

/// <summary>
/// The evidence ledger, scoring, assessments, and readiness — the centre of the system.
/// </summary>
public sealed class CompetencyModule : IModule
{
    public string Name => "Competency";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // Evidence ledger and scoring arrive in sub-slice 1.6.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Self-assessment and score endpoints arrive in sub-slice 1.6.
    }
}
