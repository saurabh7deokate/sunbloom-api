using System.Reflection;
using SunBloom.SharedKernel.Modules;

namespace SunBloom.ArchitectureTests;

/// <summary>
/// Modules communicate only through their <c>Contracts</c> namespace (ADR-0001).
/// </summary>
/// <remarks>
/// The C# compiler already prevents one module from touching another's <c>internal</c>
/// types. What it cannot prevent is a module widening its own surface by making
/// something public outside <c>Contracts</c> — which is how a boundary erodes in
/// practice. That is the invariant these tests defend.
/// </remarks>
public class ModuleBoundaryTests
{
    [Fact]
    public void Public_types_in_a_module_must_live_in_its_Contracts_namespace()
    {
        var violations = new List<string>();

        foreach (var assembly in TestContext.ModuleAssemblies)
        {
            var offenders = assembly
                .GetExportedTypes()
                .Where(type => !IsInContractsNamespace(type) && !IsModuleEntryPoint(type))
                .Select(type => $"{assembly.GetName().Name}: {type.FullName}");

            violations.AddRange(offenders);
        }

        Assert.True(
            violations.Count == 0,
            $"""
             These types are public but live outside their module's Contracts namespace.
             Make them internal, or move them into Contracts if they are genuinely part
             of the module's public surface. See ADR-0001.

             {string.Join(Environment.NewLine, violations)}
             """);
    }

    [Fact]
    public void Every_module_assembly_exposes_exactly_one_module_entry_point()
    {
        foreach (var assembly in TestContext.ModuleAssemblies)
        {
            var entryPoints = assembly
                .GetTypes()
                .Where(type => typeof(IModule).IsAssignableFrom(type) && type is { IsAbstract: false, IsInterface: false })
                .ToList();

            Assert.True(
                entryPoints.Count == 1,
                $"{assembly.GetName().Name} has {entryPoints.Count} IModule implementations; expected exactly 1.");
        }
    }

    [Fact]
    public void Modules_must_not_reference_the_api_host()
    {
        foreach (var assembly in TestContext.ModuleAssemblies)
        {
            var referencesHost = assembly
                .GetReferencedAssemblies()
                .Any(reference => reference.Name == "SunBloom.Api");

            Assert.False(
                referencesHost,
                $"{assembly.GetName().Name} references SunBloom.Api. Dependencies point from the "
                + "host into modules, never the other way.");
        }
    }

    private static bool IsInContractsNamespace(Type type) =>
        type.Namespace?.Contains(".Contracts", StringComparison.Ordinal) == true;

    // The IModule implementation must be public so the host can construct it.
    private static bool IsModuleEntryPoint(Type type) => typeof(IModule).IsAssignableFrom(type);
}
