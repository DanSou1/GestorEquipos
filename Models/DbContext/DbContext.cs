using Microsoft.EntityFrameworkCore;
using Gestor_Equipos.Models; // ← ajusta según donde estén Assignment, Computer, User

namespace Gestor_Equipos.Data;   // ← namespace más común y limpio

public class MyDbContext : DbContext
{
    public MyDbContext(DbContextOptions<MyDbContext> options)
        : base(options)
    {
    }
    
}