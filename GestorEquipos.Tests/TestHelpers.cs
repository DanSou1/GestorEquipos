using Gestor_Equipos.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GestorEquipos.Tests
{
    public static class TestHelpers
    {
        public static MyDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<MyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                // Some services wrap multi-step writes (e.g. RAES retirement) in a real
                // transaction for atomicity on SQL Server. InMemory doesn't support
                // transactions, but treats Begin/Commit as safe no-ops once this warning is
                // suppressed — the standard pattern for testing that code path without SQL Server.
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            return new MyDbContext(options);
        }
    }
}
