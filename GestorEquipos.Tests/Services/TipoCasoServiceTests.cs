using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.TipoCaso;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class TipoCasoServiceTests
    {
        private static TipoCasoService CreateService(Gestor_Equipos.Data.MyDbContext db)
        {
            return new TipoCasoService(db, new PasswordHasher<UserSystem>());
        }

        private static int SeedAdminUserSystem(Gestor_Equipos.Data.MyDbContext db, string username, string password)
        {
            var role = new Rol { Name = "Administrador" };
            db.Rols.Add(role);
            db.SaveChanges();

            var user = new Users { Name = "Admin", LastName = "User", Email = $"{username}@empresa.com", EmailTeams = $"{username}@empresa.com" };
            db.Users.Add(user);
            db.SaveChanges();

            var hasher = new PasswordHasher<UserSystem>();
            var userSystem = new UserSystem { Username = username, UserId = user.Id, RolId = role.Id, PasswordHash = string.Empty };
            userSystem.PasswordHash = hasher.HashPassword(userSystem, password);
            db.UserSystems.Add(userSystem);
            db.SaveChanges();

            return userSystem.Id;
        }

        [Fact]
        public async Task CreateAsync_CreatesTipoCaso()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            var id = await service.CreateAsync(new TipoCasoCreateViewModel { Nombre = "Soporte de impresora" });

            var created = await service.GetByIdAsync(id);
            Assert.NotNull(created);
            Assert.Equal("Soporte de impresora", created!.Nombre);
        }

        [Fact]
        public async Task CreateAsync_RejectsDuplicateNameCaseInsensitive()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            await service.CreateAsync(new TipoCasoCreateViewModel { Nombre = "Problema de red" });

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateAsync(new TipoCasoCreateViewModel { Nombre = "problema de red" }));
        }

        [Fact]
        public async Task UpdateAsync_RejectsRenameToExistingName()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            await service.CreateAsync(new TipoCasoCreateViewModel { Nombre = "Instalación de software" });
            var otherId = await service.CreateAsync(new TipoCasoCreateViewModel { Nombre = "Otro tipo" });

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.UpdateAsync(otherId, new TipoCasoEditViewModel { Id = otherId, Nombre = "instalación de software" }));
        }

        [Fact]
        public async Task UpdateAsync_AllowsRenamingToOwnCurrentName()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var id = await service.CreateAsync(new TipoCasoCreateViewModel { Nombre = "Instalación de software" });

            await service.UpdateAsync(id, new TipoCasoEditViewModel { Id = id, Nombre = "Instalación de software" });

            var updated = await service.GetByIdAsync(id);
            Assert.Equal("Instalación de software", updated!.Nombre);
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenPasswordMissing()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var adminId = SeedAdminUserSystem(db, "admin-nopw", "AdminPass1!");
            var typeId = await service.CreateAsync(new TipoCasoCreateViewModel { Nombre = "X" });

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.DeleteAsync(typeId, adminId, null));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenPasswordIncorrect()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var adminId = SeedAdminUserSystem(db, "admin-wrongpw", "AdminPass1!");
            var typeId = await service.CreateAsync(new TipoCasoCreateViewModel { Nombre = "X" });

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.DeleteAsync(typeId, adminId, "IncorrectPassword!"));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenReferencedByCasoTI()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var adminId = SeedAdminUserSystem(db, "admin-inuse", "AdminPass1!");
            var typeId = await service.CreateAsync(new TipoCasoCreateViewModel { Nombre = "En uso" });

            var tecnico = new Users { Name = "Tec", LastName = "Nico", Email = "tec@empresa.com", EmailTeams = "tec@empresa.com" };
            var persona = new Users { Name = "Persona", LastName = "Atendida", Email = "persona@empresa.com", EmailTeams = "persona@empresa.com" };
            db.Users.AddRange(tecnico, persona);
            db.SaveChanges();
            var tecnicoRole = new Rol { Name = "Técnico en Sistemas" };
            db.Rols.Add(tecnicoRole);
            db.SaveChanges();
            var tecnicoUserSystem = new UserSystem { Username = "tec-us", UserId = tecnico.Id, RolId = tecnicoRole.Id, PasswordHash = "x" };
            db.UserSystems.Add(tecnicoUserSystem);
            db.SaveChanges();

            db.CasosTI.Add(new CasoTI
            {
                Fecha = DateOnly.FromDateTime(DateTime.Now),
                TecnicoUserSystemId = tecnicoUserSystem.Id,
                PersonaUserId = persona.Id,
                TipoCasoId = typeId,
                CreatedAtUtc = DateTime.UtcNow
            });
            db.SaveChanges();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.DeleteAsync(typeId, adminId, "AdminPass1!"));
        }

        [Fact]
        public async Task DeleteAsync_HappyPath_RemovesUnreferencedType()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);
            var adminId = SeedAdminUserSystem(db, "admin-ok", "AdminPass1!");
            var typeId = await service.CreateAsync(new TipoCasoCreateViewModel { Nombre = "Para borrar" });

            await service.DeleteAsync(typeId, adminId, "AdminPass1!");

            Assert.Null(await service.GetByIdAsync(typeId));
        }
    }
}
