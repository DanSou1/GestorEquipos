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
- [x] Crear un usuario nuevo desde `UserAdmin/Create` (persona + cuenta de login + rol) → creado "Test Usuario" con rol Auditor; pudo iniciar sesión exitosamente con esas credenciales (ver sección Auditor abajo). **Superado 2026-08-10**: `UserAdmin/Create` ya no crea cuentas de login (ver Fase 6) — ese flujo ahora vive en `AccessAccount/Create`.
- [ ] Intentar crear un usuario con un correo/usuario ya existente → mensaje de validación, no se duplica. Cubierto por pruebas unitarias (`UserAdminServiceTests.CreateUserAsync_ThrowsOnDuplicateEmail`, `CreateLoginAsync_ThrowsOnDuplicateUsername`); no ejecutado manualmente vía formulario en esta pasada.

## Fase 4-5 — Flujo completo (rol Auditor)

Ejecutado 2026-08-04 con el usuario "Test Usuario" (test.usuario@empresa.com, rol Auditor) creado en la sección anterior.

- [x] Iniciar sesión como Auditor.
- [x] Ver `Desktop/Index` → 200 OK.
- [x] Filtrar por Estado (Activo / Inactivo-RAES) → verificado con el Administrador (`?estado=true` excluye correctamente el equipo inactivo); el mecanismo es el mismo para ambos roles.
- [x] Abrir la hoja de vida de un equipo (`Desktop/Details`) → 200 OK, sin botones "Editar"/"Asignar" visibles.
- [x] Intentar acceder directamente a una URL de escritura (`/Desktop/Create`, `/UserAdmin/Index`) → ambas redirigen a `Login/AccessDenied`.
- [x] Confirmar que no aparece el enlace "Usuarios" en la navegación. (Verificado: `UserAdmin` no aparece en el HTML devuelto a la sesión Auditor.)

## Fase 6 — Rediseño Usuarios / Área-Regional / Cuentas de acceso / validaciones de asignación (2026-08-10)

Ejecutado de punta a punta vía navegador (Chrome, sesión Administrador `administrador@exro.co`) contra la instancia local con `AssetOps`.

- [x] `UserAdmin/Create` ya no muestra la sección "Cuenta de acceso" (solo Name/LastName/Email/EmailTeams/Área/Regional) → creado "Carlos Ramirez" sin generar fila en `UserSystem`.
- [x] `UserAdmin/Index` excluye por defecto a las personas con cuenta de acceso (solo equipos-registro) → total pasó de mostrar la cuenta bootstrap a 0 tras el filtro.
- [x] Asignar un equipo a un usuario de equipos-registro y luego "Eliminar" a ese usuario (`UserAdmin/Deactivate`) → el usuario desaparece del listado activo, y el equipo pasa a mostrar "Disponible" en `Desktop/Index` y `Desktop/Details`, conservando el Área/Regional original del usuario eliminado.
- [x] `UserAdmin/Details` de un usuario inactivo → banner "Usuario inactivo — Disponible desde `<fecha>`", "Equipos asignados actualmente" vacío, historial de asignaciones intacto.
- [x] Checkbox "Mostrar inactivos" en `UserAdmin/Index` → reaparece el usuario eliminado con badge "Inactivo".
- [x] `Location/Index` (Áreas y Regionales) → CRUD completo probado: crear área ("Contabilidad"), intentar eliminar un área en uso (bloqueado con banner de error), eliminar un área sin uso (exitoso).
- [x] `AccessAccount/Create` (Cuentas de acceso) → creada "Laura Torres" (persona + login + rol Auditor en un solo formulario); aparece en `AccessAccount/Index` y **no** aparece en `UserAdmin/Index`.
- [x] `Desktop/Assign` — reasignar un equipo con dueño activo a otra persona → bloqueado con mensaje "Este equipo ya está asignado a `<nombre>`. Debe quedar Disponible antes de asignarlo a otra persona.", formulario re-renderizado con el usuario elegido aún seleccionado.
- [x] `Desktop/Assign` — reasignar al mismo usuario que ya lo tiene actualmente → permitido.
- [x] `Desktop/Assign` — asignar un equipo ya "Disponible" (dueño anterior eliminado) a un tercero → permitido.
- [x] "Enviar a RAES" sobre un equipo con dueño activo → verificado que sigue funcionando de punta a punta tras agregar las validaciones de `AssignAsync` (regresión encontrada y corregida durante esta prueba: el *bypass* de RAES inicialmente solo cubría el chequeo de "equipo dado de baja", no el de "ya asignado a otro usuario activo" — ver `AsignationService.AssignAsync`).

