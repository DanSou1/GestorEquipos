# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

GestorEquipos ("AssetOps") is an ASP.NET Core 8.0 MVC application that generates "hojas de vida" (full lifecycle records) for computer equipment: registering desktops, assigning them to users, tracking peripherals and their own independent lifecycle, logging preventive/corrective maintenance, and retiring equipment to a RAES (e-waste disposal) profile. It uses EF Core against SQL Server, cookie-based authentication with two roles (Administrador/Auditor), and server-rendered Razor views (Spanish-language UI).

The solution has two projects: `GestorEquipos.csproj` (the web app) and `GestorEquipos.Tests` (xUnit unit tests), both referenced by `Gestor_Equipos.sln`. See `PLAN_DESARROLLO.md` for the full design rationale and decisions behind the current data model, and `MANUAL_TESTS.md` for the manual verification checklist (kept up to date as features are exercised).

## Commands

The .NET SDK is installed at `C:\Program Files\dotnet\dotnet.exe` but is **not on PATH** in this shell environment — either call it by full path or prepend it to `$env:PATH` for the session (`$env:PATH = "C:\Program Files\dotnet;$env:PATH"`). The `dotnet-ef` global tool lives at `$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe` — add that directory to PATH too when running `dotnet ef` commands.

```
dotnet build Gestor_Equipos.sln
dotnet run --project GestorEquipos.csproj             # runs with launch profile "http" by default
dotnet test GestorEquipos.Tests/GestorEquipos.Tests.csproj
dotnet test GestorEquipos.Tests/GestorEquipos.Tests.csproj --collect:"XPlat Code Coverage"
dotnet ef migrations add <Name> --project GestorEquipos.csproj --startup-project GestorEquipos.csproj
dotnet ef database update --project GestorEquipos.csproj --startup-project GestorEquipos.csproj
```

Note: `GestorEquipos.Tests` lives *inside* the web project's directory tree, so `GestorEquipos.csproj` explicitly excludes it from SDK-style globbing (`<Compile Remove="GestorEquipos.Tests\**" />` etc.) — without that exclusion the web project would try to compile the test project's generated files.

Launch profiles (`Properties/launchSettings.json`) run on `http://localhost:5249` and `https://localhost:7274`; `ASPNETCORE_ENVIRONMENT` is `Development` for local runs.

## Testing

- Unit tests use EF Core's `Microsoft.EntityFrameworkCore.InMemory` provider (see `GestorEquipos.Tests/TestHelpers.cs`), one fresh in-memory database per test — no real SQL Server needed to run the suite.
- Coverage target: ≥80% of lines in `Services/` (the business-logic layer). As of the last full run this is ~92% aggregate; the pre-existing `Services/Auth/AuthService.cs` is the only meaningfully untested file (untouched legacy code, not part of the RAES/hoja-de-vida feature work).
- `GestorEquipos.Tests/Controllers/AuthorizationAttributeTests.cs` verifies `[Authorize]`/`[Authorize(Roles = ...)]` placement via reflection rather than spinning up a real host — cheaper and avoids needing a live SQL Server for CI-style runs.
- There's no `WebApplicationFactory`-based integration test suite; end-to-end verification (login flow, antiforgery, role-gated UI) has been done manually via scripted HTTP requests and is logged in `MANUAL_TESTS.md`, not automated.

## Configuration

- Connection string lives in `appsettings.json` under `ConnectionStrings:DefaultConnection` and points at a local named SQL Server instance. Update this per-machine rather than assuming it's portable.
- `Program.cs` calls `AuthBootstrapper.EnsureAdminAsync` on startup, which unconditionally (regardless of config) applies pending migrations (skipped if the DB provider isn't relational, so EF InMemory tests can call the same method), seeds the `Administrador`/`Auditor` roles, and seeds a well-known **RAES pseudo-user** (`AuthBootstrapper.RaesUserEmail`, currently `raes@system.local`) with its own dedicated "RAES" `Area`/`Regional` so it reads clearly in equipment lists — this user intentionally has **no** `UserSystem` (can't log in). Only the *first real admin login* additionally depends on `BootstrapAdmin:Email`/`BootstrapAdmin:Password` config being set (not present in checked-in `appsettings*.json` — use `dotnet user-secrets set "BootstrapAdmin:Email" "..."` / `...Password...` locally; **in PowerShell, wrap the password in single quotes** if it contains `$`, otherwise PowerShell string interpolation silently truncates it).
- Several lookup tables (`Ram`, `OSVersion`, `Remote`, `MaintenanceType`, `Area`, `Regional`) have no admin UI by design (out of scope) — seed them directly via SQL when setting up a fresh dev database, or the `Desktop`/`Maintenance` create forms will have empty dropdowns.

## Architecture

**Namespace inconsistency to be aware of:** the codebase mixes two root namespaces — `GestorEquipos.*` (used by `Models/`) and `Gestor_Equipos.*` (used by `Controllers/`, `Services/`, and the EF `DbContext`, which lives at `Models/DbContext/DbContext.cs` but declares namespace `Gestor_Equipos.Data`). When adding new files, match the namespace convention of the folder's existing siblings rather than assuming project-wide consistency. One further wrinkle: `Controllers/UserController.cs` is a misleadingly-named file — it actually defines the `LoginController` class, not a `UserController`. Don't be misled by the filename; there's no separate `UserController`.

