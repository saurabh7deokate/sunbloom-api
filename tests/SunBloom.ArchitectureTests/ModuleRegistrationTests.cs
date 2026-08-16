using SunBloom.Api.Modules;
using SunBloom.SharedKernel.Modules;

namespace SunBloom.ArchitectureTests;

/// <summary>
/// A module that exists but is never registered is a feature that silently does nothing.
/// </summary>
public class ModuleRegistrationTests
{
    [Fact]
    public void Every_module_in_the_solution_is_registered_with_the_host()
    {
        var declared = TestContext.ModuleAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(IModule).IsAssignableFrom(type) && type is { IsAbstract: false, IsInterface: false })
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var registered = ModuleRegistry.All
            .Select(module => module.GetType().FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var missing = declared.Except(registered, StringComparer.Ordinal).ToList();

        Assert.True(
            missing.Count == 0,
            $"""
             These modules exist but are not in ModuleRegistry.All, so nothing they
             declare will ever be wired into the application:

             {string.Join(Environment.NewLine, missing)}
             """);
    }

    [Fact]
    public void Module_names_are_unique_and_populated()
    {
        foreach (var module in ModuleRegistry.All)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(module.Name),
                $"{module.GetType().Name} has no Name; it appears in logs and health output.");
        }

        var duplicates = ModuleRegistry.All
            .GroupBy(module => module.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.True(
            duplicates.Count == 0,
            $"Duplicate module names make logs ambiguous: {string.Join(", ", duplicates)}");
    }
}
