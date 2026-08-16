using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SunBloom.SharedKernel.Modules;

namespace SunBloom.Modules.Identity;

/// <summary>Accounts, authentication, tokens, and user profile.</summary>
public sealed class IdentityModule : IModule
{
    public string Name => "Identity";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // Registration and JWT issuance arrive in sub-slice 1.2.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Auth endpoints arrive in sub-slice 1.2.
    }
}
