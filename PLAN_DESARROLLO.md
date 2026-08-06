# Plan: Gestor de Equipos — hojas de vida, asignaciones, mantenimientos y RAES

## Contexto

El propósito del aplicativo es generar **hojas de vida** de equipos de cómputo (desktops). Al iniciar este plan, el proyecto solo tenía el esqueleto: modelos EF Core para Desktop/Users/Asignation/Maintenance/etc., login funcional, y una lista de equipos completamente sin implementar (`Services/DesktopService.cs` tenía código roto/incompleto, no compilaba).

El documento de requerimientos (`Desktop\Gestor de equipos.txt`, en el escritorio del usuario) pide: registrar equipos, asignarlos a usuarios, guardar historial de usuarios y periféricos por equipo, guardar historial de mantenimientos preventivos/correctivos con técnico y observaciones, dos roles (Administrador con control total, Auditor de solo lectura), gestión de periféricos con su propio ciclo de vida/desactivación, un flujo de baja de equipos/periféricos hacia un perfil "RAES" (disposición oficial de residuos electrónicos), una vista general de equipos (6 columnas), y acceso desde ahí a la hoja de vida completa de cada equipo.

Todas las decisiones de diseño ambiguas se resolvieron directamente con el usuario (ver sección "Decisiones confirmadas"). La base de datos (`AssetOps`) está vacía/sin datos reales al momento de planear, lo que simplifica la migración (no hay que preocuparse por backfill de columnas nuevas obligatorias).

## Decisiones confirmadas con el usuario

1. **Estado de Desktop**: campo `Estado` (bool, Activo/Inactivo). Al desactivar, además se crea una nueva fila en `Asignation` apuntando a un usuario especial "RAES" (reutiliza el mecanismo de historial de asignaciones ya existente). Proceso de un solo sentido — **no hay reactivación**.
2. **Tipos de periférico**: catálogo fijo administrable (`PeripheralType`), igual patrón que `Ram`/`OSVersion`/`Remote`.
3. **Periféricos ligados a un equipo** (`Peripheral.DesktopId`), no asignados directamente a un usuario.
4. **Estado de Peripheral es independiente del Estado de Desktop** (un periférico puede fallar/ir a RAES o repararse aunque el equipo siga activo). Necesita historial de eventos acumulado (`PeripheralObservation`: fecha, tipo [Reparación/Cambio/Baja RAES], descripción), y `Peripheral.Estado` se actualiza según el último evento.
5. **Técnico de mantenimiento**: debe ser un usuario del sistema con login (`UserSystem`), FK obligatoria desde el inicio (BD vacía, sin riesgo de migración).
6. **Cambios de especificaciones** (RAM, etc.): se registra historial completo (`SpecChangeLog`: campo, valor anterior, valor nuevo, fecha, quién lo hizo), no solo se sobreescribe.
7. **Licencias**: modelo simple por equipo — `SoftwareType` (texto libre, ej. "Office 2016"), `LicenseKey` (opcional), y bandera explícita `NoLicense` para casos sin licencia (ej. Windows sin licenciar).
8. **Creación de usuarios**: se elimina la pantalla de auto-registro (`Views/Home/Register.cshtml` + `HomeController.Register`, es un mockup Tailwind desconectado). Se reemplaza por un panel de administración (`UserAdminController`) donde solo el rol Administrador crea `Users` + `UserSystem` + asigna `Rol`.
9. **Roles**: solo "Administrador" (CRUD total) y "Auditor" (solo lectura). Acciones de escritura → `[Authorize(Roles = "Administrador")]`; acciones de lectura → `[Authorize]`.
10. **Lista de equipos**: 6 columnas — Nombre de equipo, Serial, Usuario asignado, Área, Regional, Estado. Por defecto se muestran **todos** los equipos (activos e inactivos/RAES) con un filtro opcional por Estado.
11. **Exportar PDF/Excel**: fuera de alcance en esta versión.
12. **Usuario especial "RAES"**: se siembra un `Users` (sin `UserSystem`, no necesita login) con Área y Regional propias, ambas llamadas "RAES", para que se vea claramente en la lista de equipos en vez de datos administrativos reales.

