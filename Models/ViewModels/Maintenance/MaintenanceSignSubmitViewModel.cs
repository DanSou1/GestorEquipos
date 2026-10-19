using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models.ViewModels.Maintenance
{
    public class MaintenanceSignSubmitViewModel
    {
        [Required]
        public int CampaignItemId { get; set; }

        [Required(ErrorMessage = "Debes dibujar tu firma o usar tu firma guardada.")]
        public string SignatureData { get; set; } = string.Empty;

        public MaintenanceSignatureMethod Method { get; set; } = MaintenanceSignatureMethod.Manuscrita;

        public bool SaveSignature { get; set; }
    }
}
