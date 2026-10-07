# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

GestorEquipos ("AssetOps"): ASP.NET Core 8.0 MVC (UI en español) para el ciclo de vida de equipos de cómputo — registrar `Desktop`s, asignarlos a usuarios, periféricos con ciclo de vida independiente, accesos remotos (RDP/aplicativo), licencias, mantenimientos preventivos/correctivos (checklist + PDF de acta), mantenimientos obligatorios semestrales (junio/diciembre) con envío del acta por correo y cierre mediante PDF firmado subido al sistema, baja a un perfil RAES, cargos/puestos de las personas con su propio historial, y un módulo de **Casos TI** (mesa de ayuda interna) con estadísticas y calificación del servicio por correo. EF Core sobre SQL Server; cookie auth con tres roles: `Administrador`, `Técnico en Sistemas`, `Auditor`.

Dos proyectos en `Gestor_Equipos.sln`: `GestorEquipos.csproj` (app) y `GestorEquipos.Tests/GestorEquipos.Tests.csproj` (xUnit + EF Core InMemory, sin tests de controllers/HTTP).

**Inconsistencia de namespaces:** `Models/` usa `GestorEquipos.*` (sin guion bajo); `Controllers/`, `Services/` y el `DbContext` usan `Gestor_Equipos.*` (con guion bajo). Al añadir archivos, sigue la convención de los vecinos en esa carpeta.

## Commands

`dotnet` está en `C:\Program Files\dotnet\dotnet.exe` (no en el PATH del shell); `dotnet-ef` en `$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe`.

```
dotnet build GestorEquipos.csproj
dotnet run --project GestorEquipos.csproj             # perfil "http" por defecto
dotnet test GestorEquipos.Tests/GestorEquipos.Tests.csproj
dotnet ef migrations add <Name> --project GestorEquipos.csproj --startup-project GestorEquipos.csproj
dotnet ef database update --project GestorEquipos.csproj --startup-project GestorEquipos.csproj
```

Perfiles (`Properties/launchSettings.json`): `http://localhost:5249`, `https://localhost:7274`; `ASPNETCORE_ENVIRONMENT=Development` en local.

## Architecture

### Data layer

`Models/DbContext/DbContext.cs` (`Gestor_Equipos.Data.MyDbContext`) centraliza el mapeo en `OnModelCreating` — léelo primero.

