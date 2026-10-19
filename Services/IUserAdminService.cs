using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.AccessAccount;
using GestorEquipos.Models.ViewModels.UserAdmin;

namespace Gestor_Equipos.Services
{
    public interface IUserAdminService
    {
        Task<int> CreateUserAsync(UserCreateViewModel vm);
        Task<int> CreateUserWithLoginAsync(UserCreateViewModel vm);
        Task UpdateUserAsync(int id, UserCreateViewModel vm);
        Task CreateLoginAsync(int userId, string username, string password, int rolId);
        Task<int> CreateAccountAsync(UserCreateViewModel personVm, string username, string password, int rolId);
        Task UpdateAccountAsync(int userId, AccessAccountEditViewModel vm, int actingAdminUserSystemId);
        Task DeleteAccountAsync(int userId, int actingAdminUserSystemId, string? adminPassword);
        Task<Guid> ChangeOwnPasswordAsync(int userSystemId, string newPassword);
        Task<List<Users>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
        Task<List<Users>> GetAccountsAsync(CancellationToken cancellationToken = default);
        Task<Users?> GetByIdAsync(int id);
        Task<UserDetailViewModel?> GetDetailAsync(int id);
        Task DeactivateAsync(int id);
        Task<List<UsersByRegionalItem>> GetUsersByRegionalAsync(CancellationToken cancellationToken = default);
    }
}
