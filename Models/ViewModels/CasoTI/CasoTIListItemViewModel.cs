namespace GestorEquipos.Models.ViewModels.CasoTI
{
    public class CasoTIListItemViewModel
    {
        public int Id { get; set; }
        public DateOnly Fecha { get; set; }
        public string TecnicoNombre { get; set; } = string.Empty;
        public string PersonaNombre { get; set; } = string.Empty;
        public string? AreaSnapshot { get; set; }
        public string TipoCasoNombre { get; set; } = string.Empty;
        public CasoTIEstado Estado { get; set; }
    }
}
