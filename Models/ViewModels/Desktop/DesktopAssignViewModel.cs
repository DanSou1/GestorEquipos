using Microsoft.AspNetCore.Mvc.Rendering;

namespace GestorEquipos.Models.ViewModels.Desktop
{
    public class DesktopAssignViewModel
    {
        public int DesktopId { get; set; }
        public string DesktopLabel { get; set; } = string.Empty;
        public SelectList Users { get; set; } = default!;
        public int? SelectedUserId { get; set; }

        public bool HasActiveHolder { get; set; }
        public string? CurrentHolderName { get; set; }
        public int? CurrentHolderUserId { get; set; }
    }
}
