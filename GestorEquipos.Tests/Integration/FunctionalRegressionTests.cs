using System.Net;
using Gestor_Equipos.Services.Auth;
using GestorEquipos.Models;
using Microsoft.EntityFrameworkCore;

namespace GestorEquipos.Tests.Integration
{
    public class FunctionalRegressionTests(SessionApplicationFactory factory) : IClassFixture<SessionApplicationFactory>
    {
        [SqlServerFact]
        public async Task F01_LoginRememberAndLogout()
        {
            using var session = factory.CreateSession();
            var account = await factory.CreateAccountAsync();
            await factory.LoginAsync(session, account, rememberMe: true);
            Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/Home/Index")).StatusCode);
            using var logout = await SessionApplicationFactory.PostFormAsync(session, "/Home/Index", "/Login/Logout", new());
            Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
            Assert.Contains("/Login", logout.Headers.Location?.OriginalString);
            SessionRevocationTests.AssertLoginRedirect(await session.GetAsync("/Home/Index"));
        }

        [SqlServerFact]
        public async Task F02_DashboardAndRoleNavigation()
        {
            using var admin = await AdminAsync();
            using var auditor = factory.CreateSession();
            await factory.LoginAsync(auditor, await factory.CreateAccountAsync(AuthBootstrapper.AuditorRoleName));
            foreach (var session in new[] { admin, auditor })
            {
                foreach (var route in new[] { "/Home/Index", "/Desktop/Index", "/Peripheral/Index", "/Remote/Index" })
                {
                    Assert.Equal(HttpStatusCode.OK, (await session.GetAsync(route)).StatusCode);
                }
            }
            var adminHtml = await admin.GetStringAsync("/Home/Index");
            var auditorHtml = await auditor.GetStringAsync("/Home/Index");
            Assert.Contains("/AccessAccount", adminHtml);
            Assert.DoesNotContain("/AccessAccount", auditorHtml);
        }

        [SqlServerFact]
        public async Task F03_AccountCreateEditDelete()
        {
            using var session = await AdminAsync();
            var email = Unique() + "@functional.invalid";
            var roleId = await factory.ReadAsync(database => database.Rols.Where(role => role.Name == "Auditor").Select(role => role.Id).SingleAsync());
            var fields = new Dictionary<string, string>
            {
                ["Name"] = "Account", ["LastName"] = "Test", ["Email"] = email, ["Username"] = email,
                ["Password"] = factory.Password, ["RolId"] = roleId.ToString()
            };
            await PostAsync(session, "/AccessAccount/Create", "/AccessAccount/Create", fields);
            var personId = await factory.ReadAsync(database => database.Users.Where(person => person.Email == email).Select(person => person.Id).SingleAsync());
            fields["Name"] = "Changed";
            await PostAsync(session, "/AccessAccount/Edit/" + personId, "/AccessAccount/Edit/" + personId, fields);
            Assert.Equal("Changed", await factory.ReadAsync(database => database.Users.Where(person => person.Id == personId).Select(person => person.Name).SingleAsync()));
            await PostAsync(session, "/AccessAccount/Index", "/AccessAccount/Delete", new() { ["id"] = personId.ToString(), ["adminPassword"] = factory.Password });
            Assert.False(await factory.ReadAsync(database => database.UserSystems.AnyAsync(account => account.UserId == personId)));
        }

        [SqlServerFact]
        public async Task SEG03_CannotDemoteOrDeactivateTheOnlyEnabledAdmin()
        {
            using var session = factory.CreateSession();
            var account = await factory.CreateAccountAsync();
            await factory.LoginAsync(session, account);

            // This fixture's database is shared across every test in the class (IClassFixture), so other
            // tests may have already created their own Administrador accounts. Deactivate every other
            // enabled admin so `account` is genuinely the last one before exercising the guard.
            await factory.WithDatabaseAsync(async (database, _) =>
            {
                var adminRoleId = await database.Rols.Where(role => role.Name == AuthBootstrapper.AdministradorRoleName).Select(role => role.Id).SingleAsync();
                var otherAdmins = await database.UserSystems.Include(us => us.User)
                    .Where(us => us.Id != account.Id && us.RolId == adminRoleId && us.User.Activo)
                    .ToListAsync();
                foreach (var other in otherAdmins)
                {
                    other.User.Activo = false;
                }
                await database.SaveChangesAsync();
            });

            var auditorRoleId = await factory.ReadAsync(database => database.Rols.Where(role => role.Name == "Auditor").Select(role => role.Id).SingleAsync());
            var editFields = new Dictionary<string, string>
            {
                ["Name"] = account.User.Name, ["LastName"] = account.User.LastName, ["Email"] = account.User.Email,
                ["Username"] = account.Username, ["RolId"] = auditorRoleId.ToString()
            };
            using var editResponse = await SessionApplicationFactory.PostFormAsync(session, "/AccessAccount/Edit/" + account.UserId, "/AccessAccount/Edit/" + account.UserId, editFields);
            Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);
            Assert.Equal(AuthBootstrapper.AdministradorRoleName, await factory.ReadAsync(database => database.UserSystems.Where(us => us.Id == account.Id).Select(us => us.Rol.Name).SingleAsync()));

