using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SunBloom.SharedKernel.Modules;

/// <summary>
/// A module's entire contract with the host. The host discovers implementations and
/// calls them; it never reaches into a module directly.
/// </summary>
/// <remarks>
/// This is what lets a module keep its endpoints, handlers, and persistence
/// <c>internal</c> while still being wired into the application. See ADR-0001.
/// </remarks>
public interface IModule
{
    /// <summary>Stable module name, used in logs, health reporting, and diagnostics.</summary>
    string Name { get; }

    /// <summary>Registers everything the module needs in the container.</summary>
    void AddServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Maps the module's HTTP endpoints.</summary>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
