# Revisión integral de GestorEquipos — AssetOps

Fecha: 8 de septiembre de 2026. Referencia: commit `3e7079a` y cambios locales presentes durante la revisión.

## Resumen ejecutivo

El proyecto tiene una base funcional: separación MVC/servicios, contraseñas de acceso con hash, protección antifalsificación, restricciones por rol y una suite automatizada amplia. Sin embargo, se reprodujeron fallos importantes que esa suite no detecta: sesiones que conservan permisos después de eliminar una cuenta, acceso de usuarios desactivados, pérdida del último administrador, operaciones parcialmente guardadas y errores HTTP 500 ante entradas manipuladas.

Este documento contiene **40 hallazgos**, agrupados y numerados. Cada uno explica el problema, su impacto, una mejora propuesta y cómo verificarla. No se implementaron correcciones al producto.

### Cómo interpretar la prioridad y la evidencia

- **Crítica:** permite conservar y ejercer privilegios administrativos retirados.
- **Alta:** afecta acceso, confidencialidad, integridad o recuperación de operaciones importantes.
- **Media:** causa errores, confusión, limitaciones operativas o problemas al crecer.
- **Baja:** mejora consistencia, mantenimiento o presentación sin bloqueo inmediato.
- **Reproducido:** comprobado mediante HTTP y/o SQL en una base aislada.
- **Código:** comportamiento directamente identificado en la implementación; no necesariamente ejercitado de extremo a extremo.
- **Riesgo / mejora:** consecuencia potencial o propuesta de producto; se indica la validación pendiente.

### Validación realizada y límites

| Comprobación | Resultado |
| --- | --- |
| `dotnet build Gestor_Equipos.sln --no-restore` | Correcta: 0 errores y 25 advertencias de nulabilidad. |
| `dotnet test GestorEquipos.Tests/GestorEquipos.Tests.csproj --no-build --no-restore --collect:"XPlat Code Coverage" --results-directory obj/audit-results` | 262 pruebas correctas; 0 fallidas y 0 omitidas. |
| Cobertura de líneas de `Services/` | 1.683 de 1.758 líneas instrumentadas: **95,73%**; supera el objetivo documentado del 80%. |
| `dotnet list GestorEquipos.csproj package --vulnerable --include-transitive --format json` | Consulta satisfactoria; no reportó paquetes vulnerables de la aplicación. No comprende automáticamente JavaScript vendorizado, imágenes Docker ni todo el sistema operativo. |
| Arranque con SQL Server real | Migraciones y bootstrap ejecutados en `GestorEquipos_Audit_20260908`, usando exclusivamente registros ficticios. |
| HTTP con Administrador y Auditor | Verificados acceso a módulos, restricciones de lectura/escritura, formularios y escenarios adversos descritos abajo. |
| PDF | Ambas rutas de descarga respondieron 200. No se verificó visualmente la maquetación de los documentos. |
| Navegador | **Pendiente**: dos intentos fallaron antes de conectar por un error de la herramienta (`missing field sandboxPolicy`). No hubo capturas ni pruebas reales de teclado, contraste o tamaños de pantalla. |

La revisión UI/UX se apoya en Razor, CSS, JavaScript y HTML servido; los efectos visuales están señalados como pendientes. No se realizaron pruebas de carga, ataques de fuerza bruta, restauraciones de backups ni despliegue Docker. No se inspeccionaron datos de producción ni se reproducen secretos en este informe.

### Módulos cubiertos

| Área | Revisión realizada |
| --- | --- |
| Inicio de sesión y cuentas | Código, pruebas existentes y HTTP: inicio de sesión, roles, desactivación, eliminación y sesiones antiguas. |
| Usuarios, áreas y regionales | Controladores, servicios, vistas y pruebas; listados y creación/desactivación de usuarios por HTTP. CRUD de catálogos revisado por código y suite existente. |
| Equipos y asignaciones | Creación, edición, duplicados, asignación, baja, historial y descarga PDF; revisión del borrado. |
| Periféricos y tipos | Creación, herencia de asignación, baja, inventario y servicios; revisión de reasignación y eliminación. |
| Accesos remotos | Creación desde equipo, lectura por rol, liberación/asignación, validaciones y edición revisada en código. |
| Mantenimiento y licencias | Creación HTTP, descarte de clave al marcar sin licencia y descarga de mantenimiento; revisión de historial/checklist. |
| Dashboard e interfaz compartida | Rutas HTTP, agregaciones, navegación, formularios, gráficos, estilos y dependencias. |
| Persistencia, despliegue y calidad | Modelo EF, migraciones, Docker, script SQL local, configuración, cobertura y documentación. |

## 1. Seguridad y privacidad

### 1.1. SEG-01 — Las sesiones sobreviven a la retirada de permisos

