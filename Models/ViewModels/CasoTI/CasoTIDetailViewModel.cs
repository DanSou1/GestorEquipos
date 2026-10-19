namespace GestorEquipos.Models.ViewModels.CasoTI
{
    public class CasoTIDetailViewModel
    {
        public int Id { get; set; }
        public DateOnly Fecha { get; set; }
        public string Asunto { get; set; } = string.Empty;
        public int? TecnicoUserSystemId { get; set; }
        public string TecnicoNombre { get; set; } = string.Empty;
        public string PersonaNombre { get; set; } = string.Empty;
        public string? AreaSnapshot { get; set; }
        public string TipoCasoNombre { get; set; } = string.Empty;
        public string? Notas { get; set; }
        public CasoTIEstado Estado { get; set; }
        public DateTime? ResolvedAtUtc { get; set; }

        // Calculado por el servicio según el rol de quien consulta — cualquier Admin/Técnico
        // puede resolver cualquier caso Abierto (ver CasoTIService.GetDetailAsync).
        public bool CanResolve { get; set; }

        // Solo el Colaborador dueño del caso, y solo mientras esté Abierto.
        public bool CanDelete { get; set; }
    }
}
