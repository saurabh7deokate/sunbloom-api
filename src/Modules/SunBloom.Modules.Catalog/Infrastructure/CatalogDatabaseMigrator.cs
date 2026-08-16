using Microsoft.EntityFrameworkCore;
using SunBloom.SharedKernel.Modules;

namespace SunBloom.Modules.Catalog.Infrastructure;

internal sealed class CatalogDatabaseMigrator(CatalogDbContext db) : IModuleDatabaseMigrator
{
    public string ModuleName => "Catalog";

    public Task MigrateAsync(CancellationToken cancellationToken) =>
        db.Database.MigrateAsync(cancellationToken);
}