## Modelo de datos — cambios

### Modificar `Models/Desktop.cs`
- Agregar `[Required] public bool Estado { get; set; } = true;` (default `true` = Activo).
- Eliminar la propiedad de navegación muerta `ICollection<Users> User` (no tiene FK configurada, no se usa en ningún lado — verificar con Grep antes de borrar).
- Agregar navegación: `ICollection<Peripheral> Peripherals`, `ICollection<License> Licenses`, `ICollection<SpecChangeLog> SpecChangeLogs`.

### Eliminar `Models/State.cs`
Código huérfano de un intento anterior de manejar estados — no está registrado en `MyDbContext`, no tiene `DbSet`, nada lo referencia. El nuevo diseño de `Estado` lo reemplaza.

### Nuevas entidades (`Models/*.cs`, namespace `GestorEquipos.Models`)

- **`PeripheralType`**: `Id`, `Name` (catálogo administrable, como `Ram`/`OSVersion`).
- **`Peripheral`**: `Id`, `DesktopId` (FK), `PeripheralTypeId` (FK), `Brand`, `Model`, `Serial` (nullable — a veces ilegible por desgaste), `Estado` (enum `Activo|Inactivo|Raes`, default Activo). Nota de diseño: se usa un `enum` de C# aquí (primera vez en el proyecto) en vez de tabla catálogo, porque son valores cerrados no administrables por el usuario — a diferencia de `PeripheralType`.
- **`PeripheralObservation`**: `Id`, `PeripheralId` (FK), `Date`, `Type` (enum `Reparacion|Cambio|BajaRAES`), `Description`. Regla de negocio en el servicio: `BajaRAES` → `Peripheral.Estado = Raes`; `Reparacion` → `Peripheral.Estado = Activo`; `Cambio` es solo bitácora (no cambia estado por defecto, pero el método del servicio permite pasar el nuevo estado explícitamente).
- **`License`**: `Id`, `DesktopId` (FK), `SoftwareType` (texto libre), `LicenseKey` (nullable), `NoLicense` (bool).
- **`SpecChangeLog`**: `Id`, `DesktopId` (FK), `FieldName` (texto — ej. "RamId", "Processor"), `OldValue`/`NewValue` (texto nullable, genérico para cualquier campo), `Date`, `ChangedByUserSystemId` (FK a `UserSystem`).

### Modificar `Models/Maintenance.cs`
Agregar `[Required] public int TechnicianUserSystemId { get; set; }` + navegación `UserSystem Technician`. FK obligatoria desde el inicio (BD vacía).

### `Models/DbContext/DbContext.cs` — actualizar `MyDbContext`
- Nuevos `DbSet<PeripheralType>`, `DbSet<Peripheral>`, `DbSet<PeripheralObservation>`, `DbSet<License>`, `DbSet<SpecChangeLog>`.
- `ToTable(...)` para cada uno, siguiendo la convención existente (nombre singular exacto).
- Relaciones nuevas en `OnModelCreating`, siguiendo el estilo explícito ya usado (`.HasOne().WithMany().HasForeignKey().OnDelete(...)`):
  - `Peripheral → Desktop`: Cascade (el periférico pertenece al equipo).
  - `Peripheral → PeripheralType`: Restrict.
  - `PeripheralObservation → Peripheral`: Cascade.
  - `License → Desktop`: Cascade.
  - `SpecChangeLog → Desktop`: Cascade.
  - `SpecChangeLog → ChangedByUserSystem`: Restrict.
  - `Maintenance → Technician (UserSystem)`: Restrict.
- `modelBuilder.Entity<Desktop>().Property(d => d.Estado).HasDefaultValue(true);` — explícito para que la migración incluya el default correctamente.
- Opcional pero recomendado: `HasIndex(pt => pt.Name).IsUnique()` en `PeripheralType`, consistente con el patrón de `Rol.Name`.

### Migración
Una sola migración cubre todo lo anterior (BD vacía → sin riesgo de backfill). El SDK de .NET está instalado en `C:\Program Files\dotnet\dotnet.exe` (no está en el PATH de las shells de este entorno, pero es utilizable con ruta completa o agregándolo al PATH de la sesión) — se genera con `dotnet ef migrations add` y se aplica con `dotnet ef database update` contra `AssetOps`. Ojo con el desfase de versión entre `Microsoft.EntityFrameworkCore.Tools` (10.0.7) y `EFCore.Design`/`SqlServer` (8.0.x) en el `.csproj` — si `dotnet ef` falla o se comporta raro, puede ser la causa.

## Seeding — `Services/Auth/AuthBootstrapper.cs`

- Renombrar el rol sembrado de `"Admin"` a `"Administrador"`.
- Sembrar el rol `"Auditor"` **incondicionalmente** (no depender de que `BootstrapAdmin:Email/Password` estén configurados — hoy el método hace `return` temprano si faltan, lo cual saltaría también el sembrado de roles/RAES; hay que reestructurar para que roles + RAES se siembren siempre, y solo la creación de la cuenta admin dependa de esas config keys).
- Sembrar Área "RAES" y Regional "RAES" (nuevas, dedicadas).
- Sembrar el usuario especial `Users` con `Email = "raes@system.local"` (constante reutilizable, ej. `AuthBootstrapper.RaesUserEmail`), `Name = "RAES"`, Área/Regional = las nuevas "RAES". **Sin `UserSystem`** (no necesita login).
- Cualquier servicio que necesite "el usuario RAES" lo busca por `AuthBootstrapper.RaesUserEmail`.

## Capa de servicios (`Gestor_Equipos.Services.*`, patrón interfaz + implementación como `IAuthService`/`AuthService`)

- **`IDesktopService`/`DesktopService`** (reescribir desde cero, el archivo actual no compila):
  - `GetAllAsync()` → `List<DesktopListViewModel>`, uniendo cada Desktop con su asignación más reciente (por `DateAsignation` descendente) para obtener Usuario/Área/Regional, más `Estado`.
  - `GetDetailAsync(id)` → ViewModel de hoja de vida completa: specs, asignación actual + historial completo, periféricos con su historial de observaciones, mantenimientos (con técnico), licencias, historial de cambios de specs.
  - `CreateAsync(vm)` — Admin.
  - `UpdateSpecsAsync(id, vm, changedByUserSystemId)` — por cada campo modificado, escribe una fila en `SpecChangeLog` antes de aplicar el cambio.
  - `DeactivateAsync(id)` — `Estado = false` + nueva `Asignation` hacia el usuario RAES (delegar en `IAsignationService.AssignAsync` para no duplicar lógica).
- **`IAsignationService`/`AsignationService`**: `AssignAsync(desktopId, userId)`, `GetHistoryAsync(desktopId)`.
- **`IPeripheralService`/`PeripheralService`**: `AddAsync(vm)`, `AddObservationAsync(peripheralId, vm)` (aplica la regla de negocio de Estado según el tipo de evento).
- **`IMaintenanceService`/`MaintenanceService`**: `CreateAsync(vm)`, `GetByDesktopAsync(desktopId)`. El dropdown de técnico en la UI se filtra a `UserSystem`s cuyo `Rol.Name == "Administrador"`.
- **`ILicenseService`/`LicenseService`**: `AddAsync(vm)` (si `NoLicense == true`, forzar `LicenseKey = null` en el servidor).
- **`IUserAdminService`/`UserAdminService`**: `CreateUserAsync(vm)`, `CreateLoginAsync(userId, username, password, rolId)` (hashea con `IPasswordHasher<UserSystem>`, valida username/email duplicados antes de insertar dado que la BD ya tiene índices únicos).

Registrar todos los servicios nuevos en `Program.cs` vía `AddScoped<IX, X>()`.

## Controladores y autorización

