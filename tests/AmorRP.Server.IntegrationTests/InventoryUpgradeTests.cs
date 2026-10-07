using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Fact]
    public async Task M2_upgrade_seeds_categories_and_preserves_existing_currency_and_membership()
    {
        using var f = Factory(); await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        var ct = TestContext.Current.CancellationToken;
        await db.GetService<IMigrator>().MigrateAsync("20261007075330_GroupFoundation", ct);
        var actor = Guid.NewGuid(); var group = Guid.NewGuid(); var currency = Guid.NewGuid(); var now = clock.GetUtcNow();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO characters ("Id", "LodestoneId", "OwnershipKeyHash", "DisplayName", "HomeWorldId", "HomeWorldName", "VerifiedAt")
            VALUES ({actor}, '999', 'test-binding', 'Existing owner', '35', 'Zalera', {now});
            INSERT INTO groups ("Id", "Name", "Description", "OwnerCharacterId", "Version", "Deleted", "CurrencyId", "CurrencyName", "CurrencySymbol", "CurrencyVersion", "PolicyVersion", "CurrentPotionPoints", "CurrentLetters", "NextPotionPoints", "NextLetters", "PolicyPeriod")
            VALUES ({group}, 'Existing group', '', {actor}, 1, false, {currency}, 'Existing coins', 'C', 1, 1, 12, 5, 12, 5, {now});
            INSERT INTO memberships ("GroupId", "CharacterId", "Status", "Capabilities", "TradingRestricted", "Version", "Owned", "Reserved", "BalanceVersion")
            VALUES ({group}, {actor}, 'active', ARRAY[]::text[], false, 1, 12345, 0, 1);
            """, ct);
        await db.Database.MigrateAsync(ct);
        Assert.Equal(currency, (await db.Groups.SingleAsync(ct)).CurrencyId);
        Assert.Equal(12345, (await db.Memberships.SingleAsync(ct)).Owned);
        Assert.Equal(new[] { "Consumables", "Correspondence" }, await db.Categories.OrderBy(x => x.Name).Select(x => x.Name).ToArrayAsync(ct));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(ct));
    }
}
