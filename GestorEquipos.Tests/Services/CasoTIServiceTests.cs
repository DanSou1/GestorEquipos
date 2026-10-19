using Gestor_Equipos.Data;
using Gestor_Equipos.Services;
using Gestor_Equipos.Services.Auth;
using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.CasoTI;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestorEquipos.Tests.Services
{
    public class CasoTIServiceTests
    {
        private class RecordingEmailService : IEmailService
        {
            public List<(string ToEmail, string ToName, string Subject, string BodyHtml)> Sent { get; } = new();
            public bool ThrowOnSend { get; set; }

            public Task SendAsync(string toEmail, string toName, string subject, string bodyHtml,
                IReadOnlyList<EmailAttachment> attachments, CancellationToken cancellationToken = default)
            {
                if (ThrowOnSend)
                {
                    throw new InvalidOperationException("SMTP falló (simulado).");
                }

                Sent.Add((toEmail, toName, subject, bodyHtml));
                return Task.CompletedTask;
            }
        }

        private static CasoTIService CreateService(MyDbContext db, RecordingEmailService? emailService = null) => new(db, emailService ?? new RecordingEmailService());

        private static int SeedTipoCaso(MyDbContext db, string nombre = "Soporte de impresora")
        {
            var tipo = new TipoCaso { Nombre = nombre };
            db.TiposCaso.Add(tipo);
            db.SaveChanges();
            return tipo.Id;
        }

        private static (int UserSystemId, Users Persona) SeedTecnico(MyDbContext db, string username, string roleName, Area? area = null)
        {
            var role = db.Rols.FirstOrDefault(r => r.Name == roleName) ?? new Rol { Name = roleName };
            if (role.Id == 0)
            {
                db.Rols.Add(role);
                db.SaveChanges();
            }

            var persona = new Users { Name = username, LastName = "Apellido", Email = $"{username}@empresa.local", EmailTeams = $"{username}@empresa.local" };
            db.Users.Add(persona);
            db.SaveChanges();

            var userSystem = new UserSystem { Username = username, UserId = persona.Id, RolId = role.Id, PasswordHash = "x" };
            db.UserSystems.Add(userSystem);
            db.SaveChanges();

            return (userSystem.Id, persona);
        }

        private static Users SeedPersonaAtendida(MyDbContext db, string name, Area? area = null)
        {
            var persona = new Users
            {
                Name = name,
                LastName = "Atendida",
                Email = $"{name.ToLowerInvariant()}@empresa.local",
                EmailTeams = $"{name.ToLowerInvariant()}@empresa.local",
                AreaId = area?.Id
            };
            db.Users.Add(persona);
            db.SaveChanges();
            return persona;
        }

        private static CasoTICreateViewModel NewVm(int personaId, int tipoCasoId, DateOnly? fecha = null, string asunto = "Asunto de prueba") => new()
        {
            Fecha = fecha ?? DateOnly.FromDateTime(DateTime.Now),
            Asunto = asunto,
            PersonaUserId = personaId,
            TipoCasoId = tipoCasoId
        };

        [Fact]
        public async Task CreateAsync_AlwaysLeavesTecnicoUnassigned_RegardlessOfCreatorRole()
        {
            using var db = TestHelpers.CreateDbContext();
            var (adminId, adminPersona) = SeedTecnico(db, "admin1", AuthBootstrapper.AdministradorRoleName);
            var (tecnicoId, tecnicoPersona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            var idByAdmin = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), adminId, adminPersona.Id, AuthBootstrapper.AdministradorRoleName);
            var idByTecnico = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tecnicoId, tecnicoPersona.Id, AuthBootstrapper.TecnicoSistemasRoleName);

            Assert.Null((await db.CasosTI.SingleAsync(c => c.Id == idByAdmin)).TecnicoUserSystemId);
            Assert.Null((await db.CasosTI.SingleAsync(c => c.Id == idByTecnico)).TecnicoUserSystemId);
        }

        [Fact]
        public async Task CreateAsync_ByColaborador_ForcesPersonaUserIdToActingUsersId()
        {
            using var db = TestHelpers.CreateDbContext();
            var (colaboradorAccountId, colaboradorPersona) = SeedTecnico(db, "colab1", AuthBootstrapper.ColaboradorRoleName);
            var otraPersona = SeedPersonaAtendida(db, "Otra");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            // Intenta hacerse pasar por "otraPersona" — el servicio debe ignorarlo.
            var id = await service.CreateAsync(
                NewVm(otraPersona.Id, tipoCasoId), colaboradorAccountId, colaboradorPersona.Id, AuthBootstrapper.ColaboradorRoleName);

            var caso = await db.CasosTI.SingleAsync(c => c.Id == id);
            Assert.Equal(colaboradorPersona.Id, caso.PersonaUserId);
        }

        [Fact]
        public async Task CreateAsync_SnapshotsAreaAtCreationTime_UnaffectedByLaterChange()
        {
            using var db = TestHelpers.CreateDbContext();
            var areaVentas = new Area { Name = "Ventas" };
            var areaSoporte = new Area { Name = "Soporte" };
            db.Areas.AddRange(areaVentas, areaSoporte);
            db.SaveChanges();

            var (tecnicoId, tecnicoPersona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1", areaVentas);
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            var id = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tecnicoId, tecnicoPersona.Id, AuthBootstrapper.TecnicoSistemasRoleName);

            persona.AreaId = areaSoporte.Id;
            await db.SaveChangesAsync();

            var caso = await db.CasosTI.SingleAsync(c => c.Id == id);
            Assert.Equal("Ventas", caso.AreaSnapshot);
        }

        [Fact]
        public async Task GetIndexAsync_Tecnico_SeesAllCases()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, tec1Persona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var (tec2Id, tec2Persona) = SeedTecnico(db, "tec2", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tec1Id, tec1Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName);
            await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tec2Id, tec2Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName);

            var result = await service.GetIndexAsync(tec1Id, tec1Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName, new CasoTIIndexFilterViewModel());

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetIndexAsync_Colaborador_OnlySeesOwnCasesAsPersona()
        {
            using var db = TestHelpers.CreateDbContext();
            var (colab1Id, colab1Persona) = SeedTecnico(db, "colab1", AuthBootstrapper.ColaboradorRoleName);
            var (colab2Id, colab2Persona) = SeedTecnico(db, "colab2", AuthBootstrapper.ColaboradorRoleName);
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            await service.CreateAsync(NewVm(colab1Persona.Id, tipoCasoId), colab1Id, colab1Persona.Id, AuthBootstrapper.ColaboradorRoleName);
            await service.CreateAsync(NewVm(colab2Persona.Id, tipoCasoId), colab2Id, colab2Persona.Id, AuthBootstrapper.ColaboradorRoleName);

            var result = await service.GetIndexAsync(colab1Id, colab1Persona.Id, AuthBootstrapper.ColaboradorRoleName, new CasoTIIndexFilterViewModel());

            Assert.Single(result);
            Assert.Equal($"{colab1Persona.Name} {colab1Persona.LastName}", result[0].PersonaNombre);
        }

        [Fact]
        public async Task GetIndexAsync_Admin_SeesAllOrFilteredByTecnico()
        {
            using var db = TestHelpers.CreateDbContext();
            var (adminId, adminPersona) = SeedTecnico(db, "admin1", AuthBootstrapper.AdministradorRoleName);
            var (tec1Id, tec1Persona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var (tec2Id, tec2Persona) = SeedTecnico(db, "tec2", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            var id1 = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tec1Id, tec1Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName);
            var id2 = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tec2Id, tec2Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName);
            // Simula que cada caso ya fue resuelto por su respectivo técnico (un caso recién
            // creado ya no nace asignado, ver CreateAsync_AlwaysLeavesTecnicoUnassigned...).
            (await db.CasosTI.SingleAsync(c => c.Id == id1)).TecnicoUserSystemId = tec1Id;
            (await db.CasosTI.SingleAsync(c => c.Id == id2)).TecnicoUserSystemId = tec2Id;
            await db.SaveChangesAsync();

            var all = await service.GetIndexAsync(adminId, adminPersona.Id, AuthBootstrapper.AdministradorRoleName, new CasoTIIndexFilterViewModel());
            Assert.Equal(2, all.Count);

            var filtered = await service.GetIndexAsync(adminId, adminPersona.Id, AuthBootstrapper.AdministradorRoleName,
                new CasoTIIndexFilterViewModel { TecnicoUserSystemId = tec1Id });
            Assert.Single(filtered);
        }

        [Fact]
        public async Task GetIndexAsync_FiltersByAsuntoPersonaFechaEstado()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tecId, tecPersona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Daniel");
            var otraPersona = SeedPersonaAtendida(db, "Marcela");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            var idImpresora = await service.CreateAsync(
                NewVm(persona.Id, tipoCasoId, new DateOnly(2026, 3, 1), "Impresora no imprime"), tecId, tecPersona.Id, AuthBootstrapper.TecnicoSistemasRoleName);
            var idRed = await service.CreateAsync(
                NewVm(otraPersona.Id, tipoCasoId, new DateOnly(2026, 6, 1), "Sin acceso a internet"), tecId, tecPersona.Id, AuthBootstrapper.TecnicoSistemasRoleName);

            var porAsunto = await service.GetIndexAsync(tecId, tecPersona.Id, AuthBootstrapper.TecnicoSistemasRoleName,
                new CasoTIIndexFilterViewModel { Asunto = "impresora" });
            Assert.Single(porAsunto);
            Assert.Equal(idImpresora, porAsunto[0].Id);

            var porPersona = await service.GetIndexAsync(tecId, tecPersona.Id, AuthBootstrapper.TecnicoSistemasRoleName,
                new CasoTIIndexFilterViewModel { PersonaNombre = "Marcela" });
            Assert.Single(porPersona);
            Assert.Equal(idRed, porPersona[0].Id);

            var porFecha = await service.GetIndexAsync(tecId, tecPersona.Id, AuthBootstrapper.TecnicoSistemasRoleName,
                new CasoTIIndexFilterViewModel { FechaDesde = new DateOnly(2026, 5, 1) });
            Assert.Single(porFecha);
            Assert.Equal(idRed, porFecha[0].Id);

            var resueltoId = idImpresora;
            (await db.CasosTI.SingleAsync(c => c.Id == resueltoId)).Estado = CasoTIEstado.Resuelto;
            await db.SaveChangesAsync();

            var porEstado = await service.GetIndexAsync(tecId, tecPersona.Id, AuthBootstrapper.TecnicoSistemasRoleName,
                new CasoTIIndexFilterViewModel { Estado = CasoTIEstado.Resuelto });
            Assert.Single(porEstado);
            Assert.Equal(idImpresora, porEstado[0].Id);
        }

        [Fact]
        public async Task GetIndexAsync_Auditor_OnlySeesResueltoCases()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, tec1Persona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var (auditorId, auditorPersona) = SeedTecnico(db, "auditor1", AuthBootstrapper.AuditorRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            var abiertoId = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tec1Id, tec1Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName);
            var resueltoId = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tec1Id, tec1Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName);
            var resuelto = await db.CasosTI.SingleAsync(c => c.Id == resueltoId);
            resuelto.Estado = CasoTIEstado.Resuelto;
            await db.SaveChangesAsync();

            var result = await service.GetIndexAsync(auditorId, auditorPersona.Id, AuthBootstrapper.AuditorRoleName, new CasoTIIndexFilterViewModel());

            Assert.Single(result);
            Assert.DoesNotContain(result, r => r.Id == abiertoId);
        }

        [Fact]
        public async Task GetDetailAsync_Tecnico_CanViewAnyCase()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, tec1Persona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var (tec2Id, tec2Persona) = SeedTecnico(db, "tec2", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            var casoId = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tec1Id, tec1Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName);

            var result = await service.GetDetailAsync(casoId, tec2Id, tec2Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName);

            Assert.NotNull(result);
            Assert.True(result!.CanResolve);
        }

        [Fact]
        public async Task GetDetailAsync_Colaborador_ReturnsNullForOtherPersonaCase()
        {
            using var db = TestHelpers.CreateDbContext();
            var (colab1Id, colab1Persona) = SeedTecnico(db, "colab1", AuthBootstrapper.ColaboradorRoleName);
            var (colab2Id, colab2Persona) = SeedTecnico(db, "colab2", AuthBootstrapper.ColaboradorRoleName);
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            var casoId = await service.CreateAsync(NewVm(colab1Persona.Id, tipoCasoId), colab1Id, colab1Persona.Id, AuthBootstrapper.ColaboradorRoleName);

            var result = await service.GetDetailAsync(casoId, colab2Id, colab2Persona.Id, AuthBootstrapper.ColaboradorRoleName);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetDetailAsync_Auditor_ReturnsNullWhileAbierto_NonNullOnceResuelto()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, tec1Persona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var (auditorId, auditorPersona) = SeedTecnico(db, "auditor1", AuthBootstrapper.AuditorRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            var casoId = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tec1Id, tec1Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName);

            Assert.Null(await service.GetDetailAsync(casoId, auditorId, auditorPersona.Id, AuthBootstrapper.AuditorRoleName));

            var caso = await db.CasosTI.SingleAsync(c => c.Id == casoId);
            caso.Estado = CasoTIEstado.Resuelto;
            await db.SaveChangesAsync();

            Assert.NotNull(await service.GetDetailAsync(casoId, auditorId, auditorPersona.Id, AuthBootstrapper.AuditorRoleName));
        }

        [Fact]
        public async Task ResolveAsync_AnyTecnicoCanResolveAnyOpenCase_AndGetsAssignedAsOwner()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, tec1Persona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var (tec2Id, tec2Persona) = SeedTecnico(db, "tec2", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var emailService = new RecordingEmailService();
            var service = CreateService(db, emailService);
            var casoId = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tec1Id, tec1Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName);

            await service.ResolveAsync(casoId, "Resuelto por tec2", tec2Id, AuthBootstrapper.TecnicoSistemasRoleName, token => $"https://x/{token}");

            var caso = await db.CasosTI.SingleAsync(c => c.Id == casoId);
            Assert.Equal(CasoTIEstado.Resuelto, caso.Estado);
            Assert.Equal(tec2Id, caso.TecnicoUserSystemId);
            Assert.Single(emailService.Sent);
        }

        [Fact]
        public async Task ResolveAsync_ThrowsWhenPersonaEmailEmpty()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, tec1Persona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = new Users { Name = "SinCorreo", LastName = "Apellido", Email = "", EmailTeams = "" };
            db.Users.Add(persona);
            await db.SaveChangesAsync();
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);
            var casoId = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tec1Id, tec1Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ResolveAsync(casoId, "Resuelto", tec1Id, AuthBootstrapper.TecnicoSistemasRoleName, token => $"https://x/{token}"));
        }

        [Fact]
        public async Task ResolveAsync_ThrowsWhenNotasEmpty()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, tec1Persona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);
            var casoId = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tec1Id, tec1Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ResolveAsync(casoId, "  ", tec1Id, AuthBootstrapper.TecnicoSistemasRoleName, token => $"https://x/{token}"));
        }

        [Fact]
        public async Task ResolveAsync_ThrowsOnDoubleResolve()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, tec1Persona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var emailService = new RecordingEmailService();
            var service = CreateService(db, emailService);
            var casoId = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tec1Id, tec1Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName);

            await service.ResolveAsync(casoId, "Resuelto", tec1Id, AuthBootstrapper.TecnicoSistemasRoleName, token => $"https://x/{token}");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ResolveAsync(casoId, "Resuelto otra vez", tec1Id, AuthBootstrapper.TecnicoSistemasRoleName, token => $"https://x/{token}"));

            Assert.Single(emailService.Sent);
        }

        [Fact]
        public async Task ResolveAsync_HappyPath_SetsStateTokenAssignsTecnicoAndSendsEmail()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, tec1Persona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var emailService = new RecordingEmailService();
            var service = CreateService(db, emailService);
            var casoId = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tec1Id, tec1Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName);

            await service.ResolveAsync(casoId, "Se reemplazó el cartucho", tec1Id, AuthBootstrapper.TecnicoSistemasRoleName, token => $"https://x/{token}");

            var caso = await db.CasosTI.SingleAsync(c => c.Id == casoId);
            Assert.Equal(CasoTIEstado.Resuelto, caso.Estado);
            Assert.Equal("Se reemplazó el cartucho", caso.Notas);
            Assert.Equal(tec1Id, caso.TecnicoUserSystemId);
            Assert.NotNull(caso.ResolvedAtUtc);
            Assert.NotNull(caso.RatingToken);
            Assert.NotEqual(Guid.Empty, caso.RatingToken!.Value);

            Assert.Single(emailService.Sent);
            Assert.Equal(persona.Email, emailService.Sent[0].ToEmail);
            Assert.Contains(caso.RatingToken.Value.ToString(), emailService.Sent[0].BodyHtml);
        }

        [Fact]
        public async Task ResolveAsync_LeavesCaseAbiertoWhenEmailFails()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, tec1Persona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var emailService = new RecordingEmailService { ThrowOnSend = true };
            var service = CreateService(db, emailService);
            var casoId = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tec1Id, tec1Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ResolveAsync(casoId, "Resuelto", tec1Id, AuthBootstrapper.TecnicoSistemasRoleName, token => $"https://x/{token}"));

            var caso = await db.CasosTI.SingleAsync(c => c.Id == casoId);
            Assert.Equal(CasoTIEstado.Abierto, caso.Estado);
            Assert.Null(caso.ResolvedAtUtc);
            Assert.Null(caso.RatingToken);
            Assert.Null(caso.TecnicoUserSystemId);
        }

        [Fact]
        public async Task ResolveAsync_Admin_CanResolveAnyCase()
        {
            using var db = TestHelpers.CreateDbContext();
            var (adminId, adminPersona) = SeedTecnico(db, "admin1", AuthBootstrapper.AdministradorRoleName);
            var (tec1Id, tec1Persona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);
            var casoId = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tec1Id, tec1Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName);

            await service.ResolveAsync(casoId, "Resuelto por admin", adminId, AuthBootstrapper.AdministradorRoleName, token => $"https://x/{token}");

            var caso = await db.CasosTI.SingleAsync(c => c.Id == casoId);
            Assert.Equal(CasoTIEstado.Resuelto, caso.Estado);
            Assert.Equal(adminId, caso.TecnicoUserSystemId);
        }

        [Fact]
        public async Task DeleteAsync_Colaborador_CanDeleteOwnOpenCase()
        {
            using var db = TestHelpers.CreateDbContext();
            var (colabId, colabPersona) = SeedTecnico(db, "colab1", AuthBootstrapper.ColaboradorRoleName);
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);
            var casoId = await service.CreateAsync(NewVm(colabPersona.Id, tipoCasoId), colabId, colabPersona.Id, AuthBootstrapper.ColaboradorRoleName);

            await service.DeleteAsync(casoId, colabPersona.Id);

            Assert.False(await db.CasosTI.AnyAsync(c => c.Id == casoId));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenNotOwner()
        {
            using var db = TestHelpers.CreateDbContext();
            var (colab1Id, colab1Persona) = SeedTecnico(db, "colab1", AuthBootstrapper.ColaboradorRoleName);
            var (_, colab2Persona) = SeedTecnico(db, "colab2", AuthBootstrapper.ColaboradorRoleName);
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);
            var casoId = await service.CreateAsync(NewVm(colab1Persona.Id, tipoCasoId), colab1Id, colab1Persona.Id, AuthBootstrapper.ColaboradorRoleName);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(casoId, colab2Persona.Id));
            Assert.True(await db.CasosTI.AnyAsync(c => c.Id == casoId));
        }

        [Fact]
        public async Task DeleteAsync_ThrowsWhenCaseIsResuelto()
        {
            using var db = TestHelpers.CreateDbContext();
            var (colabId, colabPersona) = SeedTecnico(db, "colab1", AuthBootstrapper.ColaboradorRoleName);
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);
            var casoId = await service.CreateAsync(NewVm(colabPersona.Id, tipoCasoId), colabId, colabPersona.Id, AuthBootstrapper.ColaboradorRoleName);
            var caso = await db.CasosTI.SingleAsync(c => c.Id == casoId);
            caso.Estado = CasoTIEstado.Resuelto;
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(casoId, colabPersona.Id));
            Assert.True(await db.CasosTI.AnyAsync(c => c.Id == casoId));
        }

        [Fact]
        public async Task GetRatingPromptAsync_ReturnsNullForUnknownToken()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            Assert.Null(await service.GetRatingPromptAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task GetRatingPromptAsync_ReturnsCaseForValidUnsubmittedToken()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, tec1Persona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);
            var casoId = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tec1Id, tec1Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName);
            await service.ResolveAsync(casoId, "Resuelto", tec1Id, AuthBootstrapper.TecnicoSistemasRoleName, token => $"https://x/{token}");
            var caso = await db.CasosTI.SingleAsync(c => c.Id == casoId);

            var prompt = await service.GetRatingPromptAsync(caso.RatingToken!.Value);

            Assert.NotNull(prompt);
            Assert.False(prompt!.AlreadySubmitted);
            Assert.Equal("Resuelto", prompt.Notas);
        }

        [Fact]
        public async Task SubmitRatingAsync_ReturnsTokenNotFoundAndWritesNothing()
        {
            using var db = TestHelpers.CreateDbContext();
            var service = CreateService(db);

            var result = await service.SubmitRatingAsync(Guid.NewGuid(), 5, "Excelente");

            Assert.Equal(CasoTIRatingSubmitResult.TokenNotFound, result);
        }

        [Fact]
        public async Task SubmitRatingAsync_HappyPath_PersistsRating()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, tec1Persona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);
            var casoId = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tec1Id, tec1Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName);
            await service.ResolveAsync(casoId, "Resuelto", tec1Id, AuthBootstrapper.TecnicoSistemasRoleName, token => $"https://x/{token}");
            var token = (await db.CasosTI.SingleAsync(c => c.Id == casoId)).RatingToken!.Value;

            var result = await service.SubmitRatingAsync(token, 5, "Excelente atención");

            Assert.Equal(CasoTIRatingSubmitResult.Ok, result);
            var caso = await db.CasosTI.SingleAsync(c => c.Id == casoId);
            Assert.Equal(5, caso.RatingValue);
            Assert.Equal("Excelente atención", caso.RatingComment);
            Assert.NotNull(caso.RatingSubmittedAtUtc);
        }

        [Fact]
        public async Task SubmitRatingAsync_SecondSubmitDoesNotOverwriteFirst()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, tec1Persona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);
            var casoId = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tec1Id, tec1Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName);
            await service.ResolveAsync(casoId, "Resuelto", tec1Id, AuthBootstrapper.TecnicoSistemasRoleName, token => $"https://x/{token}");
            var token = (await db.CasosTI.SingleAsync(c => c.Id == casoId)).RatingToken!.Value;

            var first = await service.SubmitRatingAsync(token, 5, "Excelente");
            var second = await service.SubmitRatingAsync(token, 1, "Intento de sobreescribir");

            Assert.Equal(CasoTIRatingSubmitResult.Ok, first);
            Assert.Equal(CasoTIRatingSubmitResult.AlreadySubmitted, second);
            var caso = await db.CasosTI.SingleAsync(c => c.Id == casoId);
            Assert.Equal(5, caso.RatingValue);
            Assert.Equal("Excelente", caso.RatingComment);
        }

        private async Task<int> CreateCasoOnDateAsync(MyDbContext db, CasoTIService service, int tecnicoId, int tecnicoPersonaId, int personaId, int tipoCasoId, DateOnly fecha)
        {
            var id = await service.CreateAsync(NewVm(personaId, tipoCasoId, fecha), tecnicoId, tecnicoPersonaId, AuthBootstrapper.TecnicoSistemasRoleName);
            return id;
        }

        [Fact]
        public async Task GetStatsAsync_DiaSemana_BucketsByCalendarWeekday()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tecId, tecPersona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            // 2026-01-05 es lunes, 2026-01-06 es martes.
            await CreateCasoOnDateAsync(db, service, tecId, tecPersona.Id, persona.Id, tipoCasoId, new DateOnly(2026, 1, 5));
            await CreateCasoOnDateAsync(db, service, tecId, tecPersona.Id, persona.Id, tipoCasoId, new DateOnly(2026, 1, 5));
            await CreateCasoOnDateAsync(db, service, tecId, tecPersona.Id, persona.Id, tipoCasoId, new DateOnly(2026, 1, 6));

            var stats = await service.GetStatsAsync(tecId, tecPersona.Id, AuthBootstrapper.TecnicoSistemasRoleName, null, CasoTIGranularidad.DiaSemana, null, null);

            Assert.Equal(7, stats.BarChart.Labels.Count);
            Assert.Equal("Lunes", stats.BarChart.Labels[0]);
            Assert.Equal(2, stats.BarChart.Values[0]);
            Assert.Equal("Martes", stats.BarChart.Labels[1]);
            Assert.Equal(1, stats.BarChart.Values[1]);
        }

        [Fact]
        public async Task GetStatsAsync_Semana_GroupsSameIsoWeekTogether()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tecId, tecPersona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            await CreateCasoOnDateAsync(db, service, tecId, tecPersona.Id, persona.Id, tipoCasoId, new DateOnly(2026, 1, 5));
            await CreateCasoOnDateAsync(db, service, tecId, tecPersona.Id, persona.Id, tipoCasoId, new DateOnly(2026, 1, 6));
            await CreateCasoOnDateAsync(db, service, tecId, tecPersona.Id, persona.Id, tipoCasoId, new DateOnly(2026, 1, 19));

            var stats = await service.GetStatsAsync(tecId, tecPersona.Id, AuthBootstrapper.TecnicoSistemasRoleName, null, CasoTIGranularidad.Semana, null, null);

            Assert.Equal(2, stats.BarChart.Labels.Count);
            Assert.Equal(2, stats.BarChart.Values[0]);
            Assert.Equal(1, stats.BarChart.Values[1]);
        }

        [Fact]
        public async Task GetStatsAsync_Mes_DoesNotMergeAcrossYearBoundary()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tecId, tecPersona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            await CreateCasoOnDateAsync(db, service, tecId, tecPersona.Id, persona.Id, tipoCasoId, new DateOnly(2025, 12, 15));
            await CreateCasoOnDateAsync(db, service, tecId, tecPersona.Id, persona.Id, tipoCasoId, new DateOnly(2026, 1, 15));

            var stats = await service.GetStatsAsync(tecId, tecPersona.Id, AuthBootstrapper.TecnicoSistemasRoleName, null, CasoTIGranularidad.Mes, null, null);

            Assert.Equal(2, stats.BarChart.Labels.Count);
            Assert.Equal("dic 2025", stats.BarChart.Labels[0]);
            Assert.Equal("ene 2026", stats.BarChart.Labels[1]);
        }

        [Fact]
        public async Task GetStatsAsync_PieChart_CountsPerTipoCasoAndRespectsMonthFilter()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tecId, tecPersona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoRed = SeedTipoCaso(db, "Problema de red");
            var tipoImpresora = SeedTipoCaso(db, "Soporte de impresora");
            var service = CreateService(db);

            await CreateCasoOnDateAsync(db, service, tecId, tecPersona.Id, persona.Id, tipoRed, new DateOnly(2026, 1, 10));
            await CreateCasoOnDateAsync(db, service, tecId, tecPersona.Id, persona.Id, tipoImpresora, new DateOnly(2026, 2, 10));

            var statsEnero = await service.GetStatsAsync(tecId, tecPersona.Id, AuthBootstrapper.TecnicoSistemasRoleName, null, CasoTIGranularidad.Mes, 1, 2026);
            Assert.Single(statsEnero.PieChart.Labels);
            Assert.Equal("Problema de red", statsEnero.PieChart.Labels[0]);

            var statsTodos = await service.GetStatsAsync(tecId, tecPersona.Id, AuthBootstrapper.TecnicoSistemasRoleName, null, CasoTIGranularidad.Mes, null, null);
            Assert.Equal(2, statsTodos.PieChart.Labels.Count);
        }

        [Fact]
        public async Task GetStatsAsync_RoleScoping_TecnicoSeesAll_AdminFilters_AuditorOnlyResuelto()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, tec1Persona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var (tec2Id, tec2Persona) = SeedTecnico(db, "tec2", AuthBootstrapper.TecnicoSistemasRoleName);
            var (adminId, adminPersona) = SeedTecnico(db, "admin1", AuthBootstrapper.AdministradorRoleName);
            var (auditorId, auditorPersona) = SeedTecnico(db, "auditor1", AuthBootstrapper.AuditorRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            var abiertoId = await CreateCasoOnDateAsync(db, service, tec1Id, tec1Persona.Id, persona.Id, tipoCasoId, new DateOnly(2026, 1, 5));
            var resueltoId = await CreateCasoOnDateAsync(db, service, tec1Id, tec1Persona.Id, persona.Id, tipoCasoId, new DateOnly(2026, 1, 6));
            var resuelto = await db.CasosTI.SingleAsync(c => c.Id == resueltoId);
            resuelto.Estado = CasoTIEstado.Resuelto;
            resuelto.TecnicoUserSystemId = tec2Id; // simula que tec2 fue quien lo resolvió
            await db.SaveChangesAsync();

            // Técnico ahora ve TODOS los casos (ya no solo los propios).
            var tec1Stats = await service.GetStatsAsync(tec1Id, tec1Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName, null, CasoTIGranularidad.DiaSemana, null, null);
            Assert.Equal(2, tec1Stats.BarChart.Values.Sum());

            var adminStatsFiltered = await service.GetStatsAsync(adminId, adminPersona.Id, AuthBootstrapper.AdministradorRoleName, tec2Id, CasoTIGranularidad.DiaSemana, null, null);
            Assert.Equal(1, adminStatsFiltered.BarChart.Values.Sum());

            var adminStatsAll = await service.GetStatsAsync(adminId, adminPersona.Id, AuthBootstrapper.AdministradorRoleName, null, CasoTIGranularidad.DiaSemana, null, null);
            Assert.Equal(2, adminStatsAll.BarChart.Values.Sum());

            var auditorStats = await service.GetStatsAsync(auditorId, auditorPersona.Id, AuthBootstrapper.AuditorRoleName, null, CasoTIGranularidad.DiaSemana, null, null);
            Assert.Equal(1, auditorStats.BarChart.Values.Sum());
        }

        private static async Task ResolveAndRateAsync(MyDbContext db, CasoTIService service, int casoId, int tecnicoId, int rating, string? comment)
        {
            await service.ResolveAsync(casoId, "Resuelto", tecnicoId, AuthBootstrapper.TecnicoSistemasRoleName, token => $"https://x/{token}");
            var token = (await db.CasosTI.SingleAsync(c => c.Id == casoId)).RatingToken!.Value;
            await service.SubmitRatingAsync(token, rating, comment);
        }

        [Fact]
        public async Task GetRatingsAsync_ExcludesUnratedResolvedCasesFromAverage()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tecId, tecPersona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            var ratedId = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tecId, tecPersona.Id, AuthBootstrapper.TecnicoSistemasRoleName);
            await ResolveAndRateAsync(db, service, ratedId, tecId, 4, "Bien");

            var unratedId = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tecId, tecPersona.Id, AuthBootstrapper.TecnicoSistemasRoleName);
            await service.ResolveAsync(unratedId, "Resuelto sin calificar", tecId, AuthBootstrapper.TecnicoSistemasRoleName, token => $"https://x/{token}");

            var result = await service.GetRatingsAsync(tecId, tecPersona.Id, AuthBootstrapper.TecnicoSistemasRoleName, null);

            var resumen = result.Resumen.Single();
            Assert.Equal(4, resumen.Promedio);
            Assert.Equal(1, resumen.CantidadCalificados);
        }

        [Fact]
        public async Task GetRatingsAsync_ComputesAverageAcrossMultipleRatings()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tecId, tecPersona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            var caso1 = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tecId, tecPersona.Id, AuthBootstrapper.TecnicoSistemasRoleName);
            await ResolveAndRateAsync(db, service, caso1, tecId, 4, null);
            var caso2 = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tecId, tecPersona.Id, AuthBootstrapper.TecnicoSistemasRoleName);
            await ResolveAndRateAsync(db, service, caso2, tecId, 2, null);

            var result = await service.GetRatingsAsync(tecId, tecPersona.Id, AuthBootstrapper.TecnicoSistemasRoleName, null);

            Assert.Equal(3, result.Resumen.Single().Promedio);
        }

        [Fact]
        public async Task GetRatingsAsync_Tecnico_SeesAllTecnicosRatings()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, tec1Persona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var (tec2Id, tec2Persona) = SeedTecnico(db, "tec2", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            var caso1 = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tec1Id, tec1Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName);
            await ResolveAndRateAsync(db, service, caso1, tec1Id, 5, null);
            var caso2 = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tec2Id, tec2Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName);
            await ResolveAndRateAsync(db, service, caso2, tec2Id, 1, null);

            var result = await service.GetRatingsAsync(tec1Id, tec1Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName, null);

            Assert.Equal(2, result.Resumen.Count);
        }

        [Fact]
        public async Task GetRatingsAsync_Admin_AllOrFilteredByTecnico()
        {
            using var db = TestHelpers.CreateDbContext();
            var (adminId, adminPersona) = SeedTecnico(db, "admin1", AuthBootstrapper.AdministradorRoleName);
            var (tec1Id, tec1Persona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var (tec2Id, tec2Persona) = SeedTecnico(db, "tec2", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            var caso1 = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tec1Id, tec1Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName);
            await ResolveAndRateAsync(db, service, caso1, tec1Id, 5, null);
            var caso2 = await service.CreateAsync(NewVm(persona.Id, tipoCasoId), tec2Id, tec2Persona.Id, AuthBootstrapper.TecnicoSistemasRoleName);
            await ResolveAndRateAsync(db, service, caso2, tec2Id, 1, null);

            var all = await service.GetRatingsAsync(adminId, adminPersona.Id, AuthBootstrapper.AdministradorRoleName, null);
            Assert.Equal(2, all.Resumen.Count);

            var filtered = await service.GetRatingsAsync(adminId, adminPersona.Id, AuthBootstrapper.AdministradorRoleName, tec1Id);
            Assert.Single(filtered.Resumen);
            Assert.Equal(5, filtered.Resumen.Single().Promedio);
        }

        [Fact]
        public async Task GetRatingsAsync_Auditor_NeverSeesAbiertoCaseEvenIfDirectlySeededWithRating()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, tec1Persona) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var (auditorId, auditorPersona) = SeedTecnico(db, "auditor1", AuthBootstrapper.AuditorRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            // Sembrado directo en la BD: un caso Abierto con RatingValue, algo que el flujo
            // normal nunca produce (el token solo se genera al resolver), para probar que el
            // guard Estado==Resuelto es una defensa real y no una coincidencia del flujo normal.
            db.CasosTI.Add(new CasoTI
            {
                Fecha = DateOnly.FromDateTime(DateTime.Now),
                TecnicoUserSystemId = tec1Id,
                PersonaUserId = persona.Id,
                TipoCasoId = tipoCasoId,
                Estado = CasoTIEstado.Abierto,
                RatingValue = 5,
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var result = await service.GetRatingsAsync(auditorId, auditorPersona.Id, AuthBootstrapper.AuditorRoleName, null);

            Assert.Empty(result.Resumen);
            Assert.Empty(result.Comentarios);
        }
    }
}