- **`DesktopController`** (nuevo): `Index()` `[Authorize]` (lista con filtro opcional de Estado), `Details(id)` `[Authorize]`, `Create()` GET/POST `[Authorize(Roles="Administrador")]`, `Edit(id)` GET/POST `[Authorize(Roles="Administrador")]`, `Deactivate(id)` POST `[Authorize(Roles="Administrador")]`, `Assign(desktopId)` GET/POST `[Authorize(Roles="Administrador")]`.
- **`PeripheralController`** (nuevo): `Create(desktopId)`, `AddObservation(peripheralId)` — ambos `[Authorize(Roles="Administrador")]`.
- **`MaintenanceController`** (nuevo): `Create(desktopId)` `[Authorize(Roles="Administrador")]`.
- **`LicenseController`** (nuevo): `Create(desktopId)` `[Authorize(Roles="Administrador")]`.
- **`PeripheralTypeController`** (nuevo, mínimo viable): `Index()`/`Create()` `[Authorize(Roles="Administrador")]` — necesario porque el catálogo de tipos de periférico es administrable.
- **`UserAdminController`** (nuevo — **no** llamarlo `UserController`: ese nombre de archivo ya existe y contiene la clase `LoginController`, sería confuso): `Index()`, `Create()` GET/POST, todo `[Authorize(Roles="Administrador")]`.
- **`HomeController`**: eliminar la acción `Register()`.

Los demás catálogos existentes (`Ram`, `OSVersion`, `Remote`, `MaintenanceType`, `Area`, `Regional`) quedan fuera de alcance de UI administrable por ahora (se mantienen sembrados/editables solo por BD), ya que no fue parte de lo solicitado.

## Vistas

Seguir las convenciones Bootstrap 5 + `asp-for`/`asp-validation-for` de `Views/Login/Index.cshtml`, layout `_Layout.cshtml` para todo lo autenticado. Agregar enlaces de navegación "Equipos" (todos) y "Usuarios" (solo visible con `User.IsInRole("Administrador")`).

- `Views/Desktop/Index.cshtml` — tabla de 6 columnas + filtro por Estado, cada fila enlaza a `Details`. Botón "Nuevo equipo" solo Admin.
- `Views/Desktop/Details.cshtml` — hoja de vida: specs, asignación actual + historial, periféricos (cada uno con su historial de observaciones + botón Admin "Registrar novedad"), historial de mantenimiento (con técnico), licencias, historial de cambios de especificaciones. Botones Editar/Desactivar solo Admin.
- `Views/Desktop/Create.cshtml` / `Edit.cshtml` — dropdowns para OSVersion/Ram/Remote existentes.
- `Views/Peripheral/Create.cshtml`, `Views/Peripheral/AddObservation.cshtml`.
- `Views/Maintenance/Create.cshtml` — dropdown de técnico filtrado a rol Administrador.
- `Views/License/Create.cshtml` — checkbox "Sin licencia" que deshabilita/limpia el campo de clave.
- `Views/PeripheralType/Index.cshtml` / `Create.cshtml`.
- `Views/UserAdmin/Index.cshtml` / `Create.cshtml` — formulario combinado Users + UserSystem + Rol.
- **Eliminar** `Views/Home/Register.cshtml`.

Nuevos ViewModels en `Models/ViewModels/<Área>/` (siguiendo el patrón de `DesktopListViewModel`): agregar `AreaName` a `DesktopListViewModel`; crear `DesktopDetailViewModel`, `DesktopCreateViewModel`/`DesktopEditViewModel`, `PeripheralCreateViewModel`, `PeripheralObservationCreateViewModel`, `MaintenanceCreateViewModel`, `LicenseCreateViewModel`, `UserCreateViewModel`.

## Fases de desarrollo