**Prioridad: Crítica · Evidencia: Reproducido.** Ubicación: [Program.cs:37](Program.cs#L37), [UserAdminService.cs:99](Services/Implementations/UserAdminService.cs#L99).

- **Problema y evidencia:** después de degradar un administrador a Auditor, su cookie anterior siguió abriendo `/AccessAccount/Index` y permitió restaurar su propio rol. También se eliminó otra cuenta administrativa y su sesión pudo crear un usuario nuevo.
- **Por qué mejorarlo:** eliminar una cuenta o quitarle permisos no detiene una sesión comprometida. La expiración deslizante agrava el problema.
- **Mejora:** validar existencia, estado y versión de seguridad de la cuenta en la cookie; invalidar sesiones al cambiar contraseña, rol o estado y al eliminar cuentas. ASP.NET Core ofrece `ValidatePrincipal` para reaccionar a cambios del usuario: [documentación de cookies](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie#react-to-back-end-changes).
- **Aceptación:** una cookie emitida antes de esos cambios debe ser rechazada antes de autorizar la siguiente operación protegida.

### 1.2. SEG-02 — Una persona desactivada todavía puede iniciar sesión

**Prioridad: Alta · Evidencia: Reproducido.** Ubicación: [AuthService.cs:20](Services/Auth/AuthService.cs#L20), [UserAdminController.cs:133](Controllers/UserAdminController.cs#L133).

- **Problema y evidencia:** `ValidateUserAsync` no comprueba `User.Activo`. Se desactivó la persona asociada al administrador mediante el endpoint de usuarios y un inicio de sesión nuevo funcionó.
- **Por qué mejorarlo:** el estado de baja no bloquea el acceso y los endpoints genéricos permiten actuar sobre personas con cuenta aunque estén ocultas del listado normal.
- **Mejora:** definir y comprobar el estado de acceso en el servidor; proteger la separación entre personas del inventario y cuentas del sistema.
- **Aceptación:** una cuenta deshabilitada no inicia sesión; las rutas de usuarios rechazan identificadores reservados o ajenos a ese flujo.

### 1.3. SEG-03 — Es posible quitar el rol al último administrador

**Prioridad: Alta · Evidencia: Reproducido.** Ubicación: [UserAdminService.cs:148](Services/Implementations/UserAdminService.cs#L148).

- **Problema y evidencia:** editar la única cuenta administrativa y seleccionar Auditor dejó cero administradores en SQL. La protección del último administrador existe al eliminar, pero no al editar.
- **Por qué mejorarlo:** al expirar las sesiones, la administración del sistema puede quedar bloqueada.
- **Mejora:** aplicar la misma regla de conservación de al menos un administrador habilitado a edición, eliminación y desactivación, con protección ante operaciones simultáneas.
- **Aceptación:** degradar o desactivar al último administrador se rechaza sin cambios parciales; las operaciones concurrentes tampoco dejan cero administradores.

### 1.4. SEG-04 — El script SQL contiene una contraseña fija y desactiva su política

**Prioridad: Alta · Evidencia: Código; script local todavía no versionado.** Ubicación: [create-docker-sql-login.sql:14](scripts/create-docker-sql-login.sql#L14).

- **Problema:** el script crea un login con contraseña literal de baja complejidad y `CHECK_POLICY = OFF`. El valor se omite deliberadamente aquí.
- **Por qué mejorarlo:** copiar el archivo o incorporarlo a Git difunde una credencial reutilizable. No se comprobó que ese login esté desplegado ni que la contraseña siga vigente.
- **Mejora:** parametrizar la credencial, mantener activa la política y retirar el literal; rotar esa contraseña donde se haya utilizado.
- **Aceptación:** el script exige un secreto externo, no contiene una clave utilizable y un escaneo de secretos no detecta credenciales incorporadas.

### 1.5. SEG-05 — Las contraseñas RDP se guardan recuperables en texto plano

**Prioridad: Alta · Evidencia: Código.** Ubicación: [Remote.cs:31](Models/Remote.cs#L31), [RemoteService.cs:119](Services/Implementations/RemoteService.cs#L119).

- **Problema:** el campo `Password` se persiste directamente. La documentación lo identifica como una decisión deliberada, pero esa decisión conserva un riesgo real.
- **Por qué mejorarlo:** una lectura indebida de la tabla o de un backup expone accesos a otros equipos.
- **Mejora:** preferir referencias a un almacén de secretos; si es indispensable recuperarlos, usar cifrado con claves administradas fuera de la base y permisos específicos. Un hash no sirve si la aplicación necesita recuperar la clave.
- **Aceptación:** una copia de la base no permite leer las contraseñas y se verifica la recuperación con el mecanismo de claves autorizado.

### 1.6. SEG-06 — Las claves se envían anticipadamente al HTML y a PDFs administrativos

**Prioridad: Alta · Evidencia: HTML reproducido; inclusión en PDF confirmada en código.** Ubicación: [Remote/Details.cshtml:42](Views/Remote/Details.cshtml#L42), [DesktopPdfService.cs:128](Services/Implementations/DesktopPdfService.cs#L128).

- **Problema:** «mostrar clave» solo revela un valor que ya llegó en `data-password`. La exportación de hoja de vida incluye la contraseña automáticamente para Administrador.
- **Por qué mejorarlo:** una hoja de vida compartida puede divulgar credenciales sin que el usuario espere exportarlas. **El HTML del Auditor probado no contenía la clave**; no se afirma una exposición a ese rol.
- **Mejora:** excluir secretos de PDFs por defecto y recuperarlos solo mediante una acción explícita, autorizada y registrada, con respuesta no almacenable.
- **Aceptación:** el HTML inicial y el PDF ordinario no contienen claves; revelarlas requiere autorización del servidor.

### 1.7. SEG-07 — La configuración entregada permite autenticarse por HTTP

**Prioridad: Alta · Evidencia: Reproducido localmente y configuración.** Ubicación: [Program.cs:45](Program.cs#L45), [Dockerfile:28](Dockerfile#L28), [docker-compose.yml:15](docker-compose.yml#L15).

- **Problema:** el contenedor expone HTTP y la cookie usa `SameAsRequest`. En la instancia aislada con ambiente Production el login funcionó por HTTP y la cookie no tenía `Secure`.
- **Por qué mejorarlo:** si ese puerto es accesible sin una capa TLS, credenciales y cookies viajan sin protección de transporte. No se verificó la infraestructura del servidor real.
- **Mejora:** documentar e imponer HTTPS, cerrar el acceso HTTP directo y configurar proxies confiables antes de exigir cookies seguras. Ver [configuración oficial de proxies](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer).
- **Aceptación:** el acceso externo solo funciona por HTTPS y las cookies de sesión llevan `Secure` sin bucles de redirección.

### 1.8. SEG-08 — No hay control de intentos de autenticación en la aplicación

**Prioridad: Alta · Evidencia: Riesgo identificado en código; sin ataque de carga.** Ubicación: [LoginController.cs:34](Controllers/LoginController.cs#L34), [AuthService.cs:20](Services/Auth/AuthService.cs#L20).

- **Problema:** no se encontró limitación de frecuencia ni protección por cuenta para el login o la comprobación de contraseña administrativa.
- **Por qué mejorarlo:** facilita adivinación de contraseñas y abuso del coste de verificación del hash. Una protección externa, si existe, no está documentada aquí.
- **Mejora:** incorporar límites por origen y cuenta, retrasos progresivos, mensajes no reveladores y registro de intentos; evaluar segundo factor para administradores.
- **Aceptación:** pruebas controladas reciben una respuesta de limitación tras el umbral definido, sin bloquear indiscriminadamente a usuarios legítimos.

### 1.9. SEG-09 — La trazabilidad de operaciones sensibles es incompleta

**Prioridad: Media · Evidencia: Código.** Ubicación: [SpecChangeLog.cs:26](Models/SpecChangeLog.cs#L26), [RemoteService.cs:98](Services/Implementations/RemoteService.cs#L98), [UserAdminService.cs:99](Services/Implementations/UserAdminService.cs#L99).

- **Problema:** se registran cambios de especificaciones, pero no un historial equivalente de roles, eliminación de cuentas, revelación de claves o liberación de accesos. Varias fechas usan `DateOnly` y `DateTime.Now`.
- **Por qué mejorarlo:** dificulta explicar quién cambió un acceso y en qué orden; la fecha local también depende del entorno de ejecución.
- **Mejora:** registrar actor, operación, objeto, resultado y fecha UTC con hora; conservar una zona de presentación definida y excluir secretos de los registros.
- **Aceptación:** cada acción sensible produce un evento correlacionable, con precisión temporal suficiente y sin contraseñas.

## 2. Lógica e integridad de datos

### 2.1. LOG-01 — Crear una cuenta puede dejar una persona huérfana

**Prioridad: Alta · Evidencia: Reproducido.** Ubicación: [AccessAccountController.cs:51](Controllers/AccessAccountController.cs#L51).

- **Problema y evidencia:** se guarda la persona antes de crear la cuenta. Reutilizar un `Username` existente con otro correo dejó una fila en `Users` sin `UserSystem`, aunque el formulario informó un error.
- **Por qué mejorarlo:** reintentar falla después por correo duplicado y el registro aparece como persona del inventario.
- **Mejora:** encapsular la creación completa en una operación atómica y convertir conflictos de unicidad en errores del formulario.
- **Aceptación:** cualquier fallo al crear la cuenta deja la base exactamente como estaba antes del envío.

### 2.2. LOG-02 — Las bajas RAES pueden guardarse parcialmente

**Prioridad: Alta · Evidencia: Reproducido para equipos; patrón equivalente en periféricos.** Ubicación: [DesktopService.cs:260](Services/Implementations/DesktopService.cs#L260), [PeripheralService.cs:157](Services/Implementations/PeripheralService.cs#L157).

- **Problema y evidencia:** se guarda `Estado=false` antes de registrar la asignación RAES. Al faltar el usuario reservado, la baja respondió 500 pero el equipo quedó inactivo. Reintentar respondió 200 sin generar el historial faltante.
- **Por qué mejorarlo:** estado e historial se contradicen y el reintento oculta el daño.
- **Mejora:** validar precondiciones y persistir baja e historial en una transacción compatible con la estrategia de reintentos SQL; revisar también creación de periférico y asignación inicial.
- **Aceptación:** un fallo intermedio revierte todo; reintentar no duplica ni deja eventos incompletos.

### 2.3. LOG-03 — Los registros reservados RAES son editables por rutas normales

**Prioridad: Alta · Evidencia: Reproducido.** Ubicación: [UserAdminController.cs:83](Controllers/UserAdminController.cs#L83), [UserAdminService.cs:48](Services/Implementations/UserAdminService.cs#L48), [AuthBootstrapper.cs:107](Services/Auth/AuthBootstrapper.cs#L107).

- **Problema y evidencia:** un POST válido a la edición genérica permitió cambiar el correo del usuario RAES. El posterior retiro de un equipo falló. Ocultarlo del listado no impide editarlo por identificador.
- **Por qué mejorarlo:** las reglas internas dependen de un correo mutable. Áreas y regionales reservadas también carecen de protección equivalente en sus servicios.
- **Mejora:** identificar registros del sistema de forma explícita y bloquear su modificación, desactivación y eliminación en servicios y endpoints generales.
- **Aceptación:** solicitudes directas sobre registros reservados son rechazadas y el proceso RAES sigue funcionando.

### 2.4. LOG-04 — Los desplegables filtran opciones que el servidor acepta igualmente

**Prioridad: Alta · Evidencia: Reproducido.** Ubicación: [AsignationService.cs:17](Services/Implementations/AsignationService.cs#L17), [PeripheralAssignmentService.cs:17](Services/Implementations/PeripheralAssignmentService.cs#L17), [RemoteService.cs:162](Services/Implementations/RemoteService.cs#L162).

- **Problema y evidencia:** un POST asignó un equipo a una persona inactiva; otro vinculó un acceso remoto a un equipo dado de baja. Los servicios no replican todos los filtros del selector.
- **Por qué mejorarlo:** una solicitud modificada o una pantalla desactualizada produce relaciones inválidas para el negocio.
- **Mejora:** validar existencia, actividad y elegibilidad del destinatario dentro de la operación. Separar la transición interna RAES de la asignación ordinaria.
- **Aceptación:** identificadores excluidos por las reglas del negocio no se aceptan aunque se envíen manualmente.

### 2.5. LOG-05 — Asignaciones y ediciones carecen de control de concurrencia

**Prioridad: Alta · Evidencia: Riesgo; carrera no ejecutada.** Ubicación: [AsignationService.cs:34](Services/Implementations/AsignationService.cs#L34), [RemoteService.cs:98](Services/Implementations/RemoteService.cs#L98), [DbContext.cs:34](Models/DbContext/DbContext.cs#L34).

- **Problema:** varias operaciones consultan disponibilidad y guardan después; no se encontraron tokens de concurrencia o una operación atómica que proteja esa decisión. Dos solicitudes pueden leer el mismo estado libre.
- **Por qué mejorarlo:** ambas pueden declarar éxito, sobrescribir cambios o crear un historial engañoso. Un `SaveChanges` transaccional por sí solo no protege una comprobación anterior.
- **Mejora:** proteger la asignación actual con actualización condicionada/versionada y restricciones adecuadas; manejar el conflicto con un mensaje de actualización.
- **Aceptación:** dos asignaciones simultáneas incompatibles producen un único éxito y un conflicto recuperable.

### 2.6. LOG-06 — Se aceptan dos equipos con el mismo serial

**Prioridad: Alta · Evidencia: Reproducido.** Ubicación: [DesktopService.cs:171](Services/Implementations/DesktopService.cs#L171), [DbContext.cs:60](Models/DbContext/DbContext.cs#L60).

- **Problema y evidencia:** dos envíos crearon dos equipos con el mismo `SerialNumber`. No hay validación ni índice único para esa identificación.
- **Por qué mejorarlo:** compromete búsquedas, asignaciones y hojas de vida de un mismo activo físico.
- **Mejora:** definir la identidad única del activo, normalizarla y protegerla en SQL; depurar duplicados antes de migrar. Para periféricos sin serial, permitir ausencia sin convertirla en un valor compartido obligatorio.
- **Aceptación:** crear o editar un equipo con identificación duplicada muestra un error claro; un envío simultáneo tampoco duplica el activo.

### 2.7. LOG-07 — Eliminar una cuenta con historial devuelve HTTP 500

**Prioridad: Media · Evidencia: Reproducido.** Ubicación: [UserAdminService.cs:153](Services/Implementations/UserAdminService.cs#L153), [DbContext.cs:225](Models/DbContext/DbContext.cs#L225).

- **Problema y evidencia:** después de editar un equipo y generar `SpecChangeLog`, eliminar su cuenta autora falló por la relación restrictiva. El controlador solo captura `InvalidOperationException`, no el conflicto de persistencia.
- **Por qué mejorarlo:** el administrador no recibe una explicación y puede pensar que la baja se realizó.
- **Mejora:** preferir deshabilitar cuentas conservando autoría histórica; comprobar dependencias y devolver un resultado de negocio explícito.
- **Aceptación:** retirar acceso a un autor de cambios no produce 500 ni elimina su historial; sus sesiones quedan revocadas.

### 2.8. LOG-08 — El PDF de un mantenimiento histórico usa datos actuales

**Prioridad: Alta · Evidencia: Código.** Ubicación: [MaintenanceController.cs:96](Controllers/MaintenanceController.cs#L96).

- **Problema:** el PDF toma la última asignación y los periféricos actuales, no los existentes al registrar el mantenimiento. Cambiar el usuario, renombrar sus datos o retirar un periférico altera futuras descargas del mismo evento.
- **Por qué mejorarlo:** un documento histórico puede atribuir un trabajo a equipos/personas distintos de los originales.
- **Mejora:** persistir una instantánea de los datos relevantes al registrar el mantenimiento; distinguir fecha del evento y fecha de emisión. No inventar datos históricos para registros antiguos.
- **Aceptación:** reasignar el equipo o editar catálogos después no altera el contenido histórico del mantenimiento original.

### 2.9. LOG-09 — Referencias inexistentes llegan a SQL y producen errores internos

**Prioridad: Media · Evidencia: Reproducido.** Ubicación: [DesktopController.cs:94](Controllers/DesktopController.cs#L94), [DesktopCreateViewModel.cs:29](Models/ViewModels/Desktop/DesktopCreateViewModel.cs#L29).

- **Problema y evidencia:** crear un equipo con `OSVersionId=999999` devolvió 500. `[Required]` en un entero no comprueba que su registro exista y varios servicios confían en identificadores recibidos.
- **Por qué mejorarlo:** formularios antiguos, referencias borradas o solicitudes manipuladas terminan en fallos opacos.
- **Mejora:** comprobar referencias y rangos en el servidor, conservar las restricciones SQL y traducir conflictos esperables a validaciones; usar 404 cuando corresponda a un recurso de ruta.
- **Aceptación:** IDs inexistentes en equipos, cuentas, mantenimiento y licencias producen respuestas controladas y no dejan datos parciales.

### 2.10. LOG-10 — La validación de IP y puerto solo comprueba la forma

**Prioridad: Media · Evidencia: Reproducido.** Ubicación: [RemoteEditViewModel.cs:47](Models/ViewModels/Remote/RemoteEditViewModel.cs#L47), [DesktopCreateViewModel.cs:108](Models/ViewModels/Desktop/DesktopCreateViewModel.cs#L108).

- **Problema y evidencia:** se guardó un acceso RDP con IP `999.999.999.999` y puerto `99999`. Las expresiones regulares permiten octetos y puertos fuera de rango.
- **Por qué mejorarlo:** el inventario presenta como utilizable una conexión imposible y traslada el diagnóstico al operador.
- **Mejora:** compartir una validación semántica de dirección y puerto entre creación y edición; para IPv4, verificar octetos 0–255 y puerto 1–65535.
- **Aceptación:** rechazar los valores reproducidos y los límites inválidos; aceptar direcciones y puertos válidos en ambos formularios.

### 2.11. LOG-11 — Una etiqueta de RAM puede romper el formulario completo

**Prioridad: Media · Evidencia: Reproducido.** Ubicación: [DesktopController.cs:230](Controllers/DesktopController.cs#L230).

- **Problema y evidencia:** el ordenamiento usa `int.Parse(r.Especification.Replace("GB", ""))`. Añadir la etiqueta ficticia `16 GiB` produjo HTTP 500 en Crear equipo.
- **Por qué mejorarlo:** un dato de catálogo no debería dejar inutilizable toda la pantalla.
- **Mejora:** almacenar capacidad y unidad estructuradas o utilizar una conversión tolerante con orden secundario estable; validar catálogos al cargarlos.
- **Aceptación:** etiquetas inesperadas no rompen Crear/Editar; capacidades válidas mantienen orden numérico.

### 2.12. LOG-12 — Los periféricos huérfanos no tienen un flujo para cambiar de equipo

**Prioridad: Media · Evidencia: Código; mejora funcional.** Ubicación: [DbContext.cs:177](Models/DbContext/DbContext.cs#L177), [PeripheralController.cs:172](Controllers/PeripheralController.cs#L172), [PeripheralService.cs:121](Services/Implementations/PeripheralService.cs#L121).

- **Problema:** al eliminar un equipo se conserva el periférico con `DesktopId=null`. «Reasignar» cambia la persona; la edición no actualiza el equipo padre. El estado calculado continúa Disponible cuando no tiene equipo.
- **Por qué mejorarlo:** el periférico sobrevive al borrado, pero no existe un recorrido completo para reutilizarlo en otro equipo desde su módulo.
- **Mejora:** ofrecer una operación explícita de vincular/cambiar equipo, con reglas e historial coherentes con la asignación personal.
- **Aceptación:** un periférico huérfano puede vincularse a un equipo activo desde la UI y sus listados/detalle muestran el nuevo estado consistente.

## 3. UI/UX y accesibilidad

### 3.1. UX-01 — La estructura principal no se adapta a pantallas estrechas

**Prioridad: Alta · Evidencia: Riesgo deducido del CSS; pendiente navegador.** Ubicación: [dashboard.css:35](wwwroot/css/dashboard.css#L35), [dashboard.css:395](wwwroot/css/dashboard.css#L395).

- **Problema:** sidebar fijo de 240 px, contenido con padding de 2 rem y columnas de gráficos con mínimo de 360 px, sin media queries para la estructura administrativa.
- **Por qué mejorarlo:** en un móvil queda muy poco espacio para formularios y es previsible el desbordamiento horizontal.
- **Mejora:** navegación colapsable accesible, padding adaptable y columnas que admitan el ancho disponible.
- **Aceptación pendiente:** verificar 360, 390, 768 y 1366 px en navegador; no debe haber desplazamiento horizontal global ni acciones inaccesibles.

### 3.2. UX-02 — Los filtros y algunas contraseñas no tienen etiquetas asociadas

**Prioridad: Media · Evidencia: Marcado; pendiente lector de pantalla.** Ubicación: [Desktop/Index.cshtml:55](Views/Desktop/Index.cshtml#L55), [Peripheral/Details.cshtml:70](Views/Peripheral/Details.cshtml#L70).

- **Problema:** buscadores dependen de placeholders; selectores carecen de etiqueta explícita y algunos campos de contraseña en modales no tienen asociación `for`/`id`.
- **Por qué mejorarlo:** la finalidad del control es menos clara para lectores de pantalla y cuando el placeholder desaparece.
- **Mejora:** asociar etiquetas visibles o accesibles a cada control, sin duplicar identificadores en modales repetidos.
- **Aceptación pendiente:** cada control tiene nombre accesible inequívoco y se puede completar utilizando solo teclado.

### 3.3. UX-03 — Las tablas pueden ocultar columnas y acciones

**Prioridad: Media · Evidencia: Riesgo de marcado/CSS; pendiente navegador.** Ubicación: [Desktop/Index.cshtml:65](Views/Desktop/Index.cshtml#L65), [dashboard.css:333](wwwroot/css/dashboard.css#L333).

- **Problema:** tablas amplias se alojan directamente en `.app-card`, que usa `overflow:hidden`, sin un contenedor de desplazamiento horizontal.
- **Por qué mejorarlo:** seriales largos, zoom y pantallas pequeñas pueden dejar fuera del área visible enlaces importantes.
- **Mejora:** incorporar un contenedor de tabla adaptable y priorizar columnas esenciales; mantener acciones accesibles.
- **Aceptación pendiente:** con zoom del 200% y textos largos, todas las columnas pueden consultarse y la acción principal permanece alcanzable.

### 3.4. UX-04 — Búsqueda y estados vacíos no explican el resultado

**Prioridad: Media · Evidencia: Código y marcado.** Ubicación: [Desktop/Index.cshtml:51](Views/Desktop/Index.cshtml#L51), [Peripheral/Index.cshtml:134](Views/Peripheral/Index.cshtml#L134).

- **Problema:** varios buscadores no muestran botón Buscar; cambiar el filtro envía el formulario. Cuando no hay coincidencias aparece «No hay ... registrados», aunque existan datos. La búsqueda de periféricos no incluye serial.
- **Por qué mejorarlo:** el operador puede interpretar que perdió registros o no saber cómo aplicar/limpiar la búsqueda.
- **Mejora:** botones Buscar/Limpiar, cantidad de resultados y mensajes distintos para inventario vacío y búsqueda sin coincidencias; permitir buscar periféricos por serial.
- **Aceptación:** una búsqueda inexistente comunica «sin resultados», conserva el criterio y permite restaurar la lista con una acción clara.

### 3.5. UX-05 — Se ofrecen acciones que las reglas no permiten completar

**Prioridad: Media · Evidencia: Código.** Ubicación: [Desktop/Details.cshtml:19](Views/Desktop/Details.cshtml#L19), [AsignationService.cs:29](Services/Implementations/AsignationService.cs#L29).

- **Problema:** «Asignar» se muestra incluso en equipos retirados; «Reasignar» puede llevar a un flujo que rechaza cambiar de persona mientras el titular siga activo.
- **Por qué mejorarlo:** el usuario completa pasos para recibir un rechazo previsible y no obtiene una salida útil.
- **Mejora:** habilitar acciones según estado, explicar por qué una operación no está disponible y dirigir al flujo de devolución cuando corresponda.
- **Aceptación:** las pantallas no invitan a asignar equipos RAES y explican cómo resolver una asignación ocupada; el servidor mantiene sus controles.

### 3.6. UX-06 — Liberar un activo depende de desactivar a la persona

**Prioridad: Alta · Evidencia: Código; limitación de producto.** Ubicación: [AsignationService.cs:41](Services/Implementations/AsignationService.cs#L41), [UserAdminService.cs:219](Services/Implementations/UserAdminService.cs#L219).

- **Problema:** no hay una devolución ordinaria de un equipo por una persona activa. La disponibilidad se deriva de que el último titular esté inactivo; desactivarlo afecta todos sus activos.
- **Por qué mejorarlo:** cambiar un solo equipo o registrar una devolución obliga a una operación sobre la persona con consecuencias más amplias.
- **Mejora:** agregar devolución/liberación individual con fecha, motivo y responsable, y distinguirla de baja de usuario y baja RAES.
- **Aceptación:** una persona activa puede devolver un activo sin liberar los demás ni perder su estado activo; el historial conserva entrega y devolución.

### 3.7. UX-07 — Los gráficos no ofrecen los valores completos en formato accesible

**Prioridad: Media · Evidencia: Marcado; pendiente lector de pantalla.** Ubicación: [Home/Index.cshtml:135](Views/Home/Index.cshtml#L135).

- **Problema:** los canvas tienen `aria-label`, pero el gráfico por regional no ofrece una tabla o lista equivalente con sus valores; la leyenda de Chart.js se dibuja en canvas.
- **Por qué mejorarlo:** el nombre del gráfico no transmite los datos a quien no puede interpretarlo visualmente.
- **Mejora:** incluir una tabla o resumen textual asociado, especialmente para usuarios por regional; conservar los estados vacíos ya existentes.
- **Aceptación pendiente:** todos los valores del gráfico pueden consultarse con lector de pantalla y sin ejecutar JavaScript.

## 4. Incongruencias funcionales y documentación

### 4.1. INC-01 — «Activo» y «Usuarios» representan poblaciones diferentes

**Prioridad: Media · Evidencia: Código.** Ubicación: [DesktopController.cs:35](Controllers/DesktopController.cs#L35), [DesktopService.cs:312](Services/Implementations/DesktopService.cs#L312), [UserAdminService.cs:234](Services/Implementations/UserAdminService.cs#L234).

- **Problema:** en Equipos, Activo significa `Estado=true`; en Dashboard exige además un titular activo. El gráfico de usuarios incluye personas con cuenta, mientras el listado normal las excluye.
- **Por qué mejorarlo:** cifras distintas con etiquetas equivalentes parecen errores y dificultan explicar el inventario.
- **Mejora:** separar «Operativos», «Asignados», «Disponibles» y «RAES»; definir si cada métrica incluye cuentas de acceso y expresarlo en su etiqueta.
- **Aceptación:** un conjunto conocido de datos produce cifras reconciliables entre dashboard, filtros y listados.

### 4.2. INC-02 — Se exige un nombre de usuario que no sirve para autenticarse

**Prioridad: Media · Evidencia: Código.** Ubicación: [AccessAccountCreateViewModel.cs:28](Models/ViewModels/AccessAccount/AccessAccountCreateViewModel.cs#L28), [AuthService.cs:27](Services/Auth/AuthService.cs#L27).

- **Problema:** las cuentas exigen `Username` único, pero el login busca exclusivamente por correo de la persona.
- **Por qué mejorarlo:** se administra un identificador adicional sin explicar su finalidad y puede confundirse con la credencial de ingreso.
- **Mejora:** adoptar correo como identificador único de acceso o admitir explícitamente ambos; ajustar etiquetas y migración de datos según esa decisión de producto.
- **Aceptación:** el formulario de cuentas explica exactamente qué identificador se utiliza al iniciar sesión y no exige campos redundantes sin propósito.

### 4.3. INC-03 — La interfaz promete Excel y mantiene textos de plantilla

**Prioridad: Baja · Evidencia: Código.** Ubicación: [Login/Index.cshtml:20](Views/Login/Index.cshtml#L20), [Home/Privacy.cshtml:1](Views/Home/Privacy.cshtml#L1).

- **Problema:** el login anuncia «PDF o Excel», pero no se encontró exportación Excel en los controladores; el enlace Privacy lleva a una plantilla en inglés sin información propia.
- **Por qué mejorarlo:** genera expectativas incumplidas y transmite una aplicación incompleta.
- **Mejora:** ajustar el anuncio a las exportaciones disponibles y sustituir la plantilla por información pertinente en español; no anunciar funcionalidades antes de incorporarlas.
- **Aceptación:** todas las capacidades anunciadas tienen un recorrido funcional y no quedan textos de plantilla visibles.

### 4.4. INC-04 — La documentación mezcla estados históricos con instrucciones vigentes

**Prioridad: Baja · Evidencia: Código/documentación.** Ubicación: [CLAUDE.md:31](CLAUDE.md#L31), [CLAUDE.md:61](CLAUDE.md#L61), [MANUAL_TESTS.md:1](MANUAL_TESTS.md#L1).

- **Problema:** se mencionan 247 pruebas y una ruta del resolvedor de fuentes que ya cambió; hay afirmaciones antiguas sobre catálogos y flujos que conviven con correcciones posteriores.
- **Por qué mejorarlo:** quien prepara un entorno puede seguir instrucciones que ya no describen la aplicación.
- **Mejora:** separar el registro histórico de pruebas de una guía de instalación vigente; enlazar rutas correctas y fechar las mediciones.
- **Aceptación:** una persona nueva puede instalar y ejecutar las pruebas siguiendo una única guía actual, sin deducir qué notas quedaron superadas.

## 5. Rendimiento y mantenibilidad

### 5.1. REN-01 — Los listados cargan todos los datos antes de filtrar

**Prioridad: Media · Evidencia: Código; sin benchmark de carga.** Ubicación: [DesktopController.cs:30](Controllers/DesktopController.cs#L30), [UserAdminController.cs:24](Controllers/UserAdminController.cs#L24), [RemoteController.cs:25](Controllers/RemoteController.cs#L25).

- **Problema:** búsqueda y filtros se aplican a colecciones ya materializadas; no hay paginación de servidor en los listados principales.
- **Por qué mejorarlo:** el coste de SQL, memoria, HTML y navegación crece con todo el inventario, incluso para buscar un único serial.
- **Mejora:** llevar filtros, orden y paginación a consultas SQL con proyecciones específicas y conteos separados; definir un orden estable.
- **Aceptación:** solicitar una página devuelve únicamente sus filas y se mide tiempo/memoria con un volumen representativo; no se afirma una lentitud medida actualmente.

### 5.2. REN-02 — Se repiten consultas completas para calcular indicadores

**Prioridad: Media · Evidencia: Código.** Ubicación: [RemoteController.cs:27](Controllers/RemoteController.cs#L27), [RemoteService.cs:53](Services/Implementations/RemoteService.cs#L53), [PeripheralController.cs:25](Controllers/PeripheralController.cs#L25).

- **Problema:** Remotos llama a estadísticas y listado; las estadísticas vuelven a cargar el inventario completo. Periféricos también obtiene colecciones para estadísticas y resultados por separado.
- **Por qué mejorarlo:** duplica trabajo y las cifras pueden provenir de instantes distintos.
- **Mejora:** calcular agregados en SQL y traer solo la página requerida; compartir resultados cuando sea apropiado, sin paralelizar consultas sobre el mismo `DbContext`.
- **Aceptación:** una medición de consultas demuestra que las estadísticas no vuelven a materializar el inventario completo.

### 5.3. REN-03 — Las bibliotecas tienen caché anual sin versionar sus URLs

**Prioridad: Media · Evidencia: Código.** Ubicación: [Program.cs:85](Program.cs#L85), [Shared/_Layout.cshtml:116](Views/Shared/_Layout.cshtml#L116).

- **Problema:** `/lib` recibe `max-age=31536000,immutable`, pero sus referencias no incluyen hash de contenido. Actualizar una biblioteca manteniendo su ruta puede dejar clientes con la versión anterior.
- **Por qué mejorarlo:** correcciones funcionales o de seguridad pueden no llegar a navegadores que conservan caché.
- **Mejora:** aplicar versionado por contenido o nombres versionados también a bibliotecas. Inventariar Bootstrap 5.1.0, jQuery 3.6.0, Validation 1.19.5 y Chart.js 4.4.4, identificados en sus archivos; evaluar actualizaciones con pruebas de regresión.
- **Aceptación:** cambiar una biblioteca modifica su URL. La antigüedad no se presenta como vulnerabilidad: por ejemplo, el aviso [GHSA-ffmh-x56j-9rc3](https://github.com/jquery-validation/jquery-validation/security/advisories/GHSA-ffmh-x56j-9rc3) fue corregido en Validation 1.19.5, ya presente aquí.

## 6. Pruebas, configuración y operación

### 6.1. OPS-01 — La cobertura alta no valida los límites HTTP/SQL importantes

**Prioridad: Alta · Evidencia: Pruebas ejecutadas y fallos reproducidos.** Ubicación: [TestHelpers.cs:9](GestorEquipos.Tests/TestHelpers.cs#L9), [AuthorizationAttributeTests.cs:20](GestorEquipos.Tests/Controllers/AuthorizationAttributeTests.cs#L20).

- **Problema:** los servicios se prueban con EF InMemory y gran parte de autorización por reflexión. Las 262 pruebas pasan pese a los fallos de cookies, claves foráneas y transacciones reproducidos.
- **Por qué mejorarlo:** ejecutar líneas no demuestra que restricciones, cookies y operaciones compuestas funcionen en producción. Microsoft documenta las diferencias del proveedor en su [guía de estrategia de pruebas EF](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy).
- **Mejora:** mantener unitarias y añadir integración HTTP con SQL Server aislado, cubriendo primero los escenarios SEG-01/02/03 y LOG-01/02/07/09.
- **Aceptación:** esas regresiones fallan sobre el código actual y pasan al corregirlo; probar también creación de base desde cero y migración desde una versión anterior.

### 6.2. OPS-02 — El arranque modifica el esquema y requiere permisos elevados

**Prioridad: Alta · Evidencia: Código/configuración.** Ubicación: [AuthBootstrapper.cs:22](Services/Auth/AuthBootstrapper.cs#L22), [create-docker-sql-login.sql:27](scripts/create-docker-sql-login.sql#L27).

- **Problema:** cada arranque aplica migraciones y el script concede `db_ddladmin` al login de la aplicación.
- **Por qué mejorarlo:** una conexión equivocada, una migración fallida o una aplicación comprometida tienen capacidad de afectar el esquema; varios arranques requieren coordinación.
- **Mejora:** ejecutar migraciones como paso de despliegue revisable con identidad separada y backup previo; usar permisos mínimos para el proceso web.
- **Aceptación:** reiniciar la aplicación no altera el esquema y su identidad de ejecución no puede ejecutar DDL.

### 6.3. OPS-03 — Faltan controles de calidad reproducibles en el repositorio

**Prioridad: Media · Evidencia: Inspección del repositorio.** Ubicación: [GestorEquipos.csproj:18](GestorEquipos.csproj#L18), [GestorEquipos.Tests.csproj:1](GestorEquipos.Tests/GestorEquipos.Tests.csproj#L1).

- **Problema:** no se encontraron workflows de CI ni un umbral automático de cobertura. No hay `global.json`; el SDK seleccionado aquí fue 10.0.400 para proyectos `net8.0`, y Tools de EF es 10.0.7 mientras runtime/design son 8.0.x.
- **Por qué mejorarlo:** máquinas distintas pueden utilizar herramientas diferentes y el objetivo del 80% es únicamente documental. La mezcla no impidió esta compilación, pero debe verificarse al generar migraciones.
- **Mejora:** fijar una política de SDK/herramientas compatibles y automatizar build, pruebas, cobertura, dependencias y secretos en PR.
- **Aceptación:** un entorno limpio reproduce las comprobaciones y una regresión bloquea la integración con un error útil.

### 6.4. OPS-04 — El despliegue no documenta recuperación y preparación completa

**Prioridad: Media · Evidencia: Configuración y arranque aislado; capacidades externas no verificadas.** Ubicación: [docker-compose.yml:1](docker-compose.yml#L1), [Dockerfile:1](Dockerfile#L1), [DesktopController.cs:224](Controllers/DesktopController.cs#L224).

- **Problema:** no se encontró en el repositorio un procedimiento de backup/restauración, comprobación de salud o alerta. Una base nueva dejó el selector de sistema operativo sin opciones; se requirió sembrarlo para probar equipos.
- **Por qué mejorarlo:** poder arrancar no implica que el operador pueda registrar activos ni que se pueda recuperar el servicio tras una pérdida.
- **Mejora:** documentar catálogos mínimos, diagnóstico de conexión, comprobaciones de disponibilidad y recuperación de SQL y claves de Data Protection. Revisar además ejecución del contenedor con usuario no privilegiado; el Dockerfile no declara `USER`.
- **Aceptación:** una instalación limpia permite registrar un equipo y un ejercicio de restauración recupera datos y acceso. No se afirma que el servidor real carezca de backups.

### 6.5. OPS-05 — Las advertencias de nulabilidad reducen la utilidad del análisis

**Prioridad: Baja · Evidencia: Compilación.** Ubicación: [Desktop.cs:52](Models/Desktop.cs#L52), [DesktopService.cs:40](Services/Implementations/DesktopService.cs#L40).

- **Problema:** se observaron 25 advertencias, principalmente propiedades no inicializadas y referencias posiblemente nulas. `Desktop.Remote` se declara no nullable aunque su relación es opcional.
- **Por qué mejorarlo:** las advertencias repetidas ocultan defectos nuevos y el modelo comunica garantías que no siempre se cumplen.
- **Mejora:** distinguir relaciones opcionales, inicialización requerida y navegación cargada por EF; corregir cada aviso según su semántica, sin silenciar globalmente ni añadir `!` indiscriminadamente.
- **Aceptación:** compilación sin advertencias de nulabilidad y pruebas para entidades sin relaciones opcionales.

## 7. Orden recomendado de actuación

1. **Inmediato — control de acceso:** SEG-01, SEG-02 y SEG-03. Revocar sesiones correctamente, bloquear cuentas inactivas y conservar al menos un administrador habilitado. Incorporar sus pruebas de integración antes de corregir.
2. **Inmediato — credenciales y transporte:** SEG-04 a SEG-08. Eliminar el secreto del script, revisar su uso, proteger secretos recuperables y PDFs, cerrar HTTP externo y limitar intentos.
3. **Corto plazo — integridad:** LOG-01 a LOG-10, con prioridad en operaciones atómicas, registros reservados, destinatarios elegibles e historial de mantenimiento. LOG-06 exige revisar duplicados antes de crear restricciones.
4. **Corto plazo — operación diaria:** LOG-11/12, UX-04/05/06 e INC-01/02. Introducir devoluciones individuales y aclarar estados e identificadores; al hacerlo, actualizar consultas, métricas e historial conjuntamente.
5. **Corto plazo — verificación visual pendiente:** UX-01/02/03/07. Recuperar el navegador, probar móvil, zoom, teclado y lectores de pantalla; comprobar visualmente también los PDF.
6. **Evolutivo — crecimiento y despliegue:** REN-01/02/03, OPS-02/03/04/05, SEG-09 e INC-03/04. Medir consultas, automatizar calidad y recuperación, completar trazabilidad y mantener documentación vigente.

### Evidencia auxiliar y cierre del entorno

Los resultados de escenarios HTTP/SQL se guardaron en `obj/audit/results.json` y `obj/audit/extra-results.json`; la cobertura está en `obj/audit-results/`. Son artefactos locales ignorados por Git, no requisitos para entender este informe. El primer intento de preparación confirmó el catálogo OS vacío; el segundo reutilizó las filas ficticias ya sembradas.

Se detuvo el proceso de pruebas al terminar. La base `GestorEquipos_Audit_20260908` contiene exclusivamente datos ficticios y estados alterados intencionalmente para reproducir fallos; no debe reutilizarse como entorno operativo. Ningún resultado HTTP aquí equivale a una comprobación visual en navegador.
