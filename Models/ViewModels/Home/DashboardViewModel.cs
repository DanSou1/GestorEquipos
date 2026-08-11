using GestorEquipos.Models.ViewModels.Desktop;
using GestorEquipos.Models.ViewModels.UserAdmin;

namespace GestorEquipos.Models.ViewModels.Home
{
    public class DashboardViewModel
    {
        public EquipmentStatsViewModel EquipmentStats { get; set; } = new();
        public List<UsersByRegionalItem> UsersByRegional { get; set; } = new();
    }
}
