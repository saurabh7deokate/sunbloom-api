using SunBloom.Modules.Catalog;
using SunBloom.Modules.Competency;
using SunBloom.Modules.Identity;
using SunBloom.Modules.Recommendations;
using SunBloom.SharedKernel.Modules;

namespace SunBloom.Api.Modules;

/// <summary>
/// The application's modules, listed explicitly rather than discovered by scanning.
/// </summary>
/// <remarks>
/// Explicit registration is compile-checked and greppable; assembly scanning is
/// neither, and silently does nothing when an assembly has not been loaded.
/// <para>
/// <c>ModuleRegistrationTests</c> asserts this list contains every <see cref="IModule" />
/// implementation across all module assemblies, so adding a module and forgetting to
/// register it fails the build rather than shipping a silently absent feature.
/// </para>
/// </remarks>
public static class ModuleRegistry
{
    public static IReadOnlyList<IModule> All { get; } =
    [
        new IdentityModule(),
        new CatalogModule(),
        new CompetencyModule(),
        new RecommendationsModule(),
    ];
}
