namespace GestorEquipos.Models.ViewModels.CasoTI
{
    public class CasoTIChartDataViewModel
    {
        public List<string> Labels { get; set; } = new();
        public List<int> Values { get; set; } = new();
    }

    public class CasoTIStatsViewModel
    {
        public CasoTIGranularidad Granularidad { get; set; }
        public int? Mes { get; set; }
        public int? Anio { get; set; }
        public CasoTIChartDataViewModel BarChart { get; set; } = new();
        public CasoTIChartDataViewModel PieChart { get; set; } = new();
        public List<string> PieColors { get; set; } = new();

        // Paralela a PieChart.Labels: tiempo promedio de resolución formateado para ese tipo de
        // caso ("-" si ninguno de ese tipo ha sido resuelto todavía).
        public List<string> PieAvgResolutionLabels { get; set; } = new();
    }
}
