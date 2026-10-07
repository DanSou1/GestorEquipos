using Gestor_Equipos.Data;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.CasoTI;
using Microsoft.EntityFrameworkCore;

namespace Gestor_Equipos.Services.Implementations
{
    public class CasoTIService : ICasoTIService
    {
        private readonly MyDbContext _dbContext;
        private readonly IEmailService _emailService;

        public CasoTIService(MyDbContext dbContext, IEmailService emailService)
        {
            _dbContext = dbContext;
            _emailService = emailService;
        }

        public async Task<int> CreateAsync(CasoTICreateViewModel vm, int actingUserSystemId, string actingUserRole, CancellationToken ct = default)
        {
            int tecnicoUserSystemId;

            // Nunca confiar en lo que mande el cliente: si quien crea el caso es Técnico,
            // el técnico del caso es siempre él mismo, sin importar lo que venga en el vm.
            if (actingUserRole == AuthBootstrapper.TecnicoSistemasRoleName)
            {
                tecnicoUserSystemId = actingUserSystemId;
            }
            else
            {
                if (!vm.TecnicoUserSystemId.HasValue)
                {
                    throw new InvalidOperationException("Debes seleccionar un técnico.");
                }

                var tecnicoValido = await _dbContext.UserSystems
                    .Include(us => us.Rol)
                    .AnyAsync(us => us.Id == vm.TecnicoUserSystemId.Value &&
                        (us.Rol.Name == AuthBootstrapper.AdministradorRoleName || us.Rol.Name == AuthBootstrapper.TecnicoSistemasRoleName), ct);

                if (!tecnicoValido)
                {
                    throw new InvalidOperationException("Técnico inválido.");
                }

                tecnicoUserSystemId = vm.TecnicoUserSystemId.Value;
            }

            if (vm.Fecha > DateOnly.FromDateTime(DateTime.Now))
            {
                throw new InvalidOperationException("La fecha no puede ser futura.");
            }

            var persona = await _dbContext.Users
                .Include(u => u.Area)
                .SingleOrDefaultAsync(u => u.Id == vm.PersonaUserId, ct)
                ?? throw new InvalidOperationException("Persona no encontrada.");

            var tipoCasoExiste = await _dbContext.TiposCaso.AnyAsync(t => t.Id == vm.TipoCasoId, ct);
            if (!tipoCasoExiste)
            {
                throw new InvalidOperationException("Tipo de caso inválido.");
            }

            var caso = new CasoTI
            {
                Fecha = vm.Fecha,
                TecnicoUserSystemId = tecnicoUserSystemId,
                PersonaUserId = persona.Id,
                AreaSnapshot = persona.Area?.Name,
                TipoCasoId = vm.TipoCasoId,
                Notas = vm.Notas,
                Estado = CasoTIEstado.Abierto,
                CreatedAtUtc = DateTime.UtcNow
            };

            _dbContext.CasosTI.Add(caso);
            await _dbContext.SaveChangesAsync(ct);
            return caso.Id;
        }

        public async Task<CasoTIDetailViewModel?> GetDetailAsync(int id, int actingUserSystemId, string actingUserRole, CancellationToken ct = default)
        {
            var caso = await _dbContext.CasosTI
                .Include(c => c.Tecnico).ThenInclude(t => t.User)
                .Include(c => c.Persona)
                .Include(c => c.TipoCaso)
                .SingleOrDefaultAsync(c => c.Id == id, ct);

            if (caso is null)
            {
                return null;
            }

            // Un técnico no puede ver el detalle de un caso ajeno, aunque conozca el id.
            if (actingUserRole == AuthBootstrapper.TecnicoSistemasRoleName && caso.TecnicoUserSystemId != actingUserSystemId)
            {
                return null;
            }

            if (actingUserRole == AuthBootstrapper.AuditorRoleName && caso.Estado != CasoTIEstado.Resuelto)
            {
                return null;
            }

            var canResolve = caso.Estado == CasoTIEstado.Abierto &&
                (actingUserRole == AuthBootstrapper.AdministradorRoleName || caso.TecnicoUserSystemId == actingUserSystemId);

            return new CasoTIDetailViewModel
            {
                Id = caso.Id,
                Fecha = caso.Fecha,
                TecnicoUserSystemId = caso.TecnicoUserSystemId,
                TecnicoNombre = $"{caso.Tecnico.User.Name} {caso.Tecnico.User.LastName}",
                PersonaNombre = $"{caso.Persona.Name} {caso.Persona.LastName}",
                AreaSnapshot = caso.AreaSnapshot,
                TipoCasoNombre = caso.TipoCaso.Nombre,
                Notas = caso.Notas,
                Estado = caso.Estado,
                ResolvedAtUtc = caso.ResolvedAtUtc,
                CanResolve = canResolve
            };
        }

