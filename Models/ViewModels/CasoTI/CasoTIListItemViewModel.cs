namespace GestorEquipos.Models.ViewModels.CasoTI
{
    public class CasoTIListItemViewModel
    {
        public int Id { get; set; }
        public DateOnly Fecha { get; set; }
        public string Asunto { get; set; } = string.Empty;
        public string TecnicoNombre { get; set; } = string.Empty;
        public string PersonaNombre { get; set; } = string.Empty;
        public string? AreaSnapshot { get; set; }
        public string TipoCasoNombre { get; set; } = string.Empty;
        public CasoTIEstado Estado { get; set; }

        // Calculado por el servicio (no en la vista): solo el Colaborador dueño del caso puede
        // eliminarlo, y solo mientras esté Abierto — ver ICasoTIService.DeleteAsync.
        public bool CanDelete { get; set; }
    }
}
