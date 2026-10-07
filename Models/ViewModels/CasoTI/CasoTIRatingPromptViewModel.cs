namespace GestorEquipos.Models.ViewModels.CasoTI
{
    public class CasoTIRatingPromptViewModel
    {
        public Guid Token { get; set; }
        public bool AlreadySubmitted { get; set; }
        public string? Notas { get; set; }
        public int? RatingValue { get; set; }
        public string? RatingComment { get; set; }
    }
}
