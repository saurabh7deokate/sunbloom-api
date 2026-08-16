namespace SunBloom.SharedKernel.Modules;

/// <summary>
/// Applies a module's pending database migrations.
/// </summary>
/// <remarks>
/// Each module owns an internal DbContext (ADR-0006), so the host cannot migrate them
/// directly. Modules register an implementation of this instead, keeping persistence
/// internal while still letting the host drive migration at startup.
/// </remarks>
public interface IModuleDatabaseMigrator
{
    string ModuleName { get; }

    Task MigrateAsync(CancellationToken cancellationToken);
}