## Fase 7 — Edición de usuarios en la tabla Usuarios (2026-08-10)

- [x] `UserAdmin/Index` → enlace "Editar" visible tanto para usuarios activos como inactivos.
- [x] `UserAdmin/Edit` (usuario inactivo) → formulario precargado con sus datos actuales; cambiar Área y guardar → redirige a `Details`, el cambio se refleja, y el usuario sigue mostrando el banner "Usuario inactivo" (editar no lo reactiva).
- [x] `UserAdmin/Details` → botón "Editar" junto al título, funciona igual que desde el listado.
- [x] Intentar guardar un correo ya usado por otra persona → bloqueado con "Ya existe un usuario con ese correo.", formulario re-renderizado con los valores digitados.

## Fase 8 — Edición de cuentas de acceso (cambio de contraseña)

Ejecutado 2026-08-10 vía navegador (Chrome, sesión Administrador `administrador@exro.co`) contra la instancia local, editando la cuenta de "Laura Torres".

- [x] `AccessAccount/Index` → enlace "Editar" visible por cada fila.
- [x] `AccessAccount/Edit` → formulario precargado con datos personales, Username y Rol actuales; los campos de contraseña aparecen vacíos.
- [x] Editar solo datos personales (Name → "Laura Maria") dejando los tres campos de contraseña vacíos → guardó sin pedir contraseña de administrador y sin cambiar el hash; redirigió a `Index` mostrando "Laura Maria Torres".
- [x] Cambiar la contraseña ingresando "Nueva contraseña" + "Confirmar nueva contraseña" (coincidentes, `NuevaClave123!`) + la contraseña real del Administrador (`colombia1`) → guardó correctamente; cerrar sesión e iniciar sesión con `laura.torres@empresa.com` / `NuevaClave123!` → login exitoso, dashboard de Auditor.
- [x] Cambiar la contraseña con la contraseña de administrador incorrecta → bloqueado con "Contraseña de administrador incorrecta.", formulario re-renderizado sin persistir el cambio (confirmado reintentando después con la clave correcta desde el mismo estado).
- [ ] Cambiar la contraseña con "Confirmar nueva contraseña" que NO coincide con "Nueva contraseña" → bloqueado por validación del formulario (`Compare`), sin llegar al servidor. No ejecutado manualmente en esta pasada.
- [ ] Intentar guardar un Username o correo ya usado por otra cuenta → cubierto por pruebas unitarias (`UpdateAccountAsync_ThrowsOnDuplicateUsername`, `UpdateAccountAsync_ThrowsOnDuplicateEmail`); no ejecutado manualmente vía formulario en esta pasada.

## Fase 9 — Eliminación de hoja de vida (2026-08-11)

- [ ] `Desktop/Details` de un equipo Activo, sesión Administrador → botón "Eliminar" visible junto a "Editar"/"Asignar"/"Enviar a RAES".
- [ ] `Desktop/Details` de un equipo Inactivo (enviado a RAES), sesión Administrador → botón "Eliminar" sigue visible (a diferencia de "Enviar a RAES", que desaparece).
- [ ] `Desktop/Details`, sesión Auditor → botón "Eliminar" ausente del HTML.
- [ ] Click en "Eliminar" → se abre el modal de confirmación "¿Desea eliminar esta hoja de vida?" sin recargar la página; "Cancelar" cierra el modal sin enviar nada.
- [ ] Confirmar eliminación de un equipo con asignación activa, un periférico con observación, un mantenimiento, una licencia y un cambio de especificación registrado → redirige a `Desktop/Index`, el equipo ya no aparece en el listado, sin error 500 (verifica que borrar `Asignation` antes que `Desktop` evita la violación de FK `DeleteBehavior.Restrict` contra SQL Server real — único caso no cubierto por las pruebas unitarias, que usan el proveedor InMemory).
- [ ] Confirmar por consulta SQL directa contra `AssetOps` que las filas dependientes (`Peripheral`, `PeripheralObservation`, `Maintenance`, `License`, `SpecChangeLog`, `Asignation` para ese `DesktopId`) desaparecieron.

