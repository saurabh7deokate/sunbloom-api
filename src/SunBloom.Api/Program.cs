using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using SunBloom.Api.Health;
using SunBloom.Api.Modules;
using SunBloom.Api.Security;
using SunBloom.SharedKernel.Modules;
using SunBloom.SharedKernel.Ownership;
using SunBloom.SharedKernel.Time;

const string serviceName = "SunBloom.Api";

// Bootstrap logger: captures failures that happen before configuration is read.
// Invariant culture keeps log output identical regardless of machine locale.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Time is injected, never ambient. Scoring applies decay, so "now" is a domain input.
    builder.Services.AddSingleton<IClock, SystemClock>();

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

    // Modules register their own named policies; this only enables the middleware.
    builder.Services.AddRateLimiter(options =>
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests);

    // RFC 9457 Problem Details for every error response.
    builder.Services.AddProblemDetails();
    builder.Services.AddOpenApi();

    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy("Process is running."), tags: ["live"])
        .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(serviceName))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter())
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter());

    // Each module registers its own services. The host knows nothing about their internals.
    builder.Services.AddModules(builder.Configuration);

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();

        // Dev convenience only. Production applies migrations as a deliberate
        // deployment step, never as a side effect of a process starting.
        await MigrateModuleDatabasesAsync(app);
    }

    // Liveness: is the process up? Readiness: can it actually serve traffic?
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("live"),
        ResponseWriter = WriteHealthResponseAsync,
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = WriteHealthResponseAsync,
    });

    app.MapModuleEndpoints();

    Log.Information(
        "SunBloom API starting with {ModuleCount} modules: {Modules}",
        ModuleRegistry.All.Count,
        string.Join(", ", ModuleRegistry.All.Select(m => m.Name)));

    app.Run();
    return 0;
}
#pragma warning disable CA1031 // Top-level handler: any unhandled startup failure must be logged before exit.
catch (Exception ex)
{
    Log.Fatal(ex, "SunBloom API terminated unexpectedly");
    return 1;
}
#pragma warning restore CA1031
finally
{
    await Log.CloseAndFlushAsync();
}

static async Task MigrateModuleDatabasesAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();

    foreach (var migrator in scope.ServiceProvider.GetServices<IModuleDatabaseMigrator>())
    {
        await migrator.MigrateAsync(CancellationToken.None);
        Log.Information("Applied migrations for {Module}", migrator.ModuleName);
    }
}

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json; charset=utf-8";

    var payload = new
    {
        status = report.Status.ToString(),
        totalDurationMs = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description,
            durationMs = entry.Value.Duration.TotalMilliseconds,
        }),
    };

    return context.Response.WriteAsync(
        JsonSerializer.Serialize(payload, SunBloom.Api.JsonDefaults.Health));
}

/// <summary>Exposed so integration tests can host the application via WebApplicationFactory.</summary>
public partial class Program;
