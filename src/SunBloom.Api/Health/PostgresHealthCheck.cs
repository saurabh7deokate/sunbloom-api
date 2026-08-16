using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace SunBloom.Api.Health;

/// <summary>
/// Readiness check: can the application actually reach its database?
/// </summary>
/// <remarks>
/// A missing connection string reports <see cref="HealthStatus.Unhealthy" /> rather
/// than passing quietly. Readiness means "able to serve traffic", and an instance with
/// no database cannot — a readiness probe that passes without one is lying to whatever
/// is about to send it requests.
/// </remarks>
public sealed class PostgresHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public const string ConnectionStringName = "SunBloomDb";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return HealthCheckResult.Unhealthy(
                $"Connection string '{ConnectionStringName}' is not configured. " +
                $"Set it with: dotnet user-secrets set \"ConnectionStrings:{ConnectionStringName}\" \"...\"");
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy(
                $"PostgreSQL {connection.PostgreSqlVersion} reachable.");
        }
        catch (NpgsqlException ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is not reachable.", ex);
        }
        catch (TimeoutException ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL connection timed out.", ex);
        }
    }
}
