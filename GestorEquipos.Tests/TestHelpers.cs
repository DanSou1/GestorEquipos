using Gestor_Equipos.Data;
using Microsoft.EntityFrameworkCore;

namespace GestorEquipos.Tests
{
    public static class TestHelpers
    {
        public static MyDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<MyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new MyDbContext(options);
        }
    }
}
