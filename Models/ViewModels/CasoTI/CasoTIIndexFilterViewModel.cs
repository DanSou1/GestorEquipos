namespace GestorEquipos.Models.ViewModels.CasoTI
{
    // Filtros de la lista de Casos TI, aplicados siempre DESPUÉS del scoping por rol
    // (ver CasoTIService.ApplyRoleScope) — el scoping nunca se relaja por un filtro.
    public class CasoTIIndexFilterViewModel
    {
        public string? Asunto { get; set; }
        public string? PersonaNombre { get; set; }
        public DateOnly? FechaDesde { get; set; }
        public DateOnly? FechaHasta { get; set; }
        public CasoTIEstado? Estado { get; set; }
        public int? TipoCasoId { get; set; }
        public int? TecnicoUserSystemId { get; set; }
    }
}
