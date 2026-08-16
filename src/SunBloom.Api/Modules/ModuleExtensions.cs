namespace SunBloom.Api.Modules;

/// <summary>Wires every registered module into the host.</summary>
public static class ModuleExtensions
{
    public static IServiceCollection AddModules(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        foreach (var module in ModuleRegistry.All)
        {
            module.AddServices(services, configuration);
        }

        return services;
    }

    public static IEndpointRouteBuilder MapModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        foreach (var module in ModuleRegistry.All)
        {
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }
}
