using Gestor_Equipos.Services.Auth;
using Microsoft.Data.SqlClient;
using System.Data.SqlClient;

namespace Gestor_Equipos.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly string _connectionString;

        public AuthService(IConfiguration _configuration)
        {
            _connectionString = _configuration.GetConnectionString("DefaultConnection");
        }
        public async Task<bool> ValidateUserAsync(string email, string pwd)
        {
            using (SqlConnection connection = new SqlConnection (_connectionString))
            {
                await connection.OpenAsync();
                string query = "SELECT * FROM AssestInventory WHERE email = @email AND passwordHash = @passwordHash";
            }
            return false;
        }
    }
}