## Fase 10 — Acceso remoto: tipo de conexión y credenciales (2026-08-11)

- [ ] `Desktop/Create`, sesión Administrador → dropdown "Acceso remoto" muestra "-- Ninguno --", "+ Crear nuevo acceso remoto" y cualquier `Remote` ya sembrado con una etiqueta legible (tipo + IP:Puerto o descripción de aplicativo).
- [ ] Elegir "+ Crear nuevo acceso remoto" → aparece el panel con el selector de tipo de conexión; el resto de campos permanece oculto hasta elegir un tipo.
- [ ] Elegir tipo "Aplicativo" → solo se muestra el campo de descripción; guardar sin ese campo → error de validación ("Ingresa el nombre del aplicativo o la URL."), sin llegar al servidor. Completarlo y guardar → el equipo se crea con ese `Remote`.
- [ ] Elegir tipo "Escritorio remoto de Windows" → se muestran IP/Puerto/Usuario/Clave; guardar dejando alguno vacío → error de validación específico por campo. Completar los 4 y guardar → el equipo se crea con ese `Remote`.
- [ ] Botón "Mostrar"/"Ocultar" junto al campo de Clave alterna el input entre `type="password"` y texto plano sin recargar la página.
- [ ] IP con formato inválido (ej. "abc") → error de validación antes de enviar el formulario.
- [ ] Reutilizar un `Remote` ya existente desde el dropdown (sin elegir "Crear nuevo") → el equipo se crea/edita apuntando a esa misma fila, sin crear una nueva.
- [ ] `Desktop/Edit` de un equipo con acceso remoto ya asignado → el dropdown queda preseleccionado en la fila correspondiente; cambiar a "Crear nuevo acceso remoto" y guardar → el equipo queda apuntando al `Remote` nuevo (el anterior no se borra ni se edita).
- [ ] Tras el cambio anterior, `Details` → historial de cambios de especificaciones muestra una entrada "Acceso remoto" con un resumen legible (ej. "RDP 10.0.0.5:3389 (PC01\\usuario)" o "Aplicativo: SAP GUI"), no un Id numérico.
- [ ] `Desktop/Details` de un equipo con acceso remoto, sesión Administrador → sección "Acceso remoto" visible con IP/Puerto/Usuario (o descripción de aplicativo); la clave permanece oculta hasta pulsar "mostrar clave".
- [ ] `Desktop/Details` del mismo equipo, sesión Auditor → sección "Acceso remoto" completamente ausente del HTML devuelto (no solo la clave — todo el bloque).
- [ ] Descargar el PDF (`Desktop/DownloadPdf`) como Administrador → incluye la línea "Acceso remoto" con los datos completos (incluida la clave en texto plano).
- [ ] Descargar el PDF del mismo equipo como Auditor → la línea "Acceso remoto" no aparece en el PDF.
- [ ] `dotnet ef database update` contra `AssetOps` real → la migración `AddRemoteConnectionTypeAndCredentials` aplica sin fallos; si ya existían filas `Remote` (IP/Puerto sembrados por SQL), quedan con `ConnectionType = EscritorioRemotoWindows` y sin violar el CHECK constraint (agregado con `NOCHECK`, no valida datos históricos).
- [ ] Intentar insertar directamente por SQL una fila `Remote` con `ConnectionType = 1` (RDP) sin `Username`/`Password` → el INSERT falla por el CHECK constraint `CK_Remote_ConnectionTypeFields` (confirma que se aplica a datos nuevos aunque no a los históricos).

## Fase 11 — Catálogo de tipos de periférico (Editar/Eliminar) (2026-08-11)

Ejecutado vía navegador (Chrome, sesión Administrador `administrador@exro.co`) contra la instancia local con `AssetOps`.

