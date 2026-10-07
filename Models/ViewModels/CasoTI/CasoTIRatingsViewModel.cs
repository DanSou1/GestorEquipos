namespace GestorEquipos.Models.ViewModels.CasoTI
{
    public class CasoTITecnicoRatingSummary
    {
        public int TecnicoUserSystemId { get; set; }
        public string TecnicoNombre { get; set; } = string.Empty;
        public double? Promedio { get; set; }
        public int CantidadCalificados { get; set; }
    }

    public class CasoTIRatingDetailRow
    {
        public int CasoId { get; set; }
        public DateOnly Fecha { get; set; }
        public string TecnicoNombre { get; set; } = string.Empty;
        public string PersonaNombre { get; set; } = string.Empty;
        public int RatingValue { get; set; }
        public string? RatingComment { get; set; }
    }

    public class CasoTIRatingsViewModel
    {
        public List<CasoTITecnicoRatingSummary> Resumen { get; set; } = new();
        public List<CasoTIRatingDetailRow> Comentarios { get; set; } = new();
    }
}
