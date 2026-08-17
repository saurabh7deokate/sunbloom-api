using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SunBloom.ContentGenerator;
using SunBloom.ContentGenerator.Ai;
using SunBloom.ContentGenerator.Api;
using SunBloom.ContentGenerator.Generation;

// Offline content generation. No part of this runs in the API (ADR-0005) — if this tool
// breaks, the product does not.

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    PrintUsage();
    return 0;
}

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(Program).Assembly)
    .AddEnvironmentVariables()
    .Build();

var gemini = configuration.GetSection(GeminiOptions.SectionName).Get<GeminiOptions>() ?? new GeminiOptions();
var sunbloom = configuration.GetSection(SunBloomOptions.SectionName).Get<SunBloomOptions>() ?? new SunBloomOptions();

var missing = new List<string>();
if (string.IsNullOrWhiteSpace(gemini.ApiKey)) missing.Add("Gemini:ApiKey");
if (string.IsNullOrWhiteSpace(sunbloom.Email)) missing.Add("SunBloom:Email");
if (string.IsNullOrWhiteSpace(sunbloom.Password)) missing.Add("SunBloom:Password");

if (missing.Count > 0)
{
    // Fail before doing any work, naming every missing key at once rather than one per run.
    Console.Error.WriteLine($"Missing configuration: {string.Join(", ", missing)}");
    Console.Error.WriteLine();
    PrintUsage();
    return 1;
}

var services = new ServiceCollection();

services.AddLogging(builder => builder
    .AddSimpleConsole(console =>
    {
        console.SingleLine = true;
        console.TimestampFormat = "HH:mm:ss ";
    })
    .SetMinimumLevel(LogLevel.Information)
    // The per-request handler logs bury the generation output they surround.
    .AddFilter("System.Net.Http.HttpClient", LogLevel.Warning));

services.AddSingleton(gemini);
services.AddSingleton(sunbloom);

// Generation calls are slow by nature; the default 100s timeout truncates them.
services.AddHttpClient(GeminiClientName, client => client.Timeout = TimeSpan.FromMinutes(5));
services.AddHttpClient(AdminClientName, client => client.Timeout = TimeSpan.FromSeconds(60));

// Registered as singletons on purpose. AddHttpClient<T> would register the typed client
// as TRANSIENT, and both of these carry state across calls that must survive: the admin
// client holds the bearer token from sign-in, and the Gemini client holds the resolved
// model version used for provenance. With transient registration the token is set on one
// instance and every later request goes out unauthenticated — a 200 login followed by a
// wall of 401s. Holding a factory HttpClient for the process lifetime risks DNS
// staleness in a long-running service; this is a CLI that exits in minutes.
services.AddSingleton<SunBloomAdminClient>(provider => new SunBloomAdminClient(
    provider.GetRequiredService<IHttpClientFactory>().CreateClient(AdminClientName),
    provider.GetRequiredService<SunBloomOptions>(),
    provider.GetRequiredService<ILogger<SunBloomAdminClient>>()));

services.AddSingleton<IStructuredCompletion>(provider => new GeminiStructuredCompletion(
    provider.GetRequiredService<IHttpClientFactory>().CreateClient(GeminiClientName),
    provider.GetRequiredService<GeminiOptions>(),
    provider.GetRequiredService<ILogger<GeminiStructuredCompletion>>()));

services.AddSingleton<SkillGenerator>();

await using var provider = services.BuildServiceProvider();

var logger = provider.GetRequiredService<ILogger<SkillGenerator>>();
var api = provider.GetRequiredService<SunBloomAdminClient>();

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

if (!await api.SignInAsync(cancellation.Token))
{
    return 1;
}

var generator = provider.GetRequiredService<SkillGenerator>();

try
{
    switch (args[0])
    {
        case "children":
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: children <parent-slug> [count]");
                return 1;
            }

            var count = args.Length > 2 && int.TryParse(args[2], out var parsed) ? parsed : 8;
            await generator.GenerateChildrenAsync(args[1], Math.Clamp(count, 1, 30), cancellation.Token);
            return 0;

        case "prerequisites":
            await generator.GeneratePrerequisitesAsync(cancellation.Token);
            return 0;

        default:
            Console.Error.WriteLine($"Unknown command '{args[0]}'.");
            PrintUsage();
            return 1;
    }
}
catch (OperationCanceledException)
{
    GeneratorLog.Fatal(logger, "Cancelled. Anything already created stays as a draft and is safe to review.");
    return 130;
}

static void PrintUsage()
{
    Console.WriteLine(
        """
        SunBloom content generator — offline, human-reviewed content authoring.

        Usage:
          children <parent-slug> [count]   Generate direct children of a skill (default 8)
          prerequisites                    Propose prerequisite edges across existing skills

        Everything is created as a Draft. Nothing reaches learners until you approve it at
        GET /api/v1/admin/skills/pending — generate one level, review it, then generate the
        next level under what you approved.

        Configuration (user-secrets on this project):
          dotnet user-secrets set "Gemini:ApiKey"     "..." --project tools/SunBloom.ContentGenerator
          dotnet user-secrets set "SunBloom:Email"    "..." --project tools/SunBloom.ContentGenerator
          dotnet user-secrets set "SunBloom:Password" "..." --project tools/SunBloom.ContentGenerator

        The account must hold the ContentAdmin role, and the API must be running.
        """);
}

/// <summary>Anchor for user-secrets assembly lookup.</summary>
internal sealed partial class Program
{
    internal const string GeminiClientName = "gemini";
    internal const string AdminClientName = "sunbloom-admin";
}