### Fase 0 — Documentación del plan y proyecto de pruebas
- Guardar este documento como `PLAN_DESARROLLO.md` en la raíz del repositorio, para que quede versionado junto al código y no solo en la carpeta local de planes de Claude Code.
- Crear el proyecto de pruebas `GestorEquipos.Tests` (xUnit), agregado a `Gestor_Equipos.sln`, con referencia a `GestorEquipos.csproj`, el paquete `Microsoft.EntityFrameworkCore.InMemory` (para probar los servicios contra un `MyDbContext` en memoria sin depender de SQL Server real) y `coverlet.collector` para medir cobertura.
- Crear `MANUAL_TESTS.md` en la raíz del repo: checklist de pruebas de uso manuales paso a paso (se va llenando por fase, no solo al final — ver sección "Pruebas" abajo).
- **Entregable**: solución con 2 proyectos (`GestorEquipos`, `GestorEquipos.Tests`), `dotnet test` corre (aunque sin tests todavía), `PLAN_DESARROLLO.md` y `MANUAL_TESTS.md` versionados en el repo.

### Fase 1 — Modelo de datos + migración (bloquea todo lo demás)
- Modificar `Models/Desktop.cs` (campo `Estado`, quitar nav muerta `User`, agregar nuevas colecciones de navegación).
- Eliminar `Models/State.cs` (código huérfano).
- Crear `Models/PeripheralType.cs`, `Models/Peripheral.cs`, `Models/PeripheralObservation.cs`, `Models/License.cs`, `Models/SpecChangeLog.cs`.
- Modificar `Models/Maintenance.cs` (FK `TechnicianUserSystemId`).
- Actualizar `Models/DbContext/DbContext.cs` (`DbSet`s, `ToTable`, relaciones, default de `Estado`).
- Actualizar `Services/Auth/AuthBootstrapper.cs`: renombrar rol "Admin"→"Administrador", sembrar "Auditor" incondicionalmente, sembrar Área/Regional "RAES" y el usuario especial RAES (sin `UserSystem`).
- Generar y aplicar la migración (`dotnet ef migrations add` + `dotnet ef database update` contra `AssetOps`).
- **Entregable**: proyecto compila, la app arranca, se siembran roles Administrador/Auditor y el usuario RAES al iniciar.

### Fase 2 — Capa de servicios
- Reescribir `Services/DesktopService.cs` desde cero con interfaz `IDesktopService` (hoy no compila). Empezar por `GetAllAsync`/`GetDetailAsync` (son las de mayor valor visible).
- Crear `IAsignationService`/`AsignationService`.
- Crear `IPeripheralService`/`PeripheralService`, `IMaintenanceService`/`MaintenanceService`, `ILicenseService`/`LicenseService`.
- Crear `IUserAdminService`/`UserAdminService`.
- Registrar todos los servicios nuevos en `Program.cs`.
- Escribir pruebas unitarias en `GestorEquipos.Tests` para cada servicio nuevo/reescrito, usando `MyDbContext` con el proveedor InMemory (una BD nueva por test). Casos mínimos por servicio: `DesktopService` (asignación más reciente se refleja en `GetAllAsync`, `DeactivateAsync` pone `Estado=false` y crea la `Asignation` hacia RAES, `UpdateSpecsAsync` escribe `SpecChangeLog`), `PeripheralService` (reglas de transición de Estado según tipo de evento), `MaintenanceService`, `LicenseService` (`NoLicense=true` fuerza `LicenseKey=null`), `UserAdminService` (rechaza email/username duplicado).
- **Entregable**: capa de servicios completa, compilando, con ≥80% de cobertura de línea sobre el código nuevo de `Services/` (medido con `dotnet test --collect:"XPlat Code Coverage"`).

### Fase 3 — Controladores y autorización
- `DesktopController`: `Index`/`Details` primero (solo lectura, feedback rápido), luego `Create`/`Edit`/`Deactivate`/`Assign` (Admin).
- `PeripheralController`, `MaintenanceController`, `LicenseController`, `PeripheralTypeController` (todos Admin-only para escritura).
- `UserAdminController` (Admin-only).
- Eliminar la acción `Register()` de `HomeController`.
- Escribir pruebas de autorización por controlador (verificar que las acciones Admin-only tienen `[Authorize(Roles="Administrador")]` y las de lectura `[Authorize]`, vía reflexión sobre atributos o pruebas de integración ligeras con `WebApplicationFactory`).
- Actualizar `MANUAL_TESTS.md` con los pasos para probar cada endpoint nuevo manualmente (URL, rol esperado, resultado esperado) a medida que se implementa cada controlador.
- **Entregable**: todas las rutas responden con los códigos de autorización correctos (verificado por prueba automatizada y manualmente), aunque las vistas aún sean básicas.

