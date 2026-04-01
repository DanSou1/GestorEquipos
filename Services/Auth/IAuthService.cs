namespace Gestor_Equipos.Services.Auth
{
    public interface IAuthService
    {
        Task <bool> ValidateUserAsync(string email, string passwordHash);
    }
}
