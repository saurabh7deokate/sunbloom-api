namespace SunBloom.ArchitectureTests;

/// <summary>
/// Domain code stays free of infrastructure concerns.
/// </summary>
/// <remarks>
/// Module boundaries are compiler-enforced; layering inside a module is not, because
/// splitting each module into three assemblies would cost twelve projects of ceremony
/// (ADR-0001). These tests are that trade-off's other half — without them, the layering
/// in ARCHITECTURE.md §2.2 would be documentation rather than a rule.
/// <para>
/// These scan source text rather than metadata, because a <c>using</c> directive that
/// is present but unused still signals the intent that matters here.
/// </para>
/// </remarks>
public class LayeringTests
{
    private static readonly string[] ForbiddenInDomain =
    [
        "using Microsoft.EntityFrameworkCore",
        "using Microsoft.AspNetCore",
        "using Npgsql",
        "using Dapper",
    ];

    [Fact]
    public void Domain_code_must_not_depend_on_persistence_or_the_web_framework()
    {
        var violations = new List<string>();

        var domainFiles = TestContext.SourceFiles("src")
            .Where(file => file.FullName.Contains(
                $"{Path.DirectorySeparatorChar}Domain{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase));

        foreach (var file in domainFiles)
        {
            var text = File.ReadAllText(file.FullName);

            violations.AddRange(
                from forbidden in ForbiddenInDomain
                where text.Contains(forbidden, StringComparison.Ordinal)
                select $"{TestContext.RelativePath(file)} -> {forbidden}");
        }

        Assert.True(
            violations.Count == 0,
            $"""
             Domain code must not reference persistence or the web framework. Move the
             dependency into the module's Infrastructure or Endpoints layer.

             {string.Join(Environment.NewLine, violations)}
             """);
    }

    [Fact]
    public void Api_contracts_must_not_expose_entity_framework_types()
    {
        var violations = TestContext.SourceFiles("src")
            .Where(file => file.FullName.Contains(
                $"{Path.DirectorySeparatorChar}Contracts{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(file => File.ReadAllText(file.FullName)
                .Contains("using Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
            .Select(TestContext.RelativePath)
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"""
             Contracts must not reference EF Core. An entity leaking into a contract
             couples every consumer to the persistence model.

             {string.Join(Environment.NewLine, violations)}
             """);
    }
}
