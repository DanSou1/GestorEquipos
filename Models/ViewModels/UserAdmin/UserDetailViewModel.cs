namespace GestorEquipos.Models.ViewModels.UserAdmin
{
    public class UserDetailViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string EmailTeams { get; set; } = string.Empty;
        public string AreaName { get; set; } = string.Empty;
        public string RegionalName { get; set; } = string.Empty;
        public string? Username { get; set; }
        public string? RolName { get; set; }
        public bool Activo { get; set; } = true;
        public DateOnly? DeactivatedAt { get; set; }

        public List<AssignedDesktopItem> CurrentDesktops { get; set; } = new();
        public List<UserAsignationHistoryItem> AsignationHistory { get; set; } = new();
    }

    public class AssignedDesktopItem
    {
        public int DesktopId { get; set; }
        public string NameDesktop { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public DateOnly SinceDate { get; set; }
    }

    public class UserAsignationHistoryItem
    {
        public string NameDesktop { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public DateOnly DateAsignation { get; set; }
    }
}