- [x] `PeripheralType/Index`, sesión Administrador → tabla con 24 tipos sembrados por la migración `SeedPeripheralTypeCatalog` (Teclado, Mouse, Monitor, etc. — más "Diadema", que ya existía antes de esta fase), cada fila con enlaces "Editar"/"Eliminar".
- [x] `PeripheralType/Edit` → renombrar un tipo existente ("Control remoto" → "Control remoto TV") → redirige a `Index`, el cambio se refleja en la tabla.
- [ ] Intentar renombrar un tipo a un nombre ya usado por otro tipo → cubierto por prueba unitaria (`PeripheralTypeServiceTests.UpdateAsync_ThrowsOnDuplicateName`); no ejecutado manualmente vía formulario en esta pasada.
- [ ] `PeripheralType/Delete` sobre un tipo **en uso** por al menos un `Peripheral` registrado → cubierto por prueba unitaria (`PeripheralTypeServiceTests.DeleteAsync_ThrowsWhenPeripheralsReferenceType`); no ejecutado manualmente en esta pasada porque la tabla `Peripheral` del entorno de desarrollo estaba vacía (los registros de prueba anteriores se habían eliminado en cascada durante las pruebas de la Fase 9).
- [x] `PeripheralType/Delete` sobre un tipo sin uso, con contraseña de administrador **incorrecta** → bloqueado con banner "Contraseña de administrador incorrecta.", el tipo ("Cable de red") sigue en la tabla.
- [x] `PeripheralType/Delete` sobre un tipo sin uso, con contraseña correcta → el tipo ("Escáner", luego "Mouse") desaparece de `Index` de inmediato.
- [ ] Sesión Auditor → `PeripheralType/Index` y todas las acciones de escritura siguen restringidas a Administrador (sin cambios en esta fase: el controlador conserva `[Authorize(Roles="Administrador")]` a nivel de clase; no re-verificado manualmente en esta pasada, pero `AuthorizationAttributeTests` sigue cubriendo la aserción).

## Fase 12 — Editar/Eliminar periférico individual (2026-08-11)

Ejecutado vía navegador (Chrome, sesión Administrador `administrador@exro.co`) contra la instancia local con `AssetOps`, sobre el equipo "GPena".

- [x] `Peripheral/Create` → agregar un periférico (Mouse, Logitech M170) → aparece en `Desktop/Details` con enlaces "Editar"/"Eliminar".
- [x] `Peripheral/Edit` → cambiar el modelo (M170 → M185) → redirige a `Desktop/Details`, el cambio se refleja de inmediato.
- [x] `Peripheral/Delete` con contraseña de administrador **incorrecta** → bloqueado con banner "Contraseña de administrador incorrecta." en `Desktop/Details` (requirió agregar `ViewBag.Error`/banner a `DesktopController.Details`/`Views/Desktop/Details.cshtml`, que no existían — bug encontrado y corregido durante esta prueba), el periférico sigue en la lista.
- [x] `Peripheral/Delete` con contraseña correcta → el periférico desaparece de `Desktop/Details` ("Sin periféricos registrados.").
- [ ] Sesión Auditor → enlaces "Editar"/"Eliminar" de periféricos ausentes (sin cambios en esta fase: siguen gateados por `User.IsInRole("Administrador")` en la vista, igual que "Registrar novedad"); no re-verificado manualmente en esta pasada.

## Fase 13 — Historial de tenencia y reasignación de periféricos (2026-08-11)

Ejecutado vía navegador (Chrome, sesión Administrador `administrador@exro.co`) contra la instancia local con `AssetOps`, sobre el equipo "GPena" (dueño activo: Gabriela Peña).

- [x] `Peripheral/Create` sobre un equipo con dueño activo → el periférico nace ya asignado a esa persona (auto-asignación), visible de inmediato en `Peripheral/Details` → "Asignación actual".
- [x] `Peripheral/Details` → tarjetas "Datos"/"Asignación actual"/"Historial de asignaciones"/"Observaciones" renderizan correctamente, con enlace de vuelta a `Desktop/Details`.
- [x] `Peripheral/Reassign` a un usuario activo distinto mientras el periférico ya está asignado a alguien activo → bloqueado con "Este periférico ya está asignado a Gabriela Peña. Debe quedar Disponible antes de asignarlo a otra persona." (mismo mensaje que `Desktop/Assign`), dropdown re-renderizado con la selección previa.
- [x] `Peripheral/Reassign` al mismo usuario que ya lo tiene actualmente → permitido, agrega una fila nueva al historial sin borrar la anterior.
- [x] `Desktop/Details` → cada periférico muestra enlaces "Ver detalle" y "Reasignar" (Admin).
- [ ] `Peripheral/Reassign` de un periférico "Disponible" (dueño anterior inactivo) a un tercero → cubierto por prueba unitaria (`PeripheralAssignmentServiceTests.AssignAsync_AllowsAssignWhenCurrentHolderIsInactive`); no ejecutado manualmente vía formulario en esta pasada.
- [x] Eliminar un periférico (Fase 2's flujo) → confirmado que no rompe con el nuevo historial de asignaciones (cascada `PeripheralAssignment→Peripheral` funciona).

**Nota de herramienta**: durante esta prueba se detectó que los clics en botones dentro de modales Bootstrap a veces no disparan el submit real (el DOM parece actualizarse pero no hay actividad de red) — se resolvió reintentando el clic sobre una referencia fresca del elemento. No se identificó ningún problema del lado de la aplicación; el log del servidor confirmó ausencia total de requests en los intentos fallidos.

## Fase 14 — RAES de periférico, irreversible (2026-08-11)

Ejecutado vía navegador (Chrome, sesión Administrador `administrador@exro.co`) contra la instancia local con `AssetOps`, sobre el equipo "GPena".

- [x] Migración `ChangePeripheralEstadoToBool` aplicada contra `AssetOps` real, con filas preexistentes de los 3 valores del enum viejo (Activo=0, Inactivo=1, Raes=2) sembradas manualmente por SQL antes de migrar → verificado que Activo/Inactivo se mapean a `true` y Raes a `false`, sin errores de conversión (el `AlterColumn` ingenuo que genera EF por defecto sí falla contra estos datos; se reemplazó por un `AddColumn`/backfill/`DropColumn`/rename a mano).
- [x] `Peripheral/Create` en un equipo con dueño activo → el periférico nace con badge "Activo" (ya no depende del enum viejo).
- [x] `Peripheral/Details` → botón "Enviar a RAES" visible mientras el periférico no esté en RAES.
- [x] "Enviar a RAES" → badge cambia a "Raes", "Asignación actual" pasa a "RAES Sistema" / Área "RAES" / Regional "RAES", el historial de asignaciones conserva la fila anterior (Gabriela Peña) sin borrarla.
- [x] Tras enviar a RAES, el botón "Enviar a RAES" desaparece — confirmado que no existe ninguna acción de reactivación en `Peripheral/Details` ni en `PeripheralController`.
- [x] `Desktop/Details` refleja el mismo badge "Raes" para el periférico (la proyección de `DesktopService.GetDetailAsync` ahora deriva el estado desde `PeripheralAssignment`, no del enum viejo).
- [ ] Un periférico "Disponible" (sin asignación o con tenedor inactivo) → badge "Disponible" en vez de "Activo"/"Sin asignar" — cubierto por pruebas unitarias (`PeripheralServiceTests.GetDetailAsync_ShowsEstadoDerivedFromAssignmentAndBool`, `_ShowsDisponibleWhenHolderInactive`); no ejecutado manualmente vía formulario en esta pasada.
- [ ] Descargar el PDF (`Desktop/DownloadPdf`) de un equipo con un periférico en RAES → confirmar que la línea del periférico muestra "[Raes]" en vez del antiguo "[Raes]" del enum (el valor de texto es el mismo por coincidencia, pero ahora viene de la proyección derivada, no de `Enum.ToString()`); no ejecutado manualmente en esta pasada.

## Fase 15 — Mantenimiento de periféricos + retiro de Observaciones (2026-08-11)

Ejecutado vía navegador (Chrome, sesión Administrador `administrador@exro.co`) contra la instancia local con `AssetOps`, sobre el equipo "GPena".

- [x] Migración `AddPeripheralMaintenanceAndRetireObservation` aplicada contra `AssetOps` real, con datos históricos sembrados manualmente por SQL (una `PeripheralObservation` de cada tipo: Reparación, Cambio, BajaRAES) → verificado que Reparación/Cambio se migran a `PeripheralMaintenance` con el tipo placeholder "Migrado de periférico" y el primer técnico Administrador disponible, con la descripción prefijada `[Migrado automáticamente...]`; BajaRAES se omite correctamente (ya reflejado en `Peripheral.Estado`); la tabla `PeripheralObservation` desaparece sin errores.
- [x] `Desktop/Details` → el enlace "Registrar novedad" y el sub-listado de observaciones por periférico ya no aparecen en ningún periférico.
- [x] `Peripheral/Details` → la tarjeta "Observaciones" fue reemplazada por "Historial de mantenimiento", con enlace "Registrar mantenimiento".
- [x] `PeripheralMaintenance/Create` (tipo + técnico restringido a rol Administrador + fecha + descripción) → guardado correctamente, visible de inmediato en `Peripheral/Details` con formato igual al de `Maintenance/Create` de equipos.
- [x] `GET /Peripheral/AddObservation` (ruta retirada) → 404, confirmando que la acción y la vista ya no existen.
- [ ] Sesión Auditor → `PeripheralMaintenance/Create` sigue restringido a Administrador (controlador nuevo con `[Authorize(Roles="Administrador")]` a nivel de clase, cubierto por `AuthorizationAttributeTests`); no re-verificado manualmente en esta pasada.
- [ ] Descargar el PDF (`Desktop/DownloadPdf`) de un equipo con periféricos → confirmar que ya no intenta imprimir observaciones de periféricos (se quitó ese bloque de `DesktopPdfService`); no ejecutado manualmente en esta pasada.

## Fase 16 — Inventario de periféricos a nivel empresa + ajuste de autorización (2026-08-11)

Ejecutado vía navegador (Chrome, sesiones Administrador `administrador@exro.co` y Auditor `test.usuario@empresa.com` — se restableció la contraseña de esta última vía `AccessAccount/Edit` para poder iniciar sesión, ya que es una cuenta de prueba existente sin contraseña conocida) contra la instancia local con `AssetOps`.

- [x] Enlace "Periféricos" visible en la navegación tanto para Administrador como para Auditor (fuera del bloque `IsInRole("Administrador")`).
- [x] `GET /Peripheral/Index` con sesión Auditor → 200 OK, tarjetas de estadísticas (Total/Activos/Disponibles/RAES) y tabla filtrable visibles, sin ningún botón de escritura (la vista no tiene acciones de escritura embebidas).
- [x] `GET /Peripheral/Create` con sesión Auditor → redirige a `Login/AccessDenied`, confirmando que el refactor de autorización (clase `[Authorize]` simple + `[Authorize(Roles="Administrador")]` por acción de escritura) funciona igual que en `DesktopController`.
- [x] `Desktop/Details` con sesión Auditor → sin botones "Editar"/"Asignar"/"Enviar a RAES"/"Eliminar"/"Agregar periférico" (gating por `User.IsInRole` sin cambios, pero ahora verificado también tras el refactor de `PeripheralController`).
- [ ] Filtrar `Peripheral/Index` por tipo/estado/texto de búsqueda con datos reales → cubierto por pruebas unitarias de `GetInventoryAsync`/`GetInventoryStatsAsync`; no ejecutado manualmente con periféricos de prueba en esta pasada (la tabla estaba vacía al momento de probar).
- [ ] `GET /Peripheral/Reassign`, `/Peripheral/Edit`, `/Peripheral/Delete`, `/Peripheral/RetireToRaes` con sesión Auditor → deberían redirigir igual a `AccessDenied`; cubierto por `AuthorizationAttributeTests.PeripheralController_WriteActions_RequireAdministrador`, no re-verificado manualmente ruta por ruta en esta pasada.

## Cobertura automatizada

- [x] `dotnet test --collect:"XPlat Code Coverage"` corre sin fallos. (Verificado 2026-08-10: 94/94 tests correctos tras Fase 7.)
- [x] El reporte de cobertura muestra ≥80% de líneas cubiertas en `Services/` (ver sección "Pruebas — estrategia" en `PLAN_DESARROLLO.md`). (Verificado 2026-08-10: 93.82% agregado tras Fase 7; `AuthService.cs`/`IAuthService.cs` pre-existentes y sin tocar siguen en 0% pero no bajan el agregado de la meta.)
- [x] Tras la Fase 10 (acceso remoto): 133/133 pruebas correctas; `DesktopService`/`DesktopPdfService` (incluida toda la lógica nueva de `Remote`) en 100% de líneas cubiertas. (Verificado 2026-08-11 vía `dotnet test --collect:"XPlat Code Coverage"`.)
