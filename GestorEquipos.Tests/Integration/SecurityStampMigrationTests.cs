using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GestorEquipos.Tests.Integration
{
    public class SecurityStampMigrationTests
    {
        [SqlServerFact]
        public async Task Migration_ExistingAccounts_GetDistinctStampsWithoutChangingCredentials()
        {
            using var factory = new SessionApplicationFactory();
            var first = await factory.CreateAccountAsync();
            var second = await factory.CreateAccountAsync();
            await factory.WithDatabaseAsync(async (database, _) =>
            {
                const string migrationName = "20260908120000_AddUserSystemSecurityStamp";
                var migrations = database.Database.GetMigrations().ToList();
                var index = migrations.IndexOf(migrationName);
                Assert.True(index > 0, "Expected migration not found in history.");
                Assert.False(database.Database.HasPendingModelChanges());
                var migrator = database.GetService<IMigrator>();
                await migrator.MigrateAsync(migrations[index - 1]);
                await migrator.MigrateAsync(migrationName);
                database.ChangeTracker.Clear();
                var migratedFirst = await database.UserSystems.SingleAsync(account => account.Id == first.Id);
                var migratedSecond = await database.UserSystems.SingleAsync(account => account.Id == second.Id);
                Assert.NotEqual(Guid.Empty, migratedFirst.SecurityStamp);
                Assert.NotEqual(Guid.Empty, migratedSecond.SecurityStamp);
                Assert.NotEqual(migratedFirst.SecurityStamp, migratedSecond.SecurityStamp);
                Assert.Equal(first.PasswordHash, migratedFirst.PasswordHash);
                Assert.Equal(second.PasswordHash, migratedSecond.PasswordHash);
                Assert.Equal(first.RolId, migratedFirst.RolId);
                Assert.Equal(second.UserId, migratedSecond.UserId);
                var stamp = migratedFirst.SecurityStamp;
                await migrator.MigrateAsync(migrationName);
                database.ChangeTracker.Clear();
                Assert.Equal(stamp, (await database.UserSystems.FindAsync(first.Id))!.SecurityStamp);
            });
        }
    }
}