- **Catálogos simples sin UI propia** (sembrar por SQL): `Rol`, `OSVersion`, `Ram`. `Area`/`Regional` (`LocationController`), `PeripheralType`, `DesktopType` (Brand+Model), `MaintenanceType` y `Cargo` (puesto/posición de una persona) sí tienen CRUD con pantalla propia.
- **`Cargo`/`CargoAssignment`**: igual patrón que `Asignation` — `CargoAssignment` es un historial append-only Usuario↔Cargo (sin flag "actual"; el cargo vigente de una persona es su registro con mayor fecha). Un cargo puede tener varios titulares a la vez (no es 1:1 como un Desktop). Borrar un `Cargo` se bloquea si tiene algún `CargoAssignment`.
- **`Users`** (persona) / **`UserSystem`** (cuenta: `Username` único, `PasswordHash`, `SecurityStamp` Guid `NEWID()`, FK 1:1 a `Users`, FK a `Rol`).
- **`Desktop`**: `SerialNumber` único, FK `DesktopType` (Brand/Model vive ahí, no en Desktop desde la migración `RestructureDesktopTypeBrandModel`), FK `OSVersion`/`Ram`, enum `RamType`, FK opcional a `Remote` (`SetNull`), `Estado` bool.
- **`Remote`**: enum `RemoteConnectionType` con campos condicionales validados por DB `CHECK` (`CK_Remote_ConnectionTypeFields`); `Password` en texto plano.
- **`Asignation`**: join append-only Desktop↔User; la asignación "actual" es la más reciente, sin flag `IsCurrent`.
- **`Maintenance`**: ya no tiene FK a `MaintenanceType` (removida en `RemoveMaintenanceTypeFromMaintenance`) — solo `Category` (`Correctivo`/`Preventivo`). `MaintenanceType` ahora solo lo usa `PeripheralMaintenance`. Solo `Preventivo` genera `MaintenanceChecklist` (1:1) y un snapshot tomado al crear (no se re-consulta al descargar el PDF después).
- **`MaintenanceCampaignItem`**: un ítem por (Desktop activo, período `PeriodYear`/`PeriodMonth` ∈ {6,12}), `Status` (`Pendiente`/`CorreoEnviado`/`Realizado`), índice único `(DesktopId, PeriodYear, PeriodMonth)`. Se crea de forma perezosa e idempotente en `MaintenanceCampaignService.SyncOpenPeriodsAsync` (llamado desde `GET MaintenanceCampaign/Index`, sin `IHostedService`) para cada Desktop activo y cada período ya abierto (día 1 de junio/diciembre ya pasado) que todavía no tenga fila — nunca se borra ni se duplica, por eso un ítem incompleto sigue "atrasado" en los meses siguientes. FK a `Desktop`/`Maintenance`/`UserSystem` (quien subió el PDF firmado) todas `Restrict`. El PDF firmado se guarda como `varbinary(max)` en la misma fila (`SignedPdfContent`), no en filesystem.
- **`Peripheral`/`PeripheralAssignment`/`PeripheralMaintenance`**: análogos a Desktop; `Peripheral.DesktopId` opcional, `Estado` independiente del Desktop.
- **`License`**: por Desktop, `SoftwareType` libre + flag `NoLicense`.
- **`TipoCaso`**/**`CasoTI`**: catálogo de tipos de incidencia (`TipoCaso`, solo Administrador agrega) y casos de soporte TI (`CasoTI`) que un Técnico/Administrador registra al atender a una `Users` (persona). `AreaSnapshot` se copia de `Users.Area?.Name` al crear y nunca se re-consulta (mismo criterio que `Maintenance`). `Estado` (`Abierto`/`Resuelto`); al resolver (`CasoTIService.ResolveAsync`) se genera un `RatingToken` (Guid) y se envía por correo un enlace público (`CasoTIRatingController`, `[AllowAnonymous]`) para que la persona califique 1-5 con comentario — el token se "gasta" con el primer envío (`RatingSubmittedAtUtc`). Si el correo falla, la transacción revierte y el caso sigue `Abierto` sin token (mismo patrón que `MaintenanceCampaignService.CompleteAndSendAsync`).
- **`SpecChangeLog`** / **`AuditLogEntry`**: auditoría append-only (cambios de specs; cambios de rol/borrados/accesos remotos). `AuditLogEntry` deliberadamente sin FK al actor (solo texto), para sobrevivir si la cuenta se borra.

`Migrations/MyDbContextModelSnapshot.cs` es la fuente de verdad del esquema — regenerar con `dotnet ef migrations add`, nunca editar a mano. Código/vistas que asuman `Desktop.Brand`/`Model` o `Maintenance.MaintenanceTypeId` están desactualizados (ver migraciones arriba).

`Models/ViewModels/` organizado por feature, separado de las entidades EF.

### Auth

- Cookie auth (`Program.cs`): `LoginPath=/Login/Index`, 60 min sliding, `HttpOnly`, `SameSite=Lax`, `SecurePolicy=Always` fuera de Development. Fallback policy global `RequireAuthenticatedUser`.
- `Services/Auth/SessionCookieEvents` revalida en cada request contra la BD (cuenta activa, `UserId`/`SecurityStamp`/rol coinciden) — invalidación server-side sin esperar expiración.
- `Middleware/DatabaseAvailabilityMiddleware` + `Services/Diagnostics/DatabaseAvailability.IsUnavailable`: si una excepción SQL es transitoria responde `503` sin tocar la cookie ni redirigir a login (crítico porque `SessionCookieEvents` consulta la BD en cada request autenticado — un outage ahí no debe desloguear a nadie). Errores no transitorios propagan como `500`. Cubierto por `AvailabilityAndSessionTests`.
- Rate limiting `"login"` (10/min por IP, sin cola, 429) solo en `LoginController.LoginUser`; deshabilitado en entorno `Testing`.
- `ForwardedHeadersOptions` solo confía en proxies listados en `ForwardedHeaders:KnownProxies`.
- `LoginController` construye el `ClaimsPrincipal`: `ClaimTypes.NameIdentifier` = `UserSystem.Id` (cuenta, para auditoría), claim `"UserId"` = `Users.Id` (persona).
- `AuthBootstrapper.EnsureAdminAsync` corre al arrancar: migra, asegura los tres roles (`AdministradorRoleName`, `TecnicoSistemasRoleName`, `AuditorRoleName`), siembra `raes@system.local`/`sin-asignar@system.local`, y crea el admin inicial desde `BootstrapAdmin:Email`/`Password` si están en config.

**Rol `Técnico en Sistemas`**: puede crear y consultar en casi todos los módulos y hacer las operaciones del día a día (asignar/devolver equipos, reasignar/liberar periféricos y accesos remotos, enviar a RAES, desactivar una persona) — pero nunca editar ni borrar, y **sin ningún acceso** a `AccessAccountController` (cuentas de acceso/contraseñas) ni a crear filas nuevas en los catálogos cerrados (`DesktopType`, `PeripheralType`, `MaintenanceType`, `Area`/`Regional`, `Cargo` — solo Administrador agrega entradas ahí, el técnico solo las consulta/elige). La constante `AuthBootstrapper.AdminOrTecnicoRoles` (`"Administrador,Técnico en Sistemas"`) es el valor usado en casi todos los `[Authorize(Roles=...)]` de clase; un `[Authorize(Roles=AdministradorRoleName)]` de método encima restringe esa acción puntual a solo Administrador (los atributos de clase+método se combinan con AND, no con OR).

Autorización por controller (19 en `Gestor_Equipos.Controllers`): `LoginController` es `[AllowAnonymous]` de clase. Los controllers de catálogos (`CargoController`, `DesktopTypeController`, `LicenseController`, `LocationController`, `MaintenanceTypeController`, `PeripheralMaintenanceController`, `PeripheralTypeController`, `TipoCasoController`, `UserAdminController`) son `[Authorize(Roles=AdminOrTecnicoRoles)]` de clase, con `Edit`/`Delete` (y en `UserAdminController`, también `Edit`) restringidos a solo Administrador por método. `AccessAccountController` es la única excepción `[Authorize(Roles=Administrador)]` de clase sin ninguna apertura al técnico. `DesktopController`/`PeripheralController`/`RemoteController` son `[Authorize]` de clase (cualquier autenticado puede ver Index/Details) con roles por acción mutadora. `MaintenanceController` no tiene atributo de clase (cae al fallback) con roles por acción. `MaintenanceCampaignController` es `[Authorize]` de clase igual que Desktop — `Index` es accesible a los tres roles, pero el filtrado real para `Auditor` (solo ítems `Realizado`) ocurre dentro de `MaintenanceCampaignService.GetIndexAsync`, no en el atributo; `Complete`/`Upload` sí llevan `[Authorize(Roles=AdminOrTecnicoRoles)]` de método. `CasoTIController` sigue el mismo patrón que `MaintenanceCampaignController`: `[Authorize]` de clase, `Index`/`Estadisticas`/`Calificaciones` visibles a los tres roles con el scoping real (Técnico solo ve sus propios casos; Auditor solo `Resuelto`) dentro de `CasoTIService.ApplyRoleScope`, mientras que `Create`/`Resolve` llevan `[Authorize(Roles=AdminOrTecnicoRoles)]` de método. `CasoTIRatingController` es `[AllowAnonymous]` de clase (formulario público de calificación por token, sin relación con los roles del sistema). `Auditor` nunca aparece en ningún `[Authorize(Roles=...)]` — además de controlar el nav, esto significa que Auditor literalmente no puede abrir ningún catálogo/admin (`UserAdmin`, `Location`, `Cargo`, los 3 `*Type`, `TipoCaso`, `License`, `PeripheralMaintenance`, `AccessAccount`): su acceso real se limita a ver Equipos/Periféricos/Accesos remotos/Dashboard, la vista de solo-completados de Mantenimientos obligatorios, y los Casos TI ya `Resuelto`. Varios controllers inyectan `MyDbContext` directamente (para `SelectList` de catálogos) además del servicio — no hay separación estricta repositorio/servicio.

### Services layer

Interfaces en `Services/*.cs`, implementaciones en `Services/Implementations/`.

- `DesktopService`: CRUD, hoja de vida, baja a RAES, estadísticas. `DesktopTypeService`: catálogo Brand+Model, valida duplicados y bloquea borrado si hay `Desktop`s referenciándolo.
- `AsignationService`: asignación a usuarios + historial (usa cuentas pseudo RAES/"Disponible").
- `PeripheralService`/`PeripheralAssignmentService`/`PeripheralMaintenanceService`: análogos para periféricos.
- `RemoteService`: inventario/edición/liberación/asignación de accesos remotos.
- `MaintenanceService`: mantenimientos de Desktop por `Category`. `MaintenanceTypeService`: catálogo consumido solo por `PeripheralMaintenance` (ver nota de migración en Data layer).
- `LicenseService`, `LocationService` (Áreas/Regionales, protege "RAES"), `UserAdminService` (personas + cuentas; crea/edita el `CargoAssignment` de una persona como efecto colateral de `CreateUserAsync`/`UpdateUserAsync` — ver abajo), `PeripheralTypeService`.
- `CargoService`: catálogo de cargos (igual patrón que `LocationService`) + `GetHistoryAsync(cargoId)`, que calcula en memoria quién ha tenido ese cargo y cuándo: agrupa todos los `CargoAssignment` por `UserId`, y el "fin" de un tramo es la fecha del *siguiente* registro de esa misma persona (cualquier cargo), o `null` ("vigente") si es el último — mismo truco que `latestAssignationIdByDesktop` en `UserAdminService.GetDetailAsync`.
- `MaintenanceCampaignService`: motor del módulo "Mantenimientos obligatorios". `SyncOpenPeriodsAsync(referenceDate?)` abre períodos perezosamente (ver Data layer); `CompleteAndSendAsync` valida que el Desktop tenga un usuario asignado activo con email, reutiliza `IMaintenanceService.CreateAsync` (forzando `Category=Preventivo` server-side, nunca desde el cliente) dentro de una transacción (`CreateExecutionStrategy`, igual patrón que `DesktopService.DeactivateAsync`), genera el acta vía `IMaintenancePdfService.GenerateExroActaPdf` y la envía por `IEmailService` — si el correo falla, la transacción revierte y el ítem queda `Pendiente`. `UploadSignedPdfAsync`/`GetSignedPdfAsync` manejan la subida/descarga del PDF firmado (primer y único patrón de subida de archivos — `IFormFile` — en todo el repo, validado en `MaintenanceCampaignController.Upload`: extensión `.pdf`, `Content-Type`, tamaño máx. 10 MB).
- `IEmailService`/`SmtpEmailService`: envío de correo vía **MailKit** contra Gmail SMTP (`smtp.gmail.com:587`, STARTTLS). Config en `appsettings.json` → sección `Smtp` (`Host`/`Port`/`FromAddress`/`FromName`); `Smtp:Password` (contraseña de aplicación de Gmail) solo en user-secrets/`Smtp__Password` — nunca en `appsettings*.json`. Si falta configuración lanza `InvalidOperationException` (se traduce a error de formulario, no 500).
- `CasoTIService`: alta de un caso (`CreateAsync`, el Técnico siempre queda como su propio técnico — nunca confía en lo que mande el cliente), resolución con correo+token de calificación (`ResolveAsync`), formulario público de calificación (`GetRatingPromptAsync`/`SubmitRatingAsync`, sin autenticación), y estadísticas (`GetStatsAsync`: barra por día de semana/semana/mes/fecha + torta por `TipoCaso`; `GetRatingsAsync`: promedio por técnico + últimos 50 comentarios). `ApplyRoleScope` es el único punto donde se filtra por rol (Técnico → solo sus casos, Auditor → solo `Resuelto`), reutilizado por `GetIndexAsync`/`GetStatsAsync`/`GetRatingsAsync`. `TipoCasoService`: catálogo simple (igual patrón que `LocationService`), consumido solo por `CasoTI`.
- `DesktopPdfService`/`MaintenancePdfService`: PDF vía **MigraDoc** (no PdfSharp `XGraphics` directo); helpers privados duplicados entre ambos (sin clase base). `LocalFontResolver` resuelve fuentes (Windows Fonts o Liberation/DejaVu en Linux/Docker). `MaintenancePdfService` además expone `GenerateExroActaPdf` (constructor ahora recibe `IWebHostEnvironment` para ubicar `wwwroot/Images/logo-exro.png`), que replica el formato oficial de la empresa ("E-FGA-07 Control Mantenimiento V04") usado solo por el módulo de Mantenimientos obligatorios — distinto del PDF genérico de `GenerateMaintenancePdf`.

Patrones repetidos:
- **Borrado gateado por password de admin**: `CargoService`, `DesktopTypeService`, `LocationService` (`DeleteAreaAsync`/`DeleteRegionalAsync`), `MaintenanceTypeService`, `PeripheralService`, `PeripheralTypeService`, `RemoteService`, `UserAdminService` (`DeleteAccountAsync`) comparten la firma `DeleteXAsync(int id, int actingAdminUserSystemId, string? adminPassword)` contra `IPasswordHasher<UserSystem>`. Distinto de `UserAdminService.DeactivateAsync(int id)` (desactiva una persona, sin password, no borra).
- Validación de duplicados/FK antes de insertar (`DesktopService`, `LicenseService`, `MaintenanceService`, `PeripheralService`) para no dejar que una violación de constraint llegue cruda al usuario.
- Transacciones `Serializable` + `CreateExecutionStrategy` en check-then-insert con condición de carrera (`AsignationService.AssignAsync`, `RemoteService.UpdateAsync`/`AssignToDesktopAsync`).
- Registros reservados (pseudo-cuentas/área RAES y "Disponible", constantes en `AuthBootstrapper`) como destino en flujos de baja en vez de regla ad-hoc.
- Comentarios con tags `LOG-XX`/`UX-XX`/`SEG-XX` documentan decisiones y bugs corregidos — útil grep para el porqué de una validación no obvia.

### Web layer

- MVC estándar: `Controllers/`, `Views/<Controller>/*.cshtml`, `_Layout.cshtml` (autenticado, nav por rol) / `_Layout_Login.cshtml`. Ruta default `{controller=Home}/{action=Index}/{id?}`.
- Formularios: `asp-for`/`asp-validation-for` + Bootstrap, `@Html.AntiForgeryToken()` manual, `asp-validation-summary="ModelOnly"`.
- Frontend (Bootstrap, jQuery, Chart.js) **vendored** en `wwwroot/lib/` (sin libman/npm), cacheado 1 año vía middleware custom en `Program.cs`.
- Razor Runtime Compilation solo en Development; en producción/Docker las vistas quedan precompiladas.
- `QueryTimingInterceptor` (singleton, inyectado al `DbContext`) loguea SQL lento (`Warning` ≥1000ms). Compresión Brotli/Gzip y `UseHsts` fuera de Development, también en `Program.cs`.
- `wwwroot/js/cargo-quick-add.js` es la única vista con un patrón AJAX (sin librería, `fetch` + token antifalsificación leído del `<form>` que lo contiene): el "+" junto al `<select>` de Cargo en `UserAdmin/Create.cshtml`/`Edit.cshtml` llama a `CargoController.QuickCreate` y agrega la opción nueva sin recargar la página. Resto de la app usa submit normal de formulario.

## Configuration

- Connection string en `appsettings.json` → `ConnectionStrings:DefaultConnection` (retry-on-failure, `CommandTimeout(60)`).
- Admin inicial vía `dotnet user-secrets set "BootstrapAdmin:Email"/"...Password"` (no en `appsettings*.json` checkeado); en PowerShell, comillas simples si el password tiene `$`.
- Correo del módulo de Mantenimientos obligatorios: `appsettings.json` → `Smtp:Host`/`Port`/`FromAddress`/`FromName`; `dotnet user-secrets set "Smtp:Password" "..."` (contraseña de aplicación de Gmail, no la contraseña normal de la cuenta) — mismo criterio que `BootstrapAdmin:Password`, nunca en `appsettings*.json`.
- Catálogos sin UI admin (`Ram`, `OSVersion`) deben sembrarse por SQL en una BD nueva, o los dropdowns de Desktop quedan vacíos.
- `ForwardedHeaders:KnownProxies` si la app corre detrás de proxy/load balancer.

## Deployment

`Dockerfile`/`docker-compose.yml` para despliegue en contenedor (servidor de pruebas); `.env.example` documenta las variables — copiar a `.env`, nunca commitearlo. Incluye `SMTP_FROM_ADDRESS`/`SMTP_APP_PASSWORD` para el envío de correo del módulo de Mantenimientos obligatorios (mapeadas a `Smtp__FromAddress`/`Smtp__Password` en `docker-compose.yml`), ambas opcionales — si faltan, el resto de la app arranca igual, solo falla al intentar enviar un correo.
