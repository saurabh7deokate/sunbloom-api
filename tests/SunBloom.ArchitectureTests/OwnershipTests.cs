using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SunBloom.SharedKernel.Ownership;

namespace SunBloom.ArchitectureTests;

/// <summary>
/// Every entity holding personal data must be scoped to its owner (ADR-0003).
/// </summary>
/// <remarks>
/// This is the guardrail for the one decision that is genuinely expensive to retrofit.
/// Ownership added table-by-table later reliably misses one, and the table it misses is
/// the one that leaks another user's data.
/// <para>
/// It reflects over every registered <see cref="DbContext" />, so a module added in a
/// later slice is covered automatically — the author does not have to know this test
/// exists.
/// </para>
/// </remarks>
public class OwnershipTests
{
    [Fact]
    public void Every_entity_owned_by_a_user_has_a_query_filter()
    {
        var violations = new List<string>();

        foreach (var contextType in DiscoverDbContextTypes())
        {
            using var scope = TestHost.Services.CreateScope();

            if (scope.ServiceProvider.GetService(contextType) is not DbContext context)
            {
                continue;
            }

            foreach (var entity in context.Model.GetEntityTypes())
            {
                if (entity.ClrType is null || !typeof(IOwnedByUser).IsAssignableFrom(entity.ClrType))
                {
                    continue;
                }

                if (entity.GetDeclaredQueryFilters().Count == 0)
                {
                    violations.Add($"{contextType.Name}: {entity.ClrType.Name}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"""
             These entities implement IOwnedByUser but have no global query filter, so a
             query could return another user's data. Configure a filter on OwnerUserId.
             See ADR-0003.

             {string.Join(Environment.NewLine, violations)}
             """);
    }

    [Fact]
    public void Entities_named_like_personal_data_implement_IOwnedByUser()
    {
        // Catches the inverse mistake: an entity that clearly holds personal data but
        // was never marked, so the filter test above would never look at it.
        var suspicious = TestContext.ModuleAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => type.Namespace?.Contains(".Domain", StringComparison.Ordinal) == true)
            .Where(type => type.GetProperty("OwnerUserId") is not null)
            .Where(type => !typeof(IOwnedByUser).IsAssignableFrom(type))
            .Select(type => type.FullName!)
            .ToList();

        Assert.True(
            suspicious.Count == 0,
            $"""
             These entities have an OwnerUserId property but do not implement
             IOwnedByUser, so ownership enforcement will skip them entirely.

             {string.Join(Environment.NewLine, suspicious)}
             """);
    }

    private static IEnumerable<Type> DiscoverDbContextTypes() =>
        TestContext.ModuleAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(DbContext).IsAssignableFrom(type) && type is { IsAbstract: false });
}
