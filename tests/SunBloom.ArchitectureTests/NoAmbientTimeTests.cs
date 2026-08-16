namespace SunBloom.ArchitectureTests;

/// <summary>
/// Time is injected via <c>IClock</c>, never read ambiently.
/// </summary>
/// <remarks>
/// Competency scoring applies exponential decay to evidence based on its age, so "now"
/// is an input to the domain. Code calling <c>DateTime.UtcNow</c> directly cannot be
/// tested at a chosen instant, which would leave the central algorithm of the product
/// unverifiable. This is the cheapest possible enforcement of that.
/// </remarks>
public class NoAmbientTimeTests
{
    private static readonly string[] AmbientTimeCalls =
    [
        "DateTime.Now",
        "DateTime.UtcNow",
        "DateTime.Today",
        "DateTimeOffset.Now",
        "DateTimeOffset.UtcNow",
    ];

    /// <summary>The one place allowed to read the system clock — that is its whole job.</summary>
    private static readonly string[] AllowedFiles = ["SystemClock.cs"];

    [Fact]
    public void Production_code_must_use_IClock_instead_of_ambient_time()
    {
        var violations = new List<string>();

        var files = TestContext.SourceFiles("src")
            .Where(file => !AllowedFiles.Contains(file.Name, StringComparer.Ordinal));

        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file.FullName);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)
                    || line.TrimStart().StartsWith("///", StringComparison.Ordinal))
                {
                    continue;
                }

                violations.AddRange(
                    from call in AmbientTimeCalls
                    where line.Contains(call, StringComparison.Ordinal)
                    select $"{TestContext.RelativePath(file)}:{i + 1} -> {call}");
            }
        }

        Assert.True(
            violations.Count == 0,
            $"""
             Inject IClock instead of reading the system clock directly. Scoring applies
             time decay, so untestable time makes the scoring model unverifiable.

             {string.Join(Environment.NewLine, violations)}
             """);
    }
}
