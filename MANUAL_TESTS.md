# Pruebas manuales — Gestor de Equipos

Checklist de pruebas de uso manual. Se completa de forma incremental durante las Fases 3-4 (a medida que cada endpoint/vista queda implementado) y se ejecuta de punta a punta en la Fase 5. Marcar `[x]` cuando el paso pasa.

Convención: cada ítem indica **Acción → Resultado esperado**. Ejecutar con dos sesiones/usuarios distintos: uno con rol **Administrador** y otro con rol **Auditor** (crear el usuario Auditor desde el panel de administración una vez esté disponible).

## Fase 1 — Arranque y seeding

- [x] Aplicar la migración contra `AssetOps` y arrancar la app → no hay errores en consola. (Verificado 2026-08-04: `dotnet ef database update` aplicó `AddRaesLifecycleTracking` sin errores; `dotnet run` arrancó y escuchó en `http://localhost:5299`.)
- [x] Revisar la tabla `Rol` → existen exactamente `Administrador` y `Auditor`. (Verificado por log de seeding: MERGE insertó ambos roles.)
- [x] Revisar la tabla `Users` → existe un usuario con `Email = raes@system.local`, `Name = RAES`. (Verificado por log de seeding: INSERT INTO Users con esos valores.)
- [x] Revisar que el usuario RAES **no** tiene fila asociada en `UserSystem` (no puede iniciar sesión). (Confirmado: `EnsureRaesUserAsync` nunca crea `UserSystem`; no hay INSERT INTO UserSystem en el log de arranque.)
- [x] Revisar las tablas `Area`/`Regional` → existe una fila "RAES" en cada una. (Verificado por log de seeding: INSERT INTO Area/Regional con Name='RAES'.)

**Nota (resuelta 2026-08-04)**: se configuraron `BootstrapAdmin:Email`/`BootstrapAdmin:Password` vía `dotnet user-secrets` (no committeados) para poder iniciar sesión localmente. Credenciales de prueba: `admin@gestorequipos.local`. Cualquier desarrollador que clone el repo debe correr `dotnet user-secrets set "BootstrapAdmin:Email" "..."` y `dotnet user-secrets set "BootstrapAdmin:Password" "..."` (con comillas simples en PowerShell si la contraseña tiene `$`, para evitar interpolación) antes del primer arranque.

## Fase 3 — Autorización por endpoint (probar sin vista completa, solo código de respuesta)

- [x] `GET /Desktop/Index` sin sesión iniciada → redirige a `/Login/Index`. (Verificado 2026-08-04: `Invoke-WebRequest` anónimo a `/Desktop/Index`, `/UserAdmin/Index` y `/Home/Index` terminó en `Login/Index?ReturnUrl=...` con 200 tras seguir el redirect. Confirma que el fallback `RequireAuthenticatedUser` sigue protegiendo todo lo nuevo.)
- [x] Atributos `[Authorize]`/`[Authorize(Roles="Administrador")]` correctos en todos los controladores nuevos y en `HomeController`/`LoginController`. (Verificado por 15 pruebas automatizadas de reflexión en `GestorEquipos.Tests/Controllers/AuthorizationAttributeTests.cs`: `DesktopController` tiene lectura sin restricción de rol y escritura restringida a Administrador; `Peripheral`/`Maintenance`/`License`/`PeripheralType`/`UserAdmin` Controllers restringidos a Administrador a nivel de clase; `HomeController.Register` ya no existe; `LoginController` sigue `[AllowAnonymous]` con `Logout` requiriendo sesión.)
- [x] `GET /Desktop/Index` con sesión Auditor → 200 OK. (Verificado 2026-08-04 vía `Invoke-WebRequest` con sesión autenticada.)
- [x] `GET /Desktop/Index` con sesión Administrador → 200 OK. (Verificado 2026-08-04.)
- [x] `GET /Desktop/Create` con sesión Auditor → redirige a `AccessDenied`. (Verificado 2026-08-04: `FinalUri=.../Login/AccessDenied?ReturnUrl=/Desktop/Create`.)
- [x] `GET /Desktop/Create` con sesión Administrador → 200 OK. (Verificado indirectamente: el formulario se usó exitosamente para crear PC-TEST-001.)
- [x] `GET /UserAdmin/Index` con sesión Auditor → redirige a `AccessDenied`. (Verificado 2026-08-04: `FinalUri=.../Login/AccessDenied?ReturnUrl=/UserAdmin/Index`.)
- [x] `GET /UserAdmin/Index` con sesión Administrador → 200 OK. (Verificado 2026-08-04.)
- [x] Botones Admin-only (Editar, Asignar, "Enviar a RAES", nav "Usuarios") no se muestran a un usuario Auditor en `Desktop/Details`/`_Layout`. (Verificado 2026-08-04: los 3 marcadores están ausentes del HTML devuelto a la sesión Auditor.)

## Fase 4-5 — Flujo completo (rol Administrador)

Ejecutado de punta a punta 2026-08-04 vía `Invoke-WebRequest` con manejo de antiforgery token contra una instancia real de la app y `AssetOps` (con catálogos base sembrados manualmente por SQL: 1 OSVersion, 2 Ram, Area/Regional no-RAES, 2 MaintenanceType, ya que esos catálogos no tienen UI de administración por decisión de alcance).

- [x] Iniciar sesión como Administrador.
- [x] Crear un equipo nuevo (`Desktop/Create`) con specs completas → aparece en `Desktop/Index` (PC-TEST-001, redirigió a `Desktop/Details/1`).
- [x] Asignar el equipo a un usuario real → `Desktop/Assign` respondió 200 y redirigió a `Details`.
- [x] Agregar un periférico al equipo (Mouse, Logitech M100, con serial) → creado sin error, referenciado luego en la observación.
- [x] Registrar una observación de "Reparación" sobre ese periférico → guardada sin error.
- [ ] Registrar una observación de "Baja RAES" sobre el periférico → cubierto por prueba unitaria (`PeripheralServiceTests.AddObservationAsync_BajaRAES_SetsEstadoRaes`), no ejecutado manualmente vía HTTP en esta pasada.
- [x] Registrar un mantenimiento (Preventivo) con un técnico (la propia cuenta Administrador, válida como técnico) y una observación → guardado sin error.
- [x] Editar una especificación del equipo (RAM 8GB→16GB) → confirmado: la respuesta de `Desktop/Details` tras el `Edit` contiene "RAM" y "16GB" en el historial de cambios de especificaciones.
- [x] Agregar una licencia con clave (Office 2016 / AAAAA-BBBBB-CCCCC) → guardada sin error.
- [x] Agregar una licencia marcada "Sin licencia" (Windows 10) → guardada sin error.
- [x] Desactivar el equipo (`Desktop/Deactivate`) → confirmado: `Estado` pasa a "Inactivo", "RAES" aparece en el HTML de asignaciones, y `Desktop/Index` sin filtro muestra la fila con Usuario=RAES y Estado=Inactivo; con `?estado=true` PC-TEST-001 ya no aparece.
- [x] Confirmar que **no** hay forma de reactivar el equipo desde la UI. (Confirmado: tras desactivar, el botón "Enviar a RAES" desaparece de `Details` y no existe ninguna acción de reactivación en `DesktopController`.)
- [x] Crear un usuario nuevo desde `UserAdmin/Create` (persona + cuenta de login + rol) → creado "Test Usuario" con rol Auditor; pudo iniciar sesión exitosamente con esas credenciales (ver sección Auditor abajo).
- [ ] Intentar crear un usuario con un correo/usuario ya existente → mensaje de validación, no se duplica. Cubierto por pruebas unitarias (`UserAdminServiceTests.CreateUserAsync_ThrowsOnDuplicateEmail`, `CreateLoginAsync_ThrowsOnDuplicateUsername`); no ejecutado manualmente vía formulario en esta pasada.

## Fase 4-5 — Flujo completo (rol Auditor)

Ejecutado 2026-08-04 con el usuario "Test Usuario" (test.usuario@empresa.com, rol Auditor) creado en la sección anterior.

- [x] Iniciar sesión como Auditor.
- [x] Ver `Desktop/Index` → 200 OK.
- [x] Filtrar por Estado (Activo / Inactivo-RAES) → verificado con el Administrador (`?estado=true` excluye correctamente el equipo inactivo); el mecanismo es el mismo para ambos roles.
- [x] Abrir la hoja de vida de un equipo (`Desktop/Details`) → 200 OK, sin botones "Editar"/"Asignar" visibles.
- [x] Intentar acceder directamente a una URL de escritura (`/Desktop/Create`, `/UserAdmin/Index`) → ambas redirigen a `Login/AccessDenied`.
- [x] Confirmar que no aparece el enlace "Usuarios" en la navegación. (Verificado: `UserAdmin` no aparece en el HTML devuelto a la sesión Auditor.)

## Cobertura automatizada

- [x] `dotnet test --collect:"XPlat Code Coverage"` corre sin fallos. (Verificado 2026-08-04: 34/34 tests correctos.)
- [x] El reporte de cobertura muestra ≥80% de líneas cubiertas en `Services/` (ver sección "Pruebas — estrategia" en `PLAN_DESARROLLO.md`). (Verificado 2026-08-04: 91.73% agregado — AsignationService/DesktopService/LicenseService/MaintenanceService/UserAdminService al 100%, PeripheralService 97.3%, AuthBootstrapper 97%. `AuthService.cs`/`IAuthService.cs` pre-existentes y sin tocar quedan en 0% pero son solo 39 líneas del total y no bajan el agregado de la meta.)
