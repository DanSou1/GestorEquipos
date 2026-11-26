namespace GestorEquipos.Models.ViewModels.CasoTI
{
    public class CasoTITecnicoRatingSummary
    {
        public int TecnicoUserSystemId { get; set; }
        public string TecnicoNombre { get; set; } = string.Empty;
        public double? Promedio { get; set; }
        public int CantidadCalificados { get; set; }
        public double? TiempoPromedioResolucionMinutos { get; set; }
        public string TiempoPromedioResolucionLabel { get; set; } = "-";
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

    public class CasoTIChartSeries
    {
        public string Label { get; set; } = string.Empty;
        public List<int> Values { get; set; } = new();
        public string Color { get; set; } = string.Empty;
    }

    public class CasoTIStackedChartViewModel
    {
        public List<string> Labels { get; set; } = new(); // tipos de caso
        public List<CasoTIChartSeries> Series { get; set; } = new(); // una por técnico
    }

    public class CasoTIRatingsViewModel
    {
        public List<CasoTITecnicoRatingSummary> Resumen { get; set; } = new();
        public List<CasoTIRatingDetailRow> Comentarios { get; set; } = new();

        // Casos resueltos por técnico, desglosados por tipo de caso (barras apiladas).
        public CasoTIStackedChartViewModel ResueltosPorTipoYTecnico { get; set; } = new();

        // Tiempo promedio de resolución por técnico (barras simples).
        public CasoTIChartDataViewModel TiempoPromedioPorTecnicoChart { get; set; } = new();
        public List<string> TiempoPromedioPorTecnicoLabels { get; set; } = new();
    }
}