**Data layer**
- `Models/DbContext/DbContext.cs` (`Gestor_Equipos.Data.MyDbContext`) is the single EF Core `DbContext`. All entity-to-table mappings, unique indexes, and relationship/delete-behavior configuration are centralized in its `OnModelCreating` — read this first to understand the whole data model.
- Core entities (`Models/*.cs`): `Users` (a person), `UserSystem` (a login account: `Username`/`PasswordHash`/`Rol`, one-to-one with `Users` via a unique index on `UserId` despite the model nominally supporting a collection), `Rol` (`Administrador` or `Auditor`), `Area`, `Regional`, `Desktop` (a physical computer — `OSVersion`, `Ram`, optional `Remote`, `Estado` bool for Activo/Inactivo), `Asignation` (append-only join: one row per assignment event — the "current" assignment for a desktop is simply its most recent `Asignation` by date, there's no separate `IsCurrent` flag), `Maintenance`/`MaintenanceType` (service history per `Desktop`, `Maintenance.TechnicianUserSystemId` is a required FK to `UserSystem`), `Peripheral`/`PeripheralType` (peripherals belong to a `Desktop`, with their own `Estado` enum — `Activo`/`Inactivo`/`Raes` — **independent** of the desktop's Estado), `PeripheralObservation` (append-only event log per peripheral: `Reparacion`/`Cambio`/`BajaRAES`, drives `Peripheral.Estado` transitions), `License` (per-`Desktop`, free-text `SoftwareType` + optional `LicenseKey` + `NoLicense` flag for legitimately-unlicensed software), `SpecChangeLog` (append-only audit trail of `Desktop` spec edits: field name, old/new value, who, when).
- **RAES lifecycle**: deactivating a `Desktop` (`IDesktopService.DeactivateAsync`) sets `Estado = false` *and* creates a new `Asignation` pointing at the RAES pseudo-user — this reuses the existing assignment-history mechanism to timestamp when it happened, and it shows up as "RAES" in the Usuario/Área/Regional columns of the equipment list. This is a **one-way** transition by design; there's no reactivate action anywhere in the codebase.
- Migrations live in `Migrations/`; `MyDbContextModelSnapshot.cs` is the authoritative model snapshot — regenerate via `dotnet ef migrations add`, never hand-edit.

**Auth**
- Cookie authentication (`Program.cs`), with a global fallback policy requiring an authenticated user for every request (`AddAuthorization` → `RequireAuthenticatedUser`) — controllers/actions must opt out explicitly with `[AllowAnonymous]` (see `LoginController`) rather than opting in with `[Authorize]`. Read-only actions use bare `[Authorize]`; mutating actions use `[Authorize(Roles = AuthBootstrapper.AdministradorRoleName)]` (reference the constant, don't hardcode the string "Administrador").
- `Services/Auth/IAuthService` / `AuthService` validates credentials against `UserSystem` + `Users` + `Rol` using ASP.NET Identity's `IPasswordHasher<UserSystem>`, including transparent rehash-on-verify.
- `LoginController` (in `Controllers/UserController.cs`) builds the `ClaimsPrincipal` from an `AuthenticatedUser` record — `ClaimTypes.NameIdentifier` holds the **`UserSystem.Id`** (the login-account id, used e.g. as `ChangedByUserSystemId`/`TechnicianUserSystemId` when a controller needs "who is doing this"), while a separate `"UserId"` claim holds the `Users.Id` (the person).
- `Services/Auth/AuthBootstrapper` — see Configuration section above for what it seeds and when.

**Services layer** (`Gestor_Equipos.Services.*`, interface in `Services/`, implementation in `Services/Implementations/`, mirroring the pre-existing `IAuthService`/`AuthService` pattern)
- `IDesktopService`/`DesktopService`: `GetAllAsync` (equipment list, latest-assignment-per-desktop via a correlated subquery, not N+1 loops), `GetDetailAsync` (full hoja de vida), `CreateAsync`, `UpdateSpecsAsync` (diffs every editable field against the current row and writes a `SpecChangeLog` entry per change before applying it), `DeactivateAsync` (RAES transition, delegates the actual reassignment to `IAsignationService` rather than duplicating that logic).
- `IAsignationService`/`AsignationService`, `IPeripheralService`/`PeripheralService` (owns the `PeripheralObservation` → `Peripheral.Estado` transition rules), `IMaintenanceService`/`MaintenanceService`, `ILicenseService`/`LicenseService` (forces `LicenseKey = null` server-side whenever `NoLicense = true`, regardless of what was posted), `IUserAdminService`/`UserAdminService` (splits person creation and login-account creation into two methods — `CreateUserAsync` then `CreateLoginAsync` — so a controller can compose them; both defensively check for duplicate email/username before insert since the DB's unique indexes would otherwise surface as a raw `DbUpdateException`).
- View-specific projections live under `Models/ViewModels/<Feature>/` (e.g. `Models/ViewModels/Desktop/`), separate from the EF entities in `Models/`. Controllers inject `MyDbContext` directly (alongside the relevant service) purely to populate `SelectList` dropdowns for lookup tables — that's a deliberate, scoped exception to "controllers use services," not a pattern to extend into business logic.

**Web layer**
- Standard ASP.NET Core MVC: `Controllers/`, `Views/<Controller>/*.cshtml`, shared layouts in `Views/Shared/` (`_Layout.cshtml` for authenticated pages, `_Layout_Login.cshtml` for the login page). Default route is `{controller=Home}/{action=Index}/{id?}`. Nav links to "Equipos"/"Usuarios"/"Tipos de periférico" in `_Layout.cshtml` are gated with `@if (User.IsInRole("Administrador"))` where relevant.
- Client-side dependencies (Bootstrap, jQuery, jQuery Validation) are vendored under `wwwroot/lib/` rather than pulled via a package manager/CDN. Forms follow the `asp-for`/`asp-validation-for` + Bootstrap `form-control`/`form-select` convention established in `Views/Login/Index.cshtml`.
- Razor views are compiled via `Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation` — `dotnet build` still validates `.cshtml` syntax (it'll fail on a bad view), but a missing view file won't surface until the action is actually invoked.
