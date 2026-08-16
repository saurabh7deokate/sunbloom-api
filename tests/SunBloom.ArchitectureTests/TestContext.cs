using System.Reflection;
using SunBloom.Modules.Catalog;
using SunBloom.Modules.Competency;
using SunBloom.Modules.Identity;
using SunBloom.Modules.Recommendations;

namespace SunBloom.ArchitectureTests;

/// <summary>Shared helpers for locating assemblies and source files under test.</summary>
internal static class TestContext
{
    /// <summary>
    /// Listed explicitly. Scanning loaded assemblies would silently miss a module whose
    /// types are never touched, which is exactly the case these tests exist to catch.
    /// </summary>
    public static IReadOnlyList<Assembly> ModuleAssemblies { get; } =
    [
        typeof(IdentityModule).Assembly,
        typeof(CatalogModule).Assembly,
        typeof(CompetencyModule).Assembly,
        typeof(RecommendationsModule).Assembly,
    ];

    /// <summary>Walks up from the test output directory to the repository root.</summary>
    public static DirectoryInfo SolutionRoot { get; } = FindSolutionRoot();

    /// <summary>
    /// Anchored on Directory.Build.props rather than the solution file: the .NET 10 SDK
    /// emits SunBloom.slnx, and the marker should not depend on solution-file format.
    /// </summary>
    private const string RootMarker = "Directory.Build.props";

    /// <summary>All C# source files under <paramref name="relativePath" />, excluding build output.</summary>
    public static IEnumerable<FileInfo> SourceFiles(string relativePath)
    {
        var root = new DirectoryInfo(Path.Combine(SolutionRoot.FullName, relativePath));

        if (!root.Exists)
        {
            return [];
        }

        return root
            .EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(file => !file.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                       StringComparison.OrdinalIgnoreCase)
                && !file.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                       StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Path relative to the solution root, for readable assertion messages.</summary>
    public static string RelativePath(FileInfo file) =>
        Path.GetRelativePath(SolutionRoot.FullName, file.FullName);

    private static DirectoryInfo FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !directory.EnumerateFiles(RootMarker).Any())
        {
            directory = directory.Parent;
        }

        return directory
            ?? throw new InvalidOperationException(
                $"Could not locate {RootMarker} walking up from {AppContext.BaseDirectory}.");
    }
}