            using var deactivateResponse = await SessionApplicationFactory.PostFormAsync(session, "/UserAdmin/Details/" + account.UserId, "/UserAdmin/Deactivate", new() { ["id"] = account.UserId.ToString() });
            Assert.Equal(HttpStatusCode.Redirect, deactivateResponse.StatusCode);
            Assert.True(await factory.ReadAsync(database => database.Users.Where(u => u.Id == account.UserId).Select(u => u.Activo).SingleAsync()));
        }

        [SqlServerFact]
        public async Task F04_UserCreateEditDeactivate()
        {
            using var session = await AdminAsync();
            var email = Unique() + "@functional.invalid";
            var fields = new Dictionary<string, string> { ["Name"] = "Person", ["LastName"] = "Test", ["Email"] = email };
            await PostAsync(session, "/UserAdmin/Create", "/UserAdmin/Create", fields);
            var personId = await factory.ReadAsync(database => database.Users.Where(person => person.Email == email).Select(person => person.Id).SingleAsync());
            fields["Name"] = "Changed";
            await PostAsync(session, "/UserAdmin/Edit/" + personId, "/UserAdmin/Edit/" + personId, fields);
            Assert.Equal("Changed", await factory.ReadAsync(database => database.Users.Where(person => person.Id == personId).Select(person => person.Name).SingleAsync()));
            await PostAsync(session, "/UserAdmin/Index", "/UserAdmin/Deactivate", new() { ["id"] = personId.ToString() });
            Assert.False(await factory.ReadAsync(database => database.Users.Where(person => person.Id == personId).Select(person => person.Activo).SingleAsync()));
        }

        [SqlServerFact]
        public async Task F05_AreaCreateEditDelete()
        {
            using var session = await AdminAsync();
            var name = Unique();
            await PostAsync(session, "/Location/CreateArea", "/Location/CreateArea", new() { ["Name"] = name });
            var areaId = await factory.ReadAsync(database => database.Areas.Where(area => area.Name == name).Select(area => area.Id).SingleAsync());
            await PostAsync(session, "/Location/EditArea/" + areaId, "/Location/EditArea/" + areaId, new() { ["Name"] = name + "Edit" });
            Assert.Equal(name + "Edit", await factory.ReadAsync(database => database.Areas.Where(area => area.Id == areaId).Select(area => area.Name).SingleAsync()));
            await PostAsync(session, "/Location/Index", "/Location/DeleteArea", new() { ["id"] = areaId.ToString(), ["adminPassword"] = factory.Password });
            Assert.False(await factory.ReadAsync(database => database.Areas.AnyAsync(area => area.Id == areaId)));
        }

        [SqlServerFact]
        public async Task F06_RegionalCreateEditDelete()
        {
            using var session = await AdminAsync();
            var name = Unique();
            await PostAsync(session, "/Location/CreateRegional", "/Location/CreateRegional", new() { ["Name"] = name });
            var regionalId = await factory.ReadAsync(database => database.Regionals.Where(regional => regional.Name == name).Select(regional => regional.Id).SingleAsync());
            await PostAsync(session, "/Location/EditRegional/" + regionalId, "/Location/EditRegional/" + regionalId, new() { ["Name"] = name + "Edit" });
            Assert.Equal(name + "Edit", await factory.ReadAsync(database => database.Regionals.Where(regional => regional.Id == regionalId).Select(regional => regional.Name).SingleAsync()));
            await PostAsync(session, "/Location/Index", "/Location/DeleteRegional", new() { ["id"] = regionalId.ToString(), ["adminPassword"] = factory.Password });
            Assert.False(await factory.ReadAsync(database => database.Regionals.AnyAsync(regional => regional.Id == regionalId)));
        }

