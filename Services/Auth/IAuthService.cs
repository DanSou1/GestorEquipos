namespace Gestor_Equipos.Services.Auth
{
    public interface IAuthService
    {
        Task<AuthenticatedUser?> ValidateUserAsync(string email, string password);
    }

    public sealed record AuthenticatedUser(
        int UserId,
        int UserSystemId,
        string Email,
        string FullName,
        string RoleName);
}
