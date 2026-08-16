namespace SunBloom.SharedKernel.Modules;

/// <summary>
/// Seeds a module's baseline content. Must be idempotent — it runs on every
/// Development startup.
/// </summary>
public interface IModuleSeeder
{
    string ModuleName { get; }

    Task SeedAsync(CancellationToken cancellationToken);
}