        public async Task<List<CasoTIListItemViewModel>> GetIndexAsync(int actingUserSystemId, string actingUserRole, int? tecnicoFilterUserSystemId, CancellationToken ct = default)
        {
            var query = ApplyRoleScope(
                _dbContext.CasosTI
                    .Include(c => c.Tecnico).ThenInclude(t => t.User)
                    .Include(c => c.Persona)
                    .Include(c => c.TipoCaso),
                actingUserSystemId, actingUserRole, tecnicoFilterUserSystemId);

            var casos = await query
                .OrderByDescending(c => c.Fecha)
                .ThenByDescending(c => c.Id)
                .ToListAsync(ct);

            return casos.Select(c => new CasoTIListItemViewModel
            {
                Id = c.Id,
                Fecha = c.Fecha,
                TecnicoNombre = $"{c.Tecnico.User.Name} {c.Tecnico.User.LastName}",
                PersonaNombre = $"{c.Persona.Name} {c.Persona.LastName}",
                AreaSnapshot = c.AreaSnapshot,
                TipoCasoNombre = c.TipoCaso.Nombre,
                Estado = c.Estado
            }).ToList();
        }

        public async Task ResolveAsync(int casoId, string notasFinal, int actingUserSystemId, string actingUserRole, Func<Guid, string> buildRatingUrl, CancellationToken ct = default)
        {
            var caso = await _dbContext.CasosTI
                .Include(c => c.Persona)
                .SingleOrDefaultAsync(c => c.Id == casoId, ct)
                ?? throw new InvalidOperationException("El caso no existe.");

            if (caso.Estado != CasoTIEstado.Abierto)
            {
                throw new InvalidOperationException("Este caso ya fue resuelto.");
            }

            // Un técnico no puede cerrar un caso ajeno, aunque conozca el id.
            if (actingUserRole == AuthBootstrapper.TecnicoSistemasRoleName && caso.TecnicoUserSystemId != actingUserSystemId)
            {
                throw new InvalidOperationException("No puedes resolver un caso de otro técnico.");
            }

            if (string.IsNullOrWhiteSpace(notasFinal))
            {
                throw new InvalidOperationException("Debes ingresar las notas de resolución.");
            }

            if (string.IsNullOrWhiteSpace(caso.Persona.Email))
            {
                throw new InvalidOperationException("La persona atendida no tiene un correo válido registrado.");
            }

            var strategy = _dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

                var token = Guid.NewGuid();
                var personaNombre = $"{caso.Persona.Name} {caso.Persona.LastName}";
                var body = BuildResolutionEmailBody(personaNombre, notasFinal, buildRatingUrl(token));

                // El correo se envía antes de mutar/guardar: si falla, no queda nada para
                // revertir y el caso permanece Abierto sin token (ver CompleteAndSendAsync
                // en MaintenanceCampaignService, mismo criterio).
                await _emailService.SendAsync(
                    caso.Persona.Email,
                    personaNombre,
                    "Tu caso de soporte TI fue resuelto",
                    body,
                    Array.Empty<EmailAttachment>(),
                    ct);

                caso.Notas = notasFinal;
                caso.Estado = CasoTIEstado.Resuelto;
                caso.ResolvedAtUtc = DateTime.UtcNow;
                caso.RatingToken = token;
                await _dbContext.SaveChangesAsync(ct);

                await transaction.CommitAsync(ct);
            });
        }

        public async Task<CasoTIRatingPromptViewModel?> GetRatingPromptAsync(Guid token, CancellationToken ct = default)
        {
            var caso = await _dbContext.CasosTI.SingleOrDefaultAsync(c => c.RatingToken == token, ct);
            if (caso is null)
            {
                return null;
            }

            return new CasoTIRatingPromptViewModel
            {
                Token = token,
                AlreadySubmitted = caso.RatingSubmittedAtUtc is not null,
                Notas = caso.Notas,
                RatingValue = caso.RatingValue,
                RatingComment = caso.RatingComment
            };
        }

        public async Task<CasoTIRatingSubmitResult> SubmitRatingAsync(Guid token, int rating, string? comment, CancellationToken ct = default)
        {
            var caso = await _dbContext.CasosTI.SingleOrDefaultAsync(c => c.RatingToken == token, ct);
            if (caso is null)
            {
                return CasoTIRatingSubmitResult.TokenNotFound;
            }

            if (caso.RatingSubmittedAtUtc is not null)
            {
                return CasoTIRatingSubmitResult.AlreadySubmitted;
            }

            caso.RatingValue = rating;
            caso.RatingComment = comment;
            caso.RatingSubmittedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);

            return CasoTIRatingSubmitResult.Ok;
        }

        public async Task<CasoTIStatsViewModel> GetStatsAsync(
            int actingUserSystemId, string actingUserRole, int? tecnicoFilterUserSystemId,
            CasoTIGranularidad granularidad, int? mes, int? anio, CancellationToken ct = default)
        {
            var query = ApplyRoleScope(_dbContext.CasosTI.Include(c => c.TipoCaso), actingUserSystemId, actingUserRole, tecnicoFilterUserSystemId);

            if (anio.HasValue)
            {
                query = query.Where(c => c.Fecha.Year == anio.Value);
            }

            if (mes.HasValue)
            {
                query = query.Where(c => c.Fecha.Month == mes.Value);
            }

            var casos = await query.Select(c => new { c.Fecha, TipoCasoNombre = c.TipoCaso.Nombre }).ToListAsync(ct);

            var vm = new CasoTIStatsViewModel
            {
                Granularidad = granularidad,
                Mes = mes,
                Anio = anio,
                BarChart = BuildBarChart(casos.Select(c => c.Fecha).ToList(), granularidad),
                PieChart = new CasoTIChartDataViewModel()
            };

            var pieGroups = casos
                .GroupBy(c => c.TipoCasoNombre)
                .Select(g => new { Tipo = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToList();

            vm.PieChart.Labels = pieGroups.Select(g => g.Tipo).ToList();
            vm.PieChart.Values = pieGroups.Select(g => g.Count).ToList();
            vm.PieColors = pieGroups.Select((_, i) => PiePalette[i % PiePalette.Length]).ToList();

            return vm;
        }

        public async Task<CasoTIRatingsViewModel> GetRatingsAsync(int actingUserSystemId, string actingUserRole, int? tecnicoFilterUserSystemId, CancellationToken ct = default)
        {
            // Estado == Resuelto como defensa adicional explícita: en la práctica solo los
            // casos resueltos tienen RatingToken/RatingValue, pero un Auditor nunca debe
            // depender implícitamente de eso para quedar protegido.
            var query = ApplyRoleScope(
                _dbContext.CasosTI.Include(c => c.Tecnico).ThenInclude(t => t.User).Include(c => c.Persona),
                actingUserSystemId, actingUserRole, tecnicoFilterUserSystemId)
                .Where(c => c.Estado == CasoTIEstado.Resuelto);

            var casos = await query.ToListAsync(ct);

            var resumen = casos
                .GroupBy(c => c.TecnicoUserSystemId)
                .Select(g =>
                {
                    var calificados = g.Where(c => c.RatingValue.HasValue).ToList();
                    return new CasoTITecnicoRatingSummary
                    {
                        TecnicoUserSystemId = g.Key,
                        TecnicoNombre = $"{g.First().Tecnico.User.Name} {g.First().Tecnico.User.LastName}",
                        Promedio = calificados.Count > 0 ? calificados.Average(c => c.RatingValue!.Value) : null,
                        CantidadCalificados = calificados.Count
                    };
                })
                .OrderBy(r => r.TecnicoNombre)
                .ToList();

            var comentarios = casos
                .Where(c => c.RatingValue.HasValue)
                .OrderByDescending(c => c.RatingSubmittedAtUtc)
                .Take(50)
                .Select(c => new CasoTIRatingDetailRow
                {
                    CasoId = c.Id,
                    Fecha = c.Fecha,
                    TecnicoNombre = $"{c.Tecnico.User.Name} {c.Tecnico.User.LastName}",
                    PersonaNombre = $"{c.Persona.Name} {c.Persona.LastName}",
                    RatingValue = c.RatingValue!.Value,
                    RatingComment = c.RatingComment
                })
                .ToList();

            return new CasoTIRatingsViewModel { Resumen = resumen, Comentarios = comentarios };
        }

        private static readonly string[] PiePalette =
        {
            "#2c67e5", "#16a34a", "#d97706", "#7c3aed", "#dc2626", "#0891b2", "#be185d", "#65a30d"
        };

        private static readonly string[] DiasSemana = { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" };
        private static readonly string[] MesesAbrev = { "ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic" };

        private static CasoTIChartDataViewModel BuildBarChart(List<DateOnly> fechas, CasoTIGranularidad granularidad)
        {
            switch (granularidad)
            {
                case CasoTIGranularidad.DiaSemana:
                {
                    var counts = new int[7];
                    foreach (var fecha in fechas)
                    {
                        var index = ((int)fecha.DayOfWeek + 6) % 7; // Monday=0 ... Sunday=6
                        counts[index]++;
                    }

                    return new CasoTIChartDataViewModel
                    {
                        Labels = DiasSemana.ToList(),
                        Values = counts.ToList()
                    };
                }

                case CasoTIGranularidad.Semana:
                {
                    var grupos = fechas
                        .Select(f => new { f, Semana = System.Globalization.ISOWeek.GetWeekOfYear(f.ToDateTime(TimeOnly.MinValue)), Anio = System.Globalization.ISOWeek.GetYear(f.ToDateTime(TimeOnly.MinValue)) })
                        .GroupBy(x => (x.Anio, x.Semana))
                        .OrderBy(g => g.Key.Anio).ThenBy(g => g.Key.Semana)
                        .Select(g => new { Label = $"Sem {g.Key.Semana} ({g.Key.Anio})", Count = g.Count() })
                        .ToList();

                    return new CasoTIChartDataViewModel
                    {
                        Labels = grupos.Select(g => g.Label).ToList(),
                        Values = grupos.Select(g => g.Count).ToList()
                    };
                }

                case CasoTIGranularidad.Mes:
                {
                    var grupos = fechas
                        .GroupBy(f => (f.Year, f.Month))
                        .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                        .Select(g => new { Label = $"{MesesAbrev[g.Key.Month - 1]} {g.Key.Year}", Count = g.Count() })
                        .ToList();

                    return new CasoTIChartDataViewModel
                    {
                        Labels = grupos.Select(g => g.Label).ToList(),
                        Values = grupos.Select(g => g.Count).ToList()
                    };
                }

                default: // FechaEspecifica
                {
                    var grupos = fechas
                        .GroupBy(f => f)
                        .OrderBy(g => g.Key)
                        .Select(g => new { Label = g.Key.ToString("dd/MM/yyyy"), Count = g.Count() })
                        .ToList();

                    return new CasoTIChartDataViewModel
                    {
                        Labels = grupos.Select(g => g.Label).ToList(),
                        Values = grupos.Select(g => g.Count).ToList()
                    };
                }
            }
        }

        private static IQueryable<CasoTI> ApplyRoleScope(IQueryable<CasoTI> query, int actingUserSystemId, string actingUserRole, int? tecnicoFilterUserSystemId)
        {
            if (actingUserRole == AuthBootstrapper.TecnicoSistemasRoleName)
            {
                return query.Where(c => c.TecnicoUserSystemId == actingUserSystemId);
            }

            if (actingUserRole == AuthBootstrapper.AuditorRoleName)
            {
                return query.Where(c => c.Estado == CasoTIEstado.Resuelto);
            }

            if (tecnicoFilterUserSystemId.HasValue)
            {
                return query.Where(c => c.TecnicoUserSystemId == tecnicoFilterUserSystemId.Value);
            }

            return query;
        }

        private static string BuildResolutionEmailBody(string personaNombre, string notas, string ratingUrl)
        {
            return $"""
                <p>Hola {personaNombre},</p>
                <p>Tu caso de soporte TI fue resuelto. Estas son las notas de resolución:</p>
                <p>{notas}</p>
                <p>Nos gustaría conocer tu opinión sobre el soporte recibido:
                <a href="{ratingUrl}">califica tu experiencia aquí</a>.</p>
                <p>Gracias.</p>
                """;
        }
    }
}
