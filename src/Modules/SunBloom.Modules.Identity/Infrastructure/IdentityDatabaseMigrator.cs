using Microsoft.EntityFrameworkCore;
using SunBloom.SharedKernel.Modules;

namespace SunBloom.Modules.Identity.Infrastructure;

internal sealed class IdentityDatabaseMigrator(IdentityDbContext db) : IModuleDatabaseMigrator
{
    public string ModuleName => "Identity";

    public Task MigrateAsync(CancellationToken cancellationToken) =>
        db.Database.MigrateAsync(cancellationToken);
}
