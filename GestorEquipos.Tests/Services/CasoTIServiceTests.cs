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

        [Fact]
        public async Task CreateAsync_ByTecnico_ForcesTecnicoToActingUser()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tecnicoId, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var (otroTecnicoId, _) = SeedTecnico(db, "tec2", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            var vm = new CasoTICreateViewModel
            {
                Fecha = DateOnly.FromDateTime(DateTime.Now),
                TecnicoUserSystemId = otroTecnicoId,
                PersonaUserId = persona.Id,
                TipoCasoId = tipoCasoId
            };

            var id = await service.CreateAsync(vm, tecnicoId, AuthBootstrapper.TecnicoSistemasRoleName);

            var caso = await db.CasosTI.SingleAsync(c => c.Id == id);
            Assert.Equal(tecnicoId, caso.TecnicoUserSystemId);
        }

        [Fact]
        public async Task CreateAsync_ByAdmin_HonorsSubmittedTecnico()
        {
            using var db = TestHelpers.CreateDbContext();
            var (adminId, _) = SeedTecnico(db, "admin1", AuthBootstrapper.AdministradorRoleName);
            var (tecnicoId, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            var vm = new CasoTICreateViewModel
            {
                Fecha = DateOnly.FromDateTime(DateTime.Now),
                TecnicoUserSystemId = tecnicoId,
                PersonaUserId = persona.Id,
                TipoCasoId = tipoCasoId
            };

            var id = await service.CreateAsync(vm, adminId, AuthBootstrapper.AdministradorRoleName);

            var caso = await db.CasosTI.SingleAsync(c => c.Id == id);
            Assert.Equal(tecnicoId, caso.TecnicoUserSystemId);
        }

        [Fact]
        public async Task CreateAsync_SnapshotsAreaAtCreationTime_UnaffectedByLaterChange()
        {
            using var db = TestHelpers.CreateDbContext();
            var areaVentas = new Area { Name = "Ventas" };
            var areaSoporte = new Area { Name = "Soporte" };
            db.Areas.AddRange(areaVentas, areaSoporte);
            db.SaveChanges();

            var (tecnicoId, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1", areaVentas);
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            var id = await service.CreateAsync(new CasoTICreateViewModel
            {
                Fecha = DateOnly.FromDateTime(DateTime.Now),
                PersonaUserId = persona.Id,
                TipoCasoId = tipoCasoId
            }, tecnicoId, AuthBootstrapper.TecnicoSistemasRoleName);

            persona.AreaId = areaSoporte.Id;
            await db.SaveChangesAsync();

            var caso = await db.CasosTI.SingleAsync(c => c.Id == id);
            Assert.Equal("Ventas", caso.AreaSnapshot);
        }

        [Fact]
        public async Task GetIndexAsync_Tecnico_OnlySeesOwnCases()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var (tec2Id, _) = SeedTecnico(db, "tec2", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tec1Id, AuthBootstrapper.TecnicoSistemasRoleName);
            await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tec2Id, AuthBootstrapper.TecnicoSistemasRoleName);

            var result = await service.GetIndexAsync(tec1Id, AuthBootstrapper.TecnicoSistemasRoleName, null);

            Assert.Single(result);
        }

        [Fact]
        public async Task GetIndexAsync_Admin_SeesAllOrFilteredByTecnico()
        {
            using var db = TestHelpers.CreateDbContext();
            var (adminId, _) = SeedTecnico(db, "admin1", AuthBootstrapper.AdministradorRoleName);
            var (tec1Id, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var (tec2Id, _) = SeedTecnico(db, "tec2", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tec1Id, AuthBootstrapper.TecnicoSistemasRoleName);
            await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tec2Id, AuthBootstrapper.TecnicoSistemasRoleName);

            var all = await service.GetIndexAsync(adminId, AuthBootstrapper.AdministradorRoleName, null);
            Assert.Equal(2, all.Count);

            var filtered = await service.GetIndexAsync(adminId, AuthBootstrapper.AdministradorRoleName, tec1Id);
            Assert.Single(filtered);
        }

        [Fact]
        public async Task GetIndexAsync_Auditor_OnlySeesResueltoCases()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var (auditorId, _) = SeedTecnico(db, "auditor1", AuthBootstrapper.AuditorRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            var abiertoId = await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tec1Id, AuthBootstrapper.TecnicoSistemasRoleName);
            var resueltoId = await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tec1Id, AuthBootstrapper.TecnicoSistemasRoleName);
            var resuelto = await db.CasosTI.SingleAsync(c => c.Id == resueltoId);
            resuelto.Estado = CasoTIEstado.Resuelto;
            await db.SaveChangesAsync();

            var result = await service.GetIndexAsync(auditorId, AuthBootstrapper.AuditorRoleName, null);

            Assert.Single(result);
            Assert.DoesNotContain(result, r => r.Id == abiertoId);
        }

        [Fact]
        public async Task GetDetailAsync_Tecnico_ReturnsNullForOtherTecnicoCase()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var (tec2Id, _) = SeedTecnico(db, "tec2", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            var casoId = await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tec1Id, AuthBootstrapper.TecnicoSistemasRoleName);

            var result = await service.GetDetailAsync(casoId, tec2Id, AuthBootstrapper.TecnicoSistemasRoleName);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetDetailAsync_Auditor_ReturnsNullWhileAbierto_NonNullOnceResuelto()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var (auditorId, _) = SeedTecnico(db, "auditor1", AuthBootstrapper.AuditorRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            var casoId = await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tec1Id, AuthBootstrapper.TecnicoSistemasRoleName);

            Assert.Null(await service.GetDetailAsync(casoId, auditorId, AuthBootstrapper.AuditorRoleName));

            var caso = await db.CasosTI.SingleAsync(c => c.Id == casoId);
            caso.Estado = CasoTIEstado.Resuelto;
            await db.SaveChangesAsync();

            Assert.NotNull(await service.GetDetailAsync(casoId, auditorId, AuthBootstrapper.AuditorRoleName));
        }

        [Fact]
        public async Task ResolveAsync_ThrowsWhenTecnicoResolvesOtherTecnicoCase()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var (tec2Id, _) = SeedTecnico(db, "tec2", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var emailService = new RecordingEmailService();
            var service = CreateService(db, emailService);
            var casoId = await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tec1Id, AuthBootstrapper.TecnicoSistemasRoleName);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ResolveAsync(casoId, "Resuelto", tec2Id, AuthBootstrapper.TecnicoSistemasRoleName, token => $"https://x/{token}"));

            Assert.Empty(emailService.Sent);
            var caso = await db.CasosTI.SingleAsync(c => c.Id == casoId);
            Assert.Equal(CasoTIEstado.Abierto, caso.Estado);
        }

        [Fact]
        public async Task ResolveAsync_ThrowsWhenPersonaEmailEmpty()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = new Users { Name = "SinCorreo", LastName = "Apellido", Email = "", EmailTeams = "" };
            db.Users.Add(persona);
            await db.SaveChangesAsync();
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);
            var casoId = await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tec1Id, AuthBootstrapper.TecnicoSistemasRoleName);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ResolveAsync(casoId, "Resuelto", tec1Id, AuthBootstrapper.TecnicoSistemasRoleName, token => $"https://x/{token}"));
        }

        [Fact]
        public async Task ResolveAsync_ThrowsWhenNotasEmpty()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);
            var casoId = await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tec1Id, AuthBootstrapper.TecnicoSistemasRoleName);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ResolveAsync(casoId, "  ", tec1Id, AuthBootstrapper.TecnicoSistemasRoleName, token => $"https://x/{token}"));
        }

        [Fact]
        public async Task ResolveAsync_ThrowsOnDoubleResolve()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var emailService = new RecordingEmailService();
            var service = CreateService(db, emailService);
            var casoId = await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tec1Id, AuthBootstrapper.TecnicoSistemasRoleName);

            await service.ResolveAsync(casoId, "Resuelto", tec1Id, AuthBootstrapper.TecnicoSistemasRoleName, token => $"https://x/{token}");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ResolveAsync(casoId, "Resuelto otra vez", tec1Id, AuthBootstrapper.TecnicoSistemasRoleName, token => $"https://x/{token}"));

            Assert.Single(emailService.Sent);
        }

        [Fact]
        public async Task ResolveAsync_HappyPath_SetsStateTokenAndSendsEmail()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var emailService = new RecordingEmailService();
            var service = CreateService(db, emailService);
            var casoId = await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tec1Id, AuthBootstrapper.TecnicoSistemasRoleName);

            await service.ResolveAsync(casoId, "Se reemplazó el cartucho", tec1Id, AuthBootstrapper.TecnicoSistemasRoleName, token => $"https://x/{token}");

            var caso = await db.CasosTI.SingleAsync(c => c.Id == casoId);
            Assert.Equal(CasoTIEstado.Resuelto, caso.Estado);
            Assert.Equal("Se reemplazó el cartucho", caso.Notas);
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
            var (tec1Id, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var emailService = new RecordingEmailService { ThrowOnSend = true };
            var service = CreateService(db, emailService);
            var casoId = await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tec1Id, AuthBootstrapper.TecnicoSistemasRoleName);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ResolveAsync(casoId, "Resuelto", tec1Id, AuthBootstrapper.TecnicoSistemasRoleName, token => $"https://x/{token}"));

            var caso = await db.CasosTI.SingleAsync(c => c.Id == casoId);
            Assert.Equal(CasoTIEstado.Abierto, caso.Estado);
            Assert.Null(caso.ResolvedAtUtc);
            Assert.Null(caso.RatingToken);
        }

        [Fact]
        public async Task ResolveAsync_Admin_CanResolveAnyTecnicoCase()
        {
            using var db = TestHelpers.CreateDbContext();
            var (adminId, _) = SeedTecnico(db, "admin1", AuthBootstrapper.AdministradorRoleName);
            var (tec1Id, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);
            var casoId = await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tec1Id, AuthBootstrapper.TecnicoSistemasRoleName);

            await service.ResolveAsync(casoId, "Resuelto por admin", adminId, AuthBootstrapper.AdministradorRoleName, token => $"https://x/{token}");

            var caso = await db.CasosTI.SingleAsync(c => c.Id == casoId);
            Assert.Equal(CasoTIEstado.Resuelto, caso.Estado);
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
            var (tec1Id, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);
            var casoId = await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tec1Id, AuthBootstrapper.TecnicoSistemasRoleName);
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
            var (tec1Id, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);
            var casoId = await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tec1Id, AuthBootstrapper.TecnicoSistemasRoleName);
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
            var (tec1Id, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);
            var casoId = await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tec1Id, AuthBootstrapper.TecnicoSistemasRoleName);
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

        private async Task<int> CreateCasoOnDateAsync(Gestor_Equipos.Data.MyDbContext db, CasoTIService service, int tecnicoId, int personaId, int tipoCasoId, DateOnly fecha)
        {
            var id = await service.CreateAsync(new CasoTICreateViewModel { Fecha = fecha, PersonaUserId = personaId, TipoCasoId = tipoCasoId }, tecnicoId, AuthBootstrapper.TecnicoSistemasRoleName);
            return id;
        }

        [Fact]
        public async Task GetStatsAsync_DiaSemana_BucketsByCalendarWeekday()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tecId, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            // 2026-01-05 es lunes, 2026-01-06 es martes.
            await CreateCasoOnDateAsync(db, service, tecId, persona.Id, tipoCasoId, new DateOnly(2026, 1, 5));
            await CreateCasoOnDateAsync(db, service, tecId, persona.Id, tipoCasoId, new DateOnly(2026, 1, 5));
            await CreateCasoOnDateAsync(db, service, tecId, persona.Id, tipoCasoId, new DateOnly(2026, 1, 6));

            var stats = await service.GetStatsAsync(tecId, AuthBootstrapper.TecnicoSistemasRoleName, null, CasoTIGranularidad.DiaSemana, null, null);

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
            var (tecId, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            // 2026-01-05 (lunes) y 2026-01-06 (martes) caen en la misma semana ISO;
            // 2026-01-19 cae en una semana distinta.
            await CreateCasoOnDateAsync(db, service, tecId, persona.Id, tipoCasoId, new DateOnly(2026, 1, 5));
            await CreateCasoOnDateAsync(db, service, tecId, persona.Id, tipoCasoId, new DateOnly(2026, 1, 6));
            await CreateCasoOnDateAsync(db, service, tecId, persona.Id, tipoCasoId, new DateOnly(2026, 1, 19));

            var stats = await service.GetStatsAsync(tecId, AuthBootstrapper.TecnicoSistemasRoleName, null, CasoTIGranularidad.Semana, null, null);

            Assert.Equal(2, stats.BarChart.Labels.Count);
            Assert.Equal(2, stats.BarChart.Values[0]);
            Assert.Equal(1, stats.BarChart.Values[1]);
        }

        [Fact]
        public async Task GetStatsAsync_Mes_DoesNotMergeAcrossYearBoundary()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tecId, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            await CreateCasoOnDateAsync(db, service, tecId, persona.Id, tipoCasoId, new DateOnly(2025, 12, 15));
            await CreateCasoOnDateAsync(db, service, tecId, persona.Id, tipoCasoId, new DateOnly(2026, 1, 15));

            var stats = await service.GetStatsAsync(tecId, AuthBootstrapper.TecnicoSistemasRoleName, null, CasoTIGranularidad.Mes, null, null);

            Assert.Equal(2, stats.BarChart.Labels.Count);
            Assert.Equal("dic 2025", stats.BarChart.Labels[0]);
            Assert.Equal("ene 2026", stats.BarChart.Labels[1]);
        }

        [Fact]
        public async Task GetStatsAsync_PieChart_CountsPerTipoCasoAndRespectsMonthFilter()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tecId, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoRed = SeedTipoCaso(db, "Problema de red");
            var tipoImpresora = SeedTipoCaso(db, "Soporte de impresora");
            var service = CreateService(db);

            await CreateCasoOnDateAsync(db, service, tecId, persona.Id, tipoRed, new DateOnly(2026, 1, 10));
            await CreateCasoOnDateAsync(db, service, tecId, persona.Id, tipoImpresora, new DateOnly(2026, 2, 10));

            var statsEnero = await service.GetStatsAsync(tecId, AuthBootstrapper.TecnicoSistemasRoleName, null, CasoTIGranularidad.Mes, 1, 2026);
            Assert.Single(statsEnero.PieChart.Labels);
            Assert.Equal("Problema de red", statsEnero.PieChart.Labels[0]);

            var statsTodos = await service.GetStatsAsync(tecId, AuthBootstrapper.TecnicoSistemasRoleName, null, CasoTIGranularidad.Mes, null, null);
            Assert.Equal(2, statsTodos.PieChart.Labels.Count);
        }

        [Fact]
        public async Task GetStatsAsync_RoleScoping_IsEnforced()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var (tec2Id, _) = SeedTecnico(db, "tec2", AuthBootstrapper.TecnicoSistemasRoleName);
            var (adminId, _) = SeedTecnico(db, "admin1", AuthBootstrapper.AdministradorRoleName);
            var (auditorId, _) = SeedTecnico(db, "auditor1", AuthBootstrapper.AuditorRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            var abiertoId = await CreateCasoOnDateAsync(db, service, tec1Id, persona.Id, tipoCasoId, new DateOnly(2026, 1, 5));
            var resueltoId = await CreateCasoOnDateAsync(db, service, tec2Id, persona.Id, tipoCasoId, new DateOnly(2026, 1, 6));
            var resuelto = await db.CasosTI.SingleAsync(c => c.Id == resueltoId);
            resuelto.Estado = CasoTIEstado.Resuelto;
            await db.SaveChangesAsync();

            var tec1Stats = await service.GetStatsAsync(tec1Id, AuthBootstrapper.TecnicoSistemasRoleName, null, CasoTIGranularidad.DiaSemana, null, null);
            Assert.Equal(1, tec1Stats.BarChart.Values.Sum());

            var adminStatsFiltered = await service.GetStatsAsync(adminId, AuthBootstrapper.AdministradorRoleName, tec2Id, CasoTIGranularidad.DiaSemana, null, null);
            Assert.Equal(1, adminStatsFiltered.BarChart.Values.Sum());

            var adminStatsAll = await service.GetStatsAsync(adminId, AuthBootstrapper.AdministradorRoleName, null, CasoTIGranularidad.DiaSemana, null, null);
            Assert.Equal(2, adminStatsAll.BarChart.Values.Sum());

            var auditorStats = await service.GetStatsAsync(auditorId, AuthBootstrapper.AuditorRoleName, null, CasoTIGranularidad.DiaSemana, null, null);
            Assert.Equal(1, auditorStats.BarChart.Values.Sum());
        }

        private static async Task ResolveAndRateAsync(Gestor_Equipos.Data.MyDbContext db, CasoTIService service, int casoId, int tecnicoId, int rating, string? comment)
        {
            await service.ResolveAsync(casoId, "Resuelto", tecnicoId, AuthBootstrapper.TecnicoSistemasRoleName, token => $"https://x/{token}");
            var token = (await db.CasosTI.SingleAsync(c => c.Id == casoId)).RatingToken!.Value;
            await service.SubmitRatingAsync(token, rating, comment);
        }

        [Fact]
        public async Task GetRatingsAsync_ExcludesUnratedResolvedCasesFromAverage()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tecId, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            var ratedId = await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tecId, AuthBootstrapper.TecnicoSistemasRoleName);
            await ResolveAndRateAsync(db, service, ratedId, tecId, 4, "Bien");

            var unratedId = await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tecId, AuthBootstrapper.TecnicoSistemasRoleName);
            await service.ResolveAsync(unratedId, "Resuelto sin calificar", tecId, AuthBootstrapper.TecnicoSistemasRoleName, token => $"https://x/{token}");

            var result = await service.GetRatingsAsync(tecId, AuthBootstrapper.TecnicoSistemasRoleName, null);

            var resumen = result.Resumen.Single();
            Assert.Equal(4, resumen.Promedio);
            Assert.Equal(1, resumen.CantidadCalificados);
        }

        [Fact]
        public async Task GetRatingsAsync_ComputesAverageAcrossMultipleRatings()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tecId, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            var caso1 = await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tecId, AuthBootstrapper.TecnicoSistemasRoleName);
            await ResolveAndRateAsync(db, service, caso1, tecId, 4, null);
            var caso2 = await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tecId, AuthBootstrapper.TecnicoSistemasRoleName);
            await ResolveAndRateAsync(db, service, caso2, tecId, 2, null);

            var result = await service.GetRatingsAsync(tecId, AuthBootstrapper.TecnicoSistemasRoleName, null);

            Assert.Equal(3, result.Resumen.Single().Promedio);
        }

        [Fact]
        public async Task GetRatingsAsync_Tecnico_NeverSeesOtherTecnicoRatings()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var (tec2Id, _) = SeedTecnico(db, "tec2", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            var caso1 = await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tec1Id, AuthBootstrapper.TecnicoSistemasRoleName);
            await ResolveAndRateAsync(db, service, caso1, tec1Id, 5, null);
            var caso2 = await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tec2Id, AuthBootstrapper.TecnicoSistemasRoleName);
            await ResolveAndRateAsync(db, service, caso2, tec2Id, 1, null);

            var result = await service.GetRatingsAsync(tec1Id, AuthBootstrapper.TecnicoSistemasRoleName, null);

            Assert.Single(result.Resumen);
            Assert.Equal(5, result.Resumen.Single().Promedio);
        }

        [Fact]
        public async Task GetRatingsAsync_Admin_AllOrFilteredByTecnico()
        {
            using var db = TestHelpers.CreateDbContext();
            var (adminId, _) = SeedTecnico(db, "admin1", AuthBootstrapper.AdministradorRoleName);
            var (tec1Id, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var (tec2Id, _) = SeedTecnico(db, "tec2", AuthBootstrapper.TecnicoSistemasRoleName);
            var persona = SeedPersonaAtendida(db, "Persona1");
            var tipoCasoId = SeedTipoCaso(db);
            var service = CreateService(db);

            var caso1 = await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tec1Id, AuthBootstrapper.TecnicoSistemasRoleName);
            await ResolveAndRateAsync(db, service, caso1, tec1Id, 5, null);
            var caso2 = await service.CreateAsync(new CasoTICreateViewModel { Fecha = DateOnly.FromDateTime(DateTime.Now), PersonaUserId = persona.Id, TipoCasoId = tipoCasoId }, tec2Id, AuthBootstrapper.TecnicoSistemasRoleName);
            await ResolveAndRateAsync(db, service, caso2, tec2Id, 1, null);

            var all = await service.GetRatingsAsync(adminId, AuthBootstrapper.AdministradorRoleName, null);
            Assert.Equal(2, all.Resumen.Count);

            var filtered = await service.GetRatingsAsync(adminId, AuthBootstrapper.AdministradorRoleName, tec1Id);
            Assert.Single(filtered.Resumen);
            Assert.Equal(5, filtered.Resumen.Single().Promedio);
        }

        [Fact]
        public async Task GetRatingsAsync_Auditor_NeverSeesAbiertoCaseEvenIfDirectlySeededWithRating()
        {
            using var db = TestHelpers.CreateDbContext();
            var (tec1Id, _) = SeedTecnico(db, "tec1", AuthBootstrapper.TecnicoSistemasRoleName);
            var (auditorId, _) = SeedTecnico(db, "auditor1", AuthBootstrapper.AuditorRoleName);
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

            var result = await service.GetRatingsAsync(auditorId, AuthBootstrapper.AuditorRoleName, null);

            Assert.Empty(result.Resumen);
            Assert.Empty(result.Comentarios);
        }
    }
}