        [SqlServerFact]
        public async Task F07_DesktopCreateEditHistoryAndPdf()
        {
            using var session = await AdminAsync();
            var fields = await DesktopFieldsAsync();
            await PostAsync(session, "/Desktop/Create", "/Desktop/Create", fields);
            var desktopId = await DesktopIdAsync(fields["SerialNumber"]);
            fields["Disk"] = "512GB";
            await PostAsync(session, "/Desktop/Edit/" + desktopId, "/Desktop/Edit/" + desktopId, fields);
            Assert.True(await factory.ReadAsync(database => database.SpecChangeLogs.AnyAsync(change => change.DesktopId == desktopId && change.NewValue == "512GB")));
            Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/Desktop/Details/" + desktopId)).StatusCode);
            await AssertPdfAsync(session, "/Desktop/DownloadPdf/" + desktopId);
        }

        [SqlServerFact]
        public async Task F07b_DesktopCreateSurvivesMalformedRamCatalog()
        {
            // Ram.Especification is seeded directly via SQL with no admin UI/validation (see CLAUDE.md);
            // a row that isn't an exact "<number>GB" string used to throw a FormatException while
            // sorting the dropdown, taking down every Desktop Create/Edit page load app-wide.
            using var session = await AdminAsync();
            var desktopId = await CreateDesktopAsync(session);
            await factory.WithDatabaseAsync(async (database, _) =>
            {
                database.Rams.Add(new Ram { Especification = "16 GiB" });
                database.Rams.Add(new Ram { Especification = "Sin especificar" });
                await database.SaveChangesAsync();
            });
            Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/Desktop/Create")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/Desktop/Edit/" + desktopId)).StatusCode);
        }

        [SqlServerFact]
        public async Task F08_DesktopAssignAndRetire()
        {
            using var session = await AdminAsync();
            var desktopId = await CreateDesktopAsync(session);
            var person = new Users { Name = "Holder", LastName = "Test", Email = Unique() + "@functional.invalid" };
            await factory.WithDatabaseAsync(async (database, _) => { database.Users.Add(person); await database.SaveChangesAsync(); });
            await PostAsync(session, "/Desktop/Assign?desktopId=" + desktopId, "/Desktop/Assign", new() { ["desktopId"] = desktopId.ToString(), ["userId"] = person.Id.ToString() });
            Assert.True(await factory.ReadAsync(database => database.Asignations.AnyAsync(assignment => assignment.DesktopId == desktopId && assignment.UserId == person.Id)));
            await PostAsync(session, "/Desktop/Details/" + desktopId, "/Desktop/Deactivate", new() { ["id"] = desktopId.ToString() });
            Assert.False(await factory.ReadAsync(database => database.Desktops.Where(desktop => desktop.Id == desktopId).Select(desktop => desktop.Estado).SingleAsync()));
            Assert.True(await factory.ReadAsync(database => database.Asignations.AnyAsync(assignment => assignment.DesktopId == desktopId && assignment.User.Email == AuthBootstrapper.RaesUserEmail)));
        }

        [SqlServerFact]
        public async Task F09_PeripheralCreateEditAndRetire()
        {
            using var session = await AdminAsync();
            var desktopId = await CreateDesktopAsync(session);
            var typeId = await factory.ReadAsync(database => database.PeripheralTypes.Select(type => type.Id).FirstAsync());
            var serial = Unique();
            var fields = new Dictionary<string, string> { ["DesktopId"] = desktopId.ToString(), ["PeripheralTypeId"] = typeId.ToString(), ["Brand"] = "Test", ["Model"] = "Mouse", ["Serial"] = serial };
            await PostAsync(session, "/Peripheral/Create?desktopId=" + desktopId, "/Peripheral/Create", fields);
            var peripheralId = await factory.ReadAsync(database => database.Peripherals.Where(peripheral => peripheral.Serial == serial).Select(peripheral => peripheral.Id).SingleAsync());
            fields["Model"] = "Changed";
            await PostAsync(session, "/Peripheral/Edit/" + peripheralId, "/Peripheral/Edit/" + peripheralId, fields);
            Assert.Equal("Changed", await factory.ReadAsync(database => database.Peripherals.Where(peripheral => peripheral.Id == peripheralId).Select(peripheral => peripheral.Model).SingleAsync()));
            Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/Peripheral/Details/" + peripheralId)).StatusCode);
            await PostAsync(session, "/Peripheral/Details/" + peripheralId, "/Peripheral/RetireToRaes", new() { ["id"] = peripheralId.ToString() });
            Assert.False(await factory.ReadAsync(database => database.Peripherals.Where(peripheral => peripheral.Id == peripheralId).Select(peripheral => peripheral.Estado).SingleAsync()));
        }

        [SqlServerFact]
        public async Task F10_PeripheralTypeCreateEditDelete()
        {
            using var session = await AdminAsync();
            var name = Unique();
            await PostAsync(session, "/PeripheralType/Create", "/PeripheralType/Create", new() { ["Name"] = name });
            var typeId = await factory.ReadAsync(database => database.PeripheralTypes.Where(type => type.Name == name).Select(type => type.Id).SingleAsync());
            await PostAsync(session, "/PeripheralType/Edit/" + typeId, "/PeripheralType/Edit/" + typeId, new() { ["Name"] = name + "Edit" });
            Assert.Equal(name + "Edit", await factory.ReadAsync(database => database.PeripheralTypes.Where(type => type.Id == typeId).Select(type => type.Name).SingleAsync()));
            await PostAsync(session, "/PeripheralType/Index", "/PeripheralType/Delete", new() { ["id"] = typeId.ToString(), ["adminPassword"] = factory.Password });
            Assert.False(await factory.ReadAsync(database => database.PeripheralTypes.AnyAsync(type => type.Id == typeId)));
        }

        [SqlServerFact]
        public async Task F11_RemoteReadEditReleaseAssign()
        {
            using var session = await AdminAsync();
            var fields = await DesktopFieldsAsync();
            fields["RemoteSelection"] = "new";
            fields["NewRemoteConnectionType"] = "0";
            fields["NewRemoteAppDescription"] = "Test application";
            await PostAsync(session, "/Desktop/Create", "/Desktop/Create", fields);
            var desktopId = await DesktopIdAsync(fields["SerialNumber"]);
            var remoteId = await factory.ReadAsync(database => database.Desktops.Where(desktop => desktop.Id == desktopId).Select(desktop => desktop.RemoteId!.Value).SingleAsync());
            Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/Remote/Details/" + remoteId)).StatusCode);
            await PostAsync(session, "/Remote/Edit/" + remoteId, "/Remote/Edit/" + remoteId, new() { ["ConnectionType"] = "0", ["AppDescription"] = "Changed", ["DesktopId"] = desktopId.ToString() });
            Assert.Equal("Changed", await factory.ReadAsync(database => database.Remotes.Where(remote => remote.Id == remoteId).Select(remote => remote.AppDescription).SingleAsync()));
            await PostAsync(session, "/Remote/Details/" + remoteId, "/Remote/Release", new() { ["id"] = remoteId.ToString() });
            Assert.Null(await factory.ReadAsync(database => database.Desktops.Where(desktop => desktop.Id == desktopId).Select(desktop => desktop.RemoteId).SingleAsync()));
            await PostAsync(session, "/Remote/Assign?remoteId=" + remoteId, "/Remote/Assign", new() { ["remoteId"] = remoteId.ToString(), ["desktopId"] = desktopId.ToString() });
            Assert.Equal(remoteId, await factory.ReadAsync(database => database.Desktops.Where(desktop => desktop.Id == desktopId).Select(desktop => desktop.RemoteId).SingleAsync()));
        }

        [SqlServerFact]
        public async Task F12_MaintenanceCreateAndPdf()
        {
            using var session = await AdminAsync();
            var desktopId = await CreateDesktopAsync(session);
            var type = new MaintenanceType { Type = Unique() };
            await factory.WithDatabaseAsync(async (database, _) => { database.MaintenanceTypes.Add(type); await database.SaveChangesAsync(); });
            await PostAsync(session, "/Maintenance/Create?desktopId=" + desktopId, "/Maintenance/Create", new()
            {
                ["DesktopId"] = desktopId.ToString(), ["MaintenanceTypeId"] = type.Id.ToString(), ["Category"] = "0",
                ["Date"] = "2026-09-08", ["Description"] = "Test maintenance", ["TechnicianName"] = "Test technician"
            });
            var maintenanceId = await factory.ReadAsync(database => database.Maintenances.Where(maintenance => maintenance.DesktopId == desktopId).Select(maintenance => maintenance.Id).SingleAsync());
            await AssertPdfAsync(session, "/Maintenance/DownloadPdf/" + maintenanceId);
        }

        [SqlServerFact]
        public async Task F13_MaintenanceTypeCreateEditDelete()
        {
            using var session = await AdminAsync();
            var name = Unique();
            await PostAsync(session, "/MaintenanceType/Create", "/MaintenanceType/Create", new() { ["Type"] = name });
            var typeId = await factory.ReadAsync(database => database.MaintenanceTypes.Where(type => type.Type == name).Select(type => type.Id).SingleAsync());
            await PostAsync(session, "/MaintenanceType/Edit/" + typeId, "/MaintenanceType/Edit/" + typeId, new() { ["Type"] = name + "Edit" });
            Assert.Equal(name + "Edit", await factory.ReadAsync(database => database.MaintenanceTypes.Where(type => type.Id == typeId).Select(type => type.Type).SingleAsync()));
            await PostAsync(session, "/MaintenanceType/Index", "/MaintenanceType/Delete", new() { ["id"] = typeId.ToString(), ["adminPassword"] = factory.Password });
            Assert.False(await factory.ReadAsync(database => database.MaintenanceTypes.AnyAsync(type => type.Id == typeId)));
        }

        [SqlServerFact]
        public async Task F14_LicenseCreateAndNoLicense()
        {
            using var session = await AdminAsync();
            var desktopId = await CreateDesktopAsync(session);
            await PostAsync(session, "/License/Create?desktopId=" + desktopId, "/License/Create", new() { ["DesktopId"] = desktopId.ToString(), ["SoftwareType"] = "Licensed test", ["LicenseKey"] = "FICTITIOUS-KEY" });
            Assert.True(await factory.ReadAsync(database => database.Licenses.AnyAsync(license => license.DesktopId == desktopId && license.LicenseKey == "FICTITIOUS-KEY")));
            await PostAsync(session, "/License/Create?desktopId=" + desktopId, "/License/Create", new() { ["DesktopId"] = desktopId.ToString(), ["SoftwareType"] = "Unlicensed test", ["NoLicense"] = "true", ["LicenseKey"] = "DISCARD" });
            Assert.True(await factory.ReadAsync(database => database.Licenses.AnyAsync(license => license.DesktopId == desktopId && license.NoLicense && license.LicenseKey == null)));
        }

        private async Task<HttpClient> AdminAsync()
        {
            var session = factory.CreateSession();
            await factory.LoginAsync(session, await factory.CreateAccountAsync());
            return session;
        }

        private async Task<Dictionary<string, string>> DesktopFieldsAsync()
        {
            var osVersion = new OSVersion { TypeSO = "Windows", Version = "Test" };
            int ramId = 0;
            await factory.WithDatabaseAsync(async (database, _) =>
            {
                database.OSVersions.Add(osVersion);
                var ram = await database.Rams.FirstOrDefaultAsync();
                if (ram is null) { ram = new Ram { Especification = "8GB" }; database.Rams.Add(ram); }
                await database.SaveChangesAsync();
                ramId = ram.Id;
            });
            return new()
            {
                ["NameDesktop"] = "Test-PC", ["SerialNumber"] = Unique(), ["Brand"] = "Test", ["Model"] = "Test",
                ["Processor"] = "CPU", ["Disk"] = "256GB", ["OSVersionId"] = osVersion.Id.ToString(), ["RamId"] = ramId.ToString(),
                ["RamType"] = "0", ["RemoteSelection"] = ""
            };
        }

        private async Task<int> CreateDesktopAsync(HttpClient session)
        {
            var fields = await DesktopFieldsAsync();
            await PostAsync(session, "/Desktop/Create", "/Desktop/Create", fields);
            return await DesktopIdAsync(fields["SerialNumber"]);
        }

        private Task<int> DesktopIdAsync(string serial)
        {
            return factory.ReadAsync(database => database.Desktops.Where(desktop => desktop.SerialNumber == serial).Select(desktop => desktop.Id).SingleAsync());
        }

        private static async Task PostAsync(HttpClient session, string formPath, string target, Dictionary<string, string> fields)
        {
            using var response = await SessionApplicationFactory.PostFormAsync(session, formPath, target, fields);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.DoesNotContain("/Login/", response.Headers.Location?.OriginalString ?? "");
        }

        private static async Task AssertPdfAsync(HttpClient session, string path)
        {
            using var response = await session.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            Assert.True(bytes.Length > 100);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        }

        private static string Unique() => "Seg01-" + Guid.NewGuid().ToString("N");
    }
}
