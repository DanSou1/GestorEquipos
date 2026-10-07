using Gestor_Equipos.Data;
using GestorEquipos.Models;
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
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new MyDbContext(options);
        }

        public static Desktop CreateDesktop(MyDbContext db, string serialNumber = "SN-0001", bool estado = true)
        {
            var desktopType = new DesktopType { Brand = "Dell", Model = "Optiplex-" + serialNumber };
            var osVersion = new OSVersion { TypeSO = "Windows", Version = "11" };
            var ram = new Ram { Especification = "16GB" };
            db.DesktopTypes.Add(desktopType);
            db.OSVersions.Add(osVersion);
            db.Rams.Add(ram);
            db.SaveChanges();

            var desktop = new Desktop
            {
                NameDesktop = "PC-" + serialNumber,
                SerialNumber = serialNumber,
                Processor = "i5",
                Disk = "512GB SSD",
                DesktopTypeId = desktopType.Id,
                OSVersionId = osVersion.Id,
                RamId = ram.Id,
                RamType = RamType.DDR4,
                Estado = estado
            };
            db.Desktops.Add(desktop);
            db.SaveChanges();
            return desktop;
        }

        public static Users CreateUser(MyDbContext db, string name, bool activo = true, string? email = null)
        {
            var user = new Users
            {
                Name = name,
                LastName = "Apellido",
                Email = email ?? $"{name.ToLowerInvariant()}@empresa.local",
                EmailTeams = email ?? $"{name.ToLowerInvariant()}@empresa.local",
                Activo = activo
            };
            db.Users.Add(user);
            db.SaveChanges();
            return user;
        }

        public static Users CreateReservedUser(MyDbContext db, string email)
        {
            var user = new Users
            {
                Name = "Disponible",
                LastName = "Sistema",
                Email = email,
                EmailTeams = email,
                Activo = false
            };
            db.Users.Add(user);
            db.SaveChanges();
            return user;
        }
    }
}
