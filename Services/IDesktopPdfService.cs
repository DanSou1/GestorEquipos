using GestorEquipos.Models.ViewModels.Desktop;

namespace Gestor_Equipos.Services
{
    public interface IDesktopPdfService
    {
        byte[] GenerateHojaDeVidaPdf(DesktopDetailViewModel detail, bool includeRemoteAccess);
    }
}
