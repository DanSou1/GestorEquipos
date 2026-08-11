using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.AccessAccount;
using GestorEquipos.Models.ViewModels.UserAdmin;

namespace Gestor_Equipos.Services
{
    public interface IUserAdminService
    {
        Task<int> CreateUserAsync(UserCreateViewModel vm);
        Task UpdateUserAsync(int id, UserCreateViewModel vm);
        Task CreateLoginAsync(int userId, string username, string password, int rolId);
        Task UpdateAccountAsync(int userId, AccessAccountEditViewModel vm, int actingAdminUserSystemId);
        Task<List<Users>> GetAllAsync(bool includeInactive = false);
        Task<List<Users>> GetAccountsAsync();
        Task<Users?> GetByIdAsync(int id);
        Task<UserDetailViewModel?> GetDetailAsync(int id);
        Task DeactivateAsync(int id);
        Task<List<UsersByRegionalItem>> GetUsersByRegionalAsync();
    }
}
