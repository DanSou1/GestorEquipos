using Gestor_Equipos.Services.Implementations;
using GestorEquipos.Models;
using Microsoft.AspNetCore.Identity;

namespace GestorEquipos.Tests.Services;

public class InventoryReadTests
{
    [Fact]
    public async Task LatestAssignmentUsesDateThenIdAndPreservesUnassignedItems()
    {
        using var db = TestHelpers.CreateDbContext();
        var desktop = TestHelpers.CreateDesktop(db);
        var unassigned = TestHelpers.CreateDesktop(db, "EMPTY");
        var older = TestHelpers.CreateUser(db, "Older");
        var winner = TestHelpers.CreateUser(db, "Winner", activo: false);
        var date = new DateOnly(2026, 9, 30);
        db.Asignations.AddRange(
            new Asignation { Id = 1, DesktopId = desktop.Id, UserId = older.Id, DateAsignation = date },
            new Asignation { Id = 2, DesktopId = desktop.Id, UserId = winner.Id, DateAsignation = date },
            new Asignation { Id = 3, DesktopId = desktop.Id, UserId = older.Id, DateAsignation = date.AddDays(-1) });
        var type = new PeripheralType { Name = "Monitor" };
        var peripheral = new Peripheral { DesktopId = desktop.Id, PeripheralType = type, Brand = "Dell", Model = "Test" };
        var empty = new Peripheral { PeripheralType = type, Brand = "Dell", Model = "Empty" };
        db.Peripherals.AddRange(peripheral, empty);
        await db.SaveChangesAsync();
        db.PeripheralAssignments.AddRange(
            new PeripheralAssignment { Id = 1, PeripheralId = peripheral.Id, UserId = older.Id, DateAsignation = date },
            new PeripheralAssignment { Id = 2, PeripheralId = peripheral.Id, UserId = winner.Id, DateAsignation = date },
            new PeripheralAssignment { Id = 3, PeripheralId = peripheral.Id, UserId = older.Id, DateAsignation = date.AddDays(-1) });
        await db.SaveChangesAsync();

        var desktops = await new DesktopService(db, new AsignationService(db)).GetAllAsync();
        Assert.Equal("Disponible", desktops.Single(d => d.Id == desktop.Id).UserName);
        Assert.Equal("Sin asignar", desktops.Single(d => d.Id == unassigned.Id).UserName);
        var service = new PeripheralService(db, new PasswordHasher<UserSystem>(),
            new PeripheralAssignmentService(db), new PeripheralMaintenanceService(db));
        var peripherals = await service.GetInventoryAsync();
        Assert.Equal("Disponible", peripherals.Single(p => p.Id == peripheral.Id).CurrentHolderName);
        Assert.Equal("Disponible", peripherals.Single(p => p.Id == peripheral.Id).Estado);
        Assert.Equal("Sin asignar", peripherals.Single(p => p.Id == empty.Id).CurrentHolderName);

        winner.Activo = true;
        await db.SaveChangesAsync();
        peripherals = await service.GetInventoryAsync();
        Assert.Equal("Winner Apellido", peripherals.Single(p => p.Id == peripheral.Id).CurrentHolderName);
        Assert.Equal("Activo", peripherals.Single(p => p.Id == peripheral.Id).Estado);
        peripheral.Estado = false;
        await db.SaveChangesAsync();
        Assert.Equal("Raes", (await service.GetInventoryAsync()).Single(p => p.Id == peripheral.Id).Estado);
    }

    [Fact]
    public async Task EmptyInventoriesReturnEmptyLists()
    {
        using var db = TestHelpers.CreateDbContext();
        Assert.Empty(await new DesktopService(db, new AsignationService(db)).GetAllAsync());
        Assert.Empty(await new PeripheralService(db, new PasswordHasher<UserSystem>(),
            new PeripheralAssignmentService(db), new PeripheralMaintenanceService(db)).GetInventoryAsync());
    }

    [Fact]
    public async Task CancelledInventoryRequestStopsQuery()
    {
        using var db = TestHelpers.CreateDbContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new DesktopService(db, new AsignationService(db)).GetAllAsync(cancellation.Token));
    }
}
