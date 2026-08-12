using Microsoft.EntityFrameworkCore;
using GestorEquipos.Models; 

namespace Gestor_Equipos.Data;

public class MyDbContext : DbContext
{
    public MyDbContext(DbContextOptions<MyDbContext> options) : base(options)
    {
    }

    public DbSet<Desktop> Desktops { get; set; }
    public DbSet<Users> Users { get; set; }
    public DbSet<UserSystem> UserSystems { get; set; }
    public DbSet<Area> Areas { get; set; }
    public DbSet<Regional> Regionals { get; set; }
    public DbSet<Rol> Rols { get; set; }
    public DbSet<OSVersion> OSVersions { get; set; }
    public DbSet<Ram> Rams { get; set; }
    public DbSet<Remote> Remotes { get; set; }
    public DbSet<Maintenance> Maintenances { get; set; }
    public DbSet<MaintenanceType> MaintenanceTypes { get; set; }
    public DbSet<Asignation> Asignations { get; set; }
    public DbSet<PeripheralType> PeripheralTypes { get; set; }
    public DbSet<Peripheral> Peripherals { get; set; }
    public DbSet<PeripheralAssignment> PeripheralAssignments { get; set; }
    public DbSet<PeripheralMaintenance> PeripheralMaintenances { get; set; }
    public DbSet<License> Licenses { get; set; }
    public DbSet<SpecChangeLog> SpecChangeLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure table names
        modelBuilder.Entity<Desktop>().ToTable("Desktop");
        modelBuilder.Entity<Users>().ToTable("Users");
        modelBuilder.Entity<UserSystem>().ToTable("UserSystem");
        modelBuilder.Entity<Area>().ToTable("Area");
        modelBuilder.Entity<Regional>().ToTable("Regional");
        modelBuilder.Entity<Rol>().ToTable("Rol");
        modelBuilder.Entity<OSVersion>().ToTable("OSVersion");
        modelBuilder.Entity<Ram>().ToTable("Ram");
        modelBuilder.Entity<Remote>().ToTable("Remote", t => t.HasCheckConstraint(
            "CK_Remote_ConnectionTypeFields",
            "(ConnectionType = 0 AND AppDescription IS NOT NULL) OR " +
            "(ConnectionType = 1 AND IPAddress IS NOT NULL AND Port IS NOT NULL AND Username IS NOT NULL AND Password IS NOT NULL)"));
        modelBuilder.Entity<Maintenance>().ToTable("Maintenance");
        modelBuilder.Entity<MaintenanceType>().ToTable("MaintenanceType");
        modelBuilder.Entity<Asignation>().ToTable("Asignation");
        modelBuilder.Entity<PeripheralType>().ToTable("PeripheralType");
        modelBuilder.Entity<Peripheral>().ToTable("Peripheral");
        modelBuilder.Entity<PeripheralAssignment>().ToTable("PeripheralAssignment");
        modelBuilder.Entity<PeripheralMaintenance>().ToTable("PeripheralMaintenance");
        modelBuilder.Entity<License>().ToTable("License");
        modelBuilder.Entity<SpecChangeLog>().ToTable("SpecChangeLog");

        modelBuilder.Entity<Users>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<UserSystem>()
            .HasIndex(us => us.Username)
            .IsUnique();

        modelBuilder.Entity<UserSystem>()
            .HasIndex(us => us.UserId)
            .IsUnique();

        modelBuilder.Entity<Rol>()
            .HasIndex(r => r.Name)
            .IsUnique();

        modelBuilder.Entity<PeripheralType>()
            .HasIndex(pt => pt.Name)
            .IsUnique();

        modelBuilder.Entity<Ram>()
            .HasIndex(r => r.Especification)
            .IsUnique();

        modelBuilder.Entity<Desktop>()
            .Property(d => d.Estado)
            .HasDefaultValue(true);

        modelBuilder.Entity<Users>()
            .Property(u => u.Activo)
            .HasDefaultValue(true);

        modelBuilder.Entity<Peripheral>()
            .Property(p => p.Estado)
            .HasDefaultValue(true);

        // Configure relationships
        modelBuilder.Entity<Users>()
            .HasOne(u => u.Area)
            .WithMany()
            .HasForeignKey(u => u.AreaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Users>()
            .HasOne(u => u.Regional)
            .WithMany()
            .HasForeignKey(u => u.RegionalId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UserSystem>()
            .HasOne(us => us.Rol)
            .WithMany()
            .HasForeignKey(us => us.RolId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UserSystem>()
            .HasOne(us => us.User)
            .WithMany(u => u.UserSystems)
            .HasForeignKey(us => us.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Desktop>()
            .HasOne(d => d.OSVersion)
            .WithMany()
            .HasForeignKey(d => d.OSVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Desktop>()
            .HasOne(d => d.Ram)
            .WithMany()
            .HasForeignKey(d => d.RamId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Desktop>()
            .HasOne(d => d.Remote)
            .WithMany()
            .HasForeignKey(d => d.RemoteId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Asignation>()
            .HasOne(a => a.Desktop)
            .WithMany(d => d.Asignations)
            .HasForeignKey(a => a.DesktopId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Asignation>()
            .HasOne(a => a.User)
            .WithMany(u => u.Asignations)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Maintenance>()
            .HasOne(m => m.Desktop)
            .WithMany(d => d.Maintenances)
            .HasForeignKey(m => m.DesktopId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Maintenance>()
            .HasOne(m => m.MaintenanceType)
            .WithMany(mt => mt.Maintenances)
            .HasForeignKey(m => m.MaintenanceTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Peripheral>()
            .HasOne(p => p.Desktop)
            .WithMany(d => d.Peripherals)
            .HasForeignKey(p => p.DesktopId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Peripheral>()
            .HasOne(p => p.PeripheralType)
            .WithMany(pt => pt.Peripherals)
            .HasForeignKey(p => p.PeripheralTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PeripheralMaintenance>()
            .HasOne(m => m.Peripheral)
            .WithMany(p => p.Maintenances)
            .HasForeignKey(m => m.PeripheralId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PeripheralMaintenance>()
            .HasOne(m => m.MaintenanceType)
            .WithMany(mt => mt.PeripheralMaintenances)
            .HasForeignKey(m => m.MaintenanceTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PeripheralAssignment>()
            .HasOne(a => a.Peripheral)
            .WithMany(p => p.Assignments)
            .HasForeignKey(a => a.PeripheralId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PeripheralAssignment>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<License>()
            .HasOne(l => l.Desktop)
            .WithMany(d => d.Licenses)
            .HasForeignKey(l => l.DesktopId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SpecChangeLog>()
            .HasOne(s => s.Desktop)
            .WithMany(d => d.SpecChangeLogs)
            .HasForeignKey(s => s.DesktopId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SpecChangeLog>()
            .HasOne(s => s.ChangedByUserSystem)
            .WithMany()
            .HasForeignKey(s => s.ChangedByUserSystemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