### Fase 4 — Vistas
- `Views/Desktop/Index.cshtml` (lista de 6 columnas + filtro por Estado) y `Views/Desktop/Details.cshtml` (hoja de vida completa) — entregable principal del proyecto.
- `Views/Desktop/Create.cshtml` / `Edit.cshtml`.
- `Views/Peripheral/*`, `Views/Maintenance/Create.cshtml`, `Views/License/Create.cshtml`, `Views/PeripheralType/*`.
- `Views/UserAdmin/Index.cshtml` / `Create.cshtml`.
- Eliminar `Views/Home/Register.cshtml`; actualizar navegación en `_Layout.cshtml` (enlaces "Equipos" y "Usuarios", este último solo visible para Administrador).
- Completar `MANUAL_TESTS.md` con los flujos de usuario completos (no solo por endpoint) para Administrador y Auditor.
- **Entregable**: flujo completo usable desde el navegador para ambos roles, con checklist manual completo en el repo.

### Fase 5 — Pulido y verificación end-to-end
- Verificar `[ValidateAntiForgeryToken]` en todas las acciones de mutación.
- Verificar visibilidad de navegación según rol (`User.IsInRole("Administrador")`).
- Correr `dotnet test` con cobertura y confirmar que `Services/` está en ≥80% de líneas cubiertas; rellenar huecos de pruebas si hace falta.
- Ejecutar manualmente el checklist completo de `MANUAL_TESTS.md` con ambos roles y dejar constancia (marcar cada ítem) de que pasó.

## Pruebas — estrategia

- **Automatizadas**: proyecto `GestorEquipos.Tests` (xUnit) creado en Fase 0. Cobertura objetivo: **≥80% de líneas en `Services/`** (la lógica de negocio nueva: reglas de RAES, historial de specs, transiciones de estado de periféricos, validaciones de usuario/licencia), usando EF Core InMemory para no depender de SQL Server real. Los controladores se cubren con pruebas más livianas centradas en autorización, no en cobertura exhaustiva de cada acción. No se persigue 80% sobre el proyecto completo (vistas `.cshtml` y `Program.cs` no aportan cobertura significativa vía xUnit) — el objetivo aplica a la capa de servicios, que es donde vive la lógica de negocio.
- **Manuales**: `MANUAL_TESTS.md` en la raíz del repo, con pasos concretos (acción → resultado esperado) para cada flujo, agrupados por fase y por rol. Se va completando incrementalmente durante Fases 3-4 y se ejecuta de punta a punta en Fase 5.

## Verificación

- Compilar el proyecto (`dotnet build`) tras cada fase — especialmente tras reescribir `DesktopService.cs`, que hoy no compila.
- Aplicar la migración contra la BD `AssetOps` (vacía) y confirmar que arranca la app sin errores, que se siembra el usuario RAES + roles Administrador/Auditor.
- Flujo manual con rol Administrador: crear equipo → asignar a un usuario real → agregar periférico → registrar mantenimiento con técnico → cambiar una spec (verificar que aparece en `SpecChangeLog`) → desactivar equipo (verificar `Estado = false`, nueva fila `Asignation` hacia RAES, aparece "RAES" en columnas Usuario/Área/Regional de la lista).
- Flujo manual con rol Auditor: confirmar que puede ver la lista y hojas de vida, pero no ve/no puede acceder a botones ni acciones de creación/edición/desactivación (intentar acceder directamente a una URL de Admin debe devolver 403/AccessDenied).
- Confirmar que el filtro de Estado en `Desktop/Index` funciona (todos / solo activos / solo inactivos-RAES).
