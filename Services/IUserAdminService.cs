using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.UserAdmin;

namespace Gestor_Equipos.Services
{
    public interface IUserAdminService
    {
        Task<int> CreateUserAsync(UserCreateViewModel vm);
        Task CreateLoginAsync(int userId, string username, string password, int rolId);
        Task<List<Users>> GetAllAsync();
        Task<Users?> GetByIdAsync(int id);
        Task<UserDetailViewModel?> GetDetailAsync(int id);
    }
}
