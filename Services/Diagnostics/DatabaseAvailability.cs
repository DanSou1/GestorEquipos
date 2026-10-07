using Microsoft.Data.SqlClient;

namespace Gestor_Equipos.Services.Diagnostics;

public static class DatabaseAvailability
{
    public static bool IsUnavailable(Exception exception)
    {
        // EF Core wraps the provider exception when its retry strategy is exhausted.
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql && sql.Errors.Cast<SqlError>().Any(error => error.Number is
                -2 or 2 or 20 or 53 or 64 or 121 or 233 or 258 or 701 or 802 or 8645 or 8651 or 17300
                or 10053 or 10054 or 10060 or 11001 or 10928 or 10929 or 40197 or 40501 or 40613))
                return true;
        }
        return false;
    }
}
