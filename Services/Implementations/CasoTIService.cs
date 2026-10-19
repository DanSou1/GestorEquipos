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

        public async Task<int> CreateAsync(CasoTICreateViewModel vm, int actingUserSystemId, int actingUsersId, string actingUserRole, CancellationToken ct = default)
        {
            int personaUserId;

            // Nunca confiar en lo que mande el cliente: un Colaborador solo puede registrar
            // casos sobre sí mismo.
            if (actingUserRole == AuthBootstrapper.ColaboradorRoleName)
            {
                personaUserId = actingUsersId;
            }
            else
            {
                if (!vm.PersonaUserId.HasValue)
                {
                    throw new InvalidOperationException("Selecciona la persona que recibió el soporte.");
                }

                personaUserId = vm.PersonaUserId.Value;
            }

            if (vm.Fecha > DateOnly.FromDateTime(DateTime.Now))
            {
                throw new InvalidOperationException("La fecha no puede ser futura.");
            }

            var persona = await _dbContext.Users
                .Include(u => u.Area)
                .SingleOrDefaultAsync(u => u.Id == personaUserId, ct)
                ?? throw new InvalidOperationException("Persona no encontrada.");

            var tipoCasoExiste = await _dbContext.TiposCaso.AnyAsync(t => t.Id == vm.TipoCasoId, ct);
            if (!tipoCasoExiste)
            {
                throw new InvalidOperationException("Tipo de caso inválido.");
            }

            var caso = new CasoTI
            {
                Fecha = vm.Fecha,
                Asunto = vm.Asunto,
                // Todo caso nace sin técnico asignado, sin importar quién lo crea — queda
                // asignado a quien lo resuelva (ver ResolveAsync), para que cualquier
                // técnico/admin pueda entrar y resolverlo.
                TecnicoUserSystemId = null,
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

        public async Task<CasoTIDetailViewModel?> GetDetailAsync(int id, int actingUserSystemId, int actingUsersId, string actingUserRole, CancellationToken ct = default)
        {
            var caso = await _dbContext.CasosTI
                .Include(c => c.Tecnico).ThenInclude(t => t!.User)
                .Include(c => c.Persona)
                .Include(c => c.TipoCaso)
                .SingleOrDefaultAsync(c => c.Id == id, ct);

            if (caso is null)
            {
                return null;
            }

            // Un Colaborador no puede ver el detalle de un caso ajeno, aunque conozca el id.
            if (actingUserRole == AuthBootstrapper.ColaboradorRoleName && caso.PersonaUserId != actingUsersId)
            {
                return null;
            }

            if (actingUserRole == AuthBootstrapper.AuditorRoleName && caso.Estado != CasoTIEstado.Resuelto)
            {
                return null;
            }

            var canResolve = caso.Estado == CasoTIEstado.Abierto &&
                (actingUserRole == AuthBootstrapper.AdministradorRoleName || actingUserRole == AuthBootstrapper.TecnicoSistemasRoleName);

            var canDelete = caso.Estado == CasoTIEstado.Abierto &&
                actingUserRole == AuthBootstrapper.ColaboradorRoleName && caso.PersonaUserId == actingUsersId;

            return new CasoTIDetailViewModel
            {
                Id = caso.Id,
                Fecha = caso.Fecha,
                Asunto = caso.Asunto ?? "-",
                TecnicoUserSystemId = caso.TecnicoUserSystemId,
                TecnicoNombre = caso.Tecnico is null ? "Sin asignar" : $"{caso.Tecnico.User.Name} {caso.Tecnico.User.LastName}",
                PersonaNombre = $"{caso.Persona.Name} {caso.Persona.LastName}",
                AreaSnapshot = caso.AreaSnapshot,
                TipoCasoNombre = caso.TipoCaso.Nombre,
                Notas = caso.Notas,
                Estado = caso.Estado,
                ResolvedAtUtc = caso.ResolvedAtUtc,
                CanResolve = canResolve,
                CanDelete = canDelete
            };
        }

        public async Task<List<CasoTIListItemViewModel>> GetIndexAsync(
            int actingUserSystemId, int actingUsersId, string actingUserRole, CasoTIIndexFilterViewModel filter, CancellationToken ct = default)
        {
            var query = ApplyRoleScope(
                _dbContext.CasosTI
                    .Include(c => c.Tecnico).ThenInclude(t => t!.User)
                    .Include(c => c.Persona)
                    .Include(c => c.TipoCaso),
                actingUserSystemId, actingUsersId, actingUserRole, filter.TecnicoUserSystemId);

            if (filter.FechaDesde.HasValue)
            {
                query = query.Where(c => c.Fecha >= filter.FechaDesde.Value);
            }

            if (filter.FechaHasta.HasValue)
            {
                query = query.Where(c => c.Fecha <= filter.FechaHasta.Value);
            }

            if (filter.Estado.HasValue)
            {
                query = query.Where(c => c.Estado == filter.Estado.Value);
            }

            if (filter.TipoCasoId.HasValue)
            {
                query = query.Where(c => c.TipoCasoId == filter.TipoCasoId.Value);
            }

            var casos = await query
                .OrderByDescending(c => c.Fecha)
                .ThenByDescending(c => c.Id)
                .ToListAsync(ct);

            // Búsqueda de texto insensible a mayúsculas, aplicada en memoria (igual criterio que
            // UserAdminController.Index) — las comparaciones de texto insensibles a mayúsculas no
            // se traducen de forma confiable a nivel de IQueryable en todos los proveedores de EF.
            if (!string.IsNullOrWhiteSpace(filter.Asunto))
            {
                casos = casos.Where(c => c.Asunto != null && c.Asunto.Contains(filter.Asunto, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(filter.PersonaNombre))
            {
                casos = casos.Where(c =>
                    c.Persona.Name.Contains(filter.PersonaNombre, StringComparison.OrdinalIgnoreCase) ||
                    c.Persona.LastName.Contains(filter.PersonaNombre, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return casos.Select(c => new CasoTIListItemViewModel
            {
                Id = c.Id,
                Fecha = c.Fecha,
                Asunto = c.Asunto ?? "-",
                TecnicoNombre = c.Tecnico is null ? "Sin asignar" : $"{c.Tecnico.User.Name} {c.Tecnico.User.LastName}",
                PersonaNombre = $"{c.Persona.Name} {c.Persona.LastName}",
                AreaSnapshot = c.AreaSnapshot,
                TipoCasoNombre = c.TipoCaso.Nombre,
                Estado = c.Estado,
                CanDelete = c.Estado == CasoTIEstado.Abierto
                    && actingUserRole == AuthBootstrapper.ColaboradorRoleName && c.PersonaUserId == actingUsersId
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
                // Cualquier técnico/admin que resuelva un caso (propio o ajeno) queda asignado
                // como su técnico — "auto-asignación al que resuelve", sin dropdown de selección.
                caso.TecnicoUserSystemId = actingUserSystemId;
                await _dbContext.SaveChangesAsync(ct);

                await transaction.CommitAsync(ct);
            });
        }

        public async Task DeleteAsync(int casoId, int actingUsersId, CancellationToken ct = default)
        {
            var caso = await _dbContext.CasosTI.SingleOrDefaultAsync(c => c.Id == casoId, ct)
                ?? throw new InvalidOperationException("El caso no existe.");

            if (caso.PersonaUserId != actingUsersId)
            {
                throw new InvalidOperationException("No puedes eliminar un caso que no es tuyo.");
            }

            if (caso.Estado != CasoTIEstado.Abierto)
            {
                throw new InvalidOperationException("No se puede eliminar un caso ya resuelto.");
            }

            _dbContext.CasosTI.Remove(caso);
            await _dbContext.SaveChangesAsync(ct);
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
            int actingUserSystemId, int actingUsersId, string actingUserRole, int? tecnicoFilterUserSystemId,
            CasoTIGranularidad granularidad, int? mes, int? anio, CancellationToken ct = default)
        {
            var query = ApplyRoleScope(_dbContext.CasosTI.Include(c => c.TipoCaso), actingUserSystemId, actingUsersId, actingUserRole, tecnicoFilterUserSystemId);

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

        public async Task<CasoTIRatingsViewModel> GetRatingsAsync(int actingUserSystemId, int actingUsersId, string actingUserRole, int? tecnicoFilterUserSystemId, CancellationToken ct = default)
        {
            // Estado == Resuelto como defensa adicional explícita: en la práctica solo los
            // casos resueltos tienen RatingToken/RatingValue (y TecnicoUserSystemId asignado),
            // pero un Auditor nunca debe depender implícitamente de eso para quedar protegido.
            var query = ApplyRoleScope(
                _dbContext.CasosTI.Include(c => c.Tecnico).ThenInclude(t => t!.User).Include(c => c.Persona),
                actingUserSystemId, actingUsersId, actingUserRole, tecnicoFilterUserSystemId)
                .Where(c => c.Estado == CasoTIEstado.Resuelto);

            var casos = await query.ToListAsync(ct);

            var resumen = casos
                .Where(c => c.TecnicoUserSystemId.HasValue)
                .GroupBy(c => c.TecnicoUserSystemId!.Value)
                .Select(g =>
                {
                    var calificados = g.Where(c => c.RatingValue.HasValue).ToList();
                    return new CasoTITecnicoRatingSummary
                    {
                        TecnicoUserSystemId = g.Key,
                        TecnicoNombre = $"{g.First().Tecnico!.User.Name} {g.First().Tecnico!.User.LastName}",
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
                    TecnicoNombre = c.Tecnico is null ? "Sin asignar" : $"{c.Tecnico.User.Name} {c.Tecnico.User.LastName}",
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

        private static IQueryable<CasoTI> ApplyRoleScope(IQueryable<CasoTI> query, int actingUserSystemId, int actingUsersId, string actingUserRole, int? tecnicoFilterUserSystemId)
        {
            if (actingUserRole == AuthBootstrapper.ColaboradorRoleName)
            {
                return query.Where(c => c.PersonaUserId == actingUsersId);
            }

            if (actingUserRole == AuthBootstrapper.AuditorRoleName)
            {
                return query.Where(c => c.Estado == CasoTIEstado.Resuelto);
            }

            // Técnico y Administrador comparten alcance: ven todos los casos, con filtro opcional.
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
