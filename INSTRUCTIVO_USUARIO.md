# Instructivo de uso — GestorEquipos (AssetOps)

> Guía práctica de "cómo hacer cada cosa" en el sistema, organizada según tu rol. Si no sabes cuál es tu rol, mira la esquina inferior izquierda del menú: ahí aparece tu nombre y, debajo, "Admin", "Técnico" o "Auditor".

---

## 1. Cómo iniciar sesión

1. Abre el sistema en el navegador (te darán la dirección/URL).
2. Ingresa tu **usuario** y **contraseña** y haz clic en **Entrar**.
3. Si te equivocas de contraseña muchas veces seguidas, el sistema te bloqueará los intentos por un minuto, por seguridad — espera y vuelve a intentar.
4. Para salir, usa el botón **Cerrar sesión** al final del menú izquierdo.

---

## 2. Resumen rápido: qué puede hacer cada rol

| Acción | Administrador | Técnico en Sistemas | Auditor |
|---|:---:|:---:|:---:|
| Ver el Tablero | ✅ | ✅ | ✅ |
| Ver Equipos, Periféricos y Accesos remotos | ✅ | ✅ | ✅ |
| Ver Mantenimientos obligatorios | ✅ (todo) | ✅ (todo) | ✅ (solo los ya "Realizados") |
| Ver Usuarios, Cargos y los catálogos de Tipos/Áreas/Regionales | ✅ | ✅ | ❌ (esas pantallas no le aparecen en el menú) |
| Crear equipo, periférico, usuario, licencia o mantenimiento | ✅ | ✅ | ❌ |
| Ver Casos TI, Estadísticas y Calificaciones | ✅ (todos los casos/técnicos) | ✅ (solo sus propios casos) | ✅ (solo casos ya Resueltos) |
| Registrar y Resolver un Caso TI | ✅ | ✅ (solo los que él mismo registró) | ❌ |
| Completar un mantenimiento obligatorio y enviarlo por correo / subir el acta firmada | ✅ | ✅ | ❌ |
| Asignar/devolver equipo, reasignar/dar de baja periférico, asignar/liberar acceso remoto, desactivar persona | ✅ | ✅ | ❌ |
| Agregar un tipo/área/regional/cargo/tipo de caso **nuevo** al catálogo | ✅ | ❌ | ❌ |
| **Editar** cualquier registro ya creado | ✅ | ❌ | ❌ |
| **Eliminar** cualquier registro | ✅ | ❌ | ❌ |
| Ver la contraseña de un acceso remoto | ✅ | ✅ | ❌ |
| Cuentas de acceso (usuario/contraseña/rol de login) | ✅ | ❌ (ni siquiera puede ver esa pantalla) | ❌ |

En corto: el **Técnico en Sistemas** hace el trabajo operativo de todos los días (registrar y mover equipos) pero no puede corregir ni borrar lo ya creado — eso queda para Administrador. El **Auditor** solo mira equipos, periféricos y accesos remotos, nada más.

Si al hacer clic en algo te aparece una página de "Acceso denegado", es porque tu rol no tiene permiso para esa acción — no es un error del sistema.

---

## 3. Instructivo para Administrador

El Administrador puede hacer todo lo que describe esta sección, más las de Técnico y Auditor.

### 3.1 Equipos de escritorio

- **Registrar un equipo nuevo**: menú **Equipos** → botón **Nuevo equipo** → completa Nombre, Serial, Marca/Modelo, Procesador, Disco, Sistema operativo, RAM, y opcionalmente un acceso remoto nuevo → **Guardar**.
- **Ver la hoja de vida de un equipo**: **Equipos** → clic en **Ver hoja de vida** sobre la fila del equipo. Ahí ves sus especificaciones, a quién está asignado, su historial de asignaciones, licencias, periféricos conectados, mantenimientos y cambios de especificaciones — y puedes descargar un PDF con toda esta información.
- **Editar las especificaciones**: desde la hoja de vida → botón **Editar**.
- **Asignar el equipo a una persona**: desde la hoja de vida → botón **Asignar** → elige la persona → **Asignar**.
- **Devolver un equipo** (queda "Disponible" sin tocar el resto de equipos de esa persona): desde la hoja de vida → botón **Devolver** (solo aparece si está asignado a alguien activo).
- **Dar de baja a RAES**: desde la hoja de vida → botón **Enviar a RAES**. Es reversible solo en el sentido de que el equipo sigue existiendo en el sistema, pero la acción en sí no tiene un botón de "deshacer".
- **Eliminar la hoja de vida por completo** (borra el registro de la base de datos): desde la hoja de vida → botón **Eliminar**. Úsalo con cuidado — a diferencia de "Enviar a RAES", esto no deja rastro.

### 3.2 Periféricos

- **Registrar uno nuevo**: desde la hoja de vida de un equipo → **Agregar periférico** (o desde **Periféricos** si ya existe un periférico sin equipo). Completa Tipo, Marca, Modelo, Serial.
- **Ver detalle, reasignar, editar, dar de baja o eliminar**: igual que un equipo — desde **Periféricos** → **Ver detalle**, o desde el periférico dentro de la hoja de vida de su equipo.

### 3.3 Licencias y mantenimientos

- **Agregar una licencia a un equipo**: desde la hoja de vida del equipo → **Agregar licencia** → Software (y clave, o marcar "Sin licencia").
- **Registrar un mantenimiento**: desde la hoja de vida del equipo → **Registrar mantenimiento** → elige **Correctivo** o **Preventivo**, fecha, técnico, descripción. El Preventivo pide además una lista de chequeo. Después puedes **Descargar PDF** del acta.
- **Mantenimiento de periféricos**: desde el detalle del periférico → **Registrar mantenimiento**, eligiendo un Tipo de mantenimiento del catálogo.

### 3.3.1 Mantenimientos obligatorios (junio y diciembre)

Menú **Equipos → Mantenimientos obligatorios**. El sistema arma solo, cada vez que entras a esta pantalla, la lista de equipos activos que deben pasar su mantenimiento preventivo obligatorio de junio o diciembre de este año. Verás tres listas:

- **Pendientes**: haz clic en **Completar y enviar** → se abre el mismo formulario de mantenimiento preventivo (con su lista de chequeo) → al **Guardar**, el sistema crea el mantenimiento, genera el acta en PDF con el formato oficial de la empresa, y la envía por correo al usuario que tiene asignado el equipo, pidiéndole que confirme con su firma. El equipo pasa a la lista "Correo enviado".
  - Si el equipo no tiene a nadie asignado (o la persona asignada no tiene correo), el sistema no deja completar el mantenimiento por esta vía hasta que se le asigne una persona con correo válido.
  - Si un equipo no se completa dentro de su mes, **no desaparece**: sigue apareciendo como pendiente ("Atrasado") en los meses siguientes hasta que se haga.
- **Correo enviado**: equipos que ya recibieron su acta por correo y están esperando que el usuario la firme y la devuelva (por correo o en persona). Cuando tengas esa acta firmada en tus manos (como PDF), haz clic en **Subir PDF firmado** y adjúntala — el equipo pasa a "Realizado".
- **Realizados**: el historial de mantenimientos obligatorios ya cerrados, con la fecha en que se subió el acta firmada. Se puede descargar esa acta en cualquier momento con **Descargar PDF firmado**.

### 3.4 Accesos remotos

- Un acceso remoto se crea **al crear o editar un equipo** (opción "Nuevo acceso remoto" dentro del formulario), no desde una pantalla aparte.
- **Asignar/liberar un acceso remoto existente a otro equipo**: menú **Usuarios → Accesos remotos** → entra al acceso → **Asignar a equipo** o **Liberar**.
- **Editar sus datos (IP, usuario, contraseña, etc.) o eliminarlo**: desde la misma pantalla → **Editar** / **Eliminar**.

### 3.5 Catálogos (Tipos de equipo, Tipos de periférico, Tipos de mantenimiento, Áreas y Regionales, Cargos)

Todos siguen el mismo patrón, bajo **Usuarios** o **Equipos/Periféricos** según corresponda en el menú:

1. Entra al catálogo (ej. **Equipos → Tipos de equipo**).
2. **Nuevo tipo/área/regional/cargo** → escribe el nombre → **Guardar**. El sistema no deja crear dos con el mismo nombre (sin importar mayúsculas/minúsculas).
3. **Editar** corrige el nombre; **Eliminar** pide tu contraseña de Administrador como confirmación, y el sistema no deja eliminar un catálogo que ya esté en uso (ej. un Tipo de equipo con equipos registrados, o un Cargo que alguien haya tenido).

**Cargos** tiene además un botón **Ver historial** por cada cargo: muestra la línea de tiempo de qué personas lo han tenido y en qué fechas (la persona sin fecha de fin está "Vigente" en ese cargo).

### 3.5.1 Casos TI (mesa de ayuda)

Menú **Casos TI**.

- **Registrar un caso**: **Casos TI → Registrar caso** → elige la Persona atendida, el Tipo de caso, la fecha y escribe tus notas. Como Administrador también eliges quién es el Técnico que atendió el caso (el Técnico en Sistemas no ve este campo: el caso queda registrado a su propio nombre automáticamente).
- **Ver el listado**: **Casos TI → Casos**. Muestra fecha, técnico, persona atendida, tipo y estado (Abierto/Resuelto).
- **Resolver un caso**: entra al caso → escribe las notas de resolución → **Resolver**. Esto envía automáticamente un correo a la persona atendida avisándole que su caso se resolvió, con un enlace para que califique el servicio de 1 a 5 estrellas (sin necesidad de iniciar sesión). Si la persona no tiene un correo válido registrado, el sistema no deja resolver el caso hasta que se le agregue uno (menú Usuarios → Editar).
- **Estadísticas**: **Casos TI → Estadísticas** — gráficas de cuántos casos hay por tipo (torta) y a lo largo del tiempo (barra, agrupable por día de la semana/semana/mes/fecha exacta), con filtro opcional de técnico y de mes/año.
- **Calificaciones**: **Casos TI → Calificaciones** — el promedio de estrellas de cada técnico y los últimos comentarios que dejó la gente al calificar.

### 3.6 Usuarios (las personas) y sus cuentas de acceso

- **Registrar una persona nueva**: menú **Usuarios** → **Nuevo usuario** → Nombre, correo, Área, Regional y Cargo (opcionales, salvo nombre/correo). Si el cargo que necesitas no está en la lista, haz clic en el botón **+** junto al selector de Cargo, escribe el nombre y **Agregar** — queda disponible al instante, sin salir del formulario.
- **Editar una persona** (incluyendo cambiarle el cargo): desde su hoja de vida → **Editar**. Si ya tiene un cargo asignado, solo puedes cambiarlo por otro — no puedes dejarlo "sin cargo" otra vez; cada cambio queda guardado en su historial de cargos, visible en su hoja de vida.
- **Desactivar una persona** (no la borra; libera todos sus equipos y periféricos hacia "Disponible"): desde su hoja de vida o el listado → **Eliminar** (el nombre del botón dice "Eliminar" pero la acción real es desactivar — la persona y todo su historial se conservan).
- **Crear su cuenta de acceso al sistema** (usuario/contraseña/rol): menú **Usuarios → Cuentas de acceso** → **Crear**. Esto es distinto de registrar la persona — una persona puede existir en el sistema sin tener cuenta para iniciar sesión.
- **Cambiarle la contraseña o el rol, o eliminar su cuenta de acceso**: desde **Cuentas de acceso** → **Editar** / **Eliminar** (pide tu contraseña de Administrador).

---

## 4. Instructivo para Técnico en Sistemas

Puedes hacer lo siguiente — en todos los casos, los pasos son los mismos que se describen arriba para Administrador, simplemente no verás los botones "Editar" ni "Eliminar" (ni el "+" para agregar un catálogo nuevo):

- **Equipos**: registrar uno nuevo, asignarlo a una persona, devolverlo, enviarlo a RAES.
- **Periféricos**: registrarlo, reasignarlo, enviarlo a RAES.
- **Licencias**: agregar una licencia a un equipo.
- **Mantenimientos**: registrar un mantenimiento de equipo o de periférico.
- **Mantenimientos obligatorios**: igual que Administrador — completar un pendiente y enviar su acta por correo, y subir el acta firmada cuando vuelva (ver sección 3.3.1).
- **Accesos remotos**: asignar uno existente a un equipo, o liberarlo (incluyendo ver su contraseña, para poder usarlo). No puedes crear uno desde cero fuera de "Nuevo equipo"/no puedes editar sus datos.
- **Usuarios**: registrar una persona nueva (eligiendo su Cargo de la lista existente — si no está, pide a un Administrador que lo agregue primero) y desactivarla cuando corresponda.
- **Catálogos y Cargos**: puedes **consultar** Tipos de equipo/periférico/mantenimiento/caso, Áreas/Regionales y Cargos (incluyendo el historial de un cargo), pero no agregar ni editar ni eliminar entradas ahí.
- **Casos TI**: puedes registrar un caso (siempre a tu propio nombre como técnico, no eliges a otro) y resolverlo (ver sección 3.5.1), pero el listado, Estadísticas y Calificaciones solo te muestran tus propios casos — no los de otros técnicos.

Lo que **no** puedes hacer (y por qué no verás esos botones): editar o eliminar cualquier registro ya creado, agregar una entrada nueva a un catálogo, y nada relacionado con "Cuentas de acceso" (esa pantalla ni siquiera te aparece en el menú) — esas acciones quedan reservadas a Administrador.

---

## 5. Instructivo para Auditor

Tu acceso es de solo consulta, y solo sobre tres pantallas:

- **Tablero**: el resumen general con las gráficas.
- **Equipos**: puedes ver el listado y entrar a la hoja de vida de cualquier equipo (especificaciones, historial, licencias, periféricos, mantenimientos) y descargar su PDF — pero no verás botones de Editar/Asignar/Eliminar.
- **Periféricos**: igual, solo consulta.
- **Accesos remotos**: puedes ver el listado y el detalle, pero no la contraseña del acceso (esa queda oculta para tu rol).
- **Mantenimientos obligatorios**: solo ves la lista de **Realizados** (los que ya tienen su acta firmada subida) — no ves los pendientes ni los que están esperando firma, y no puedes completar ni subir nada ahí.
- **Casos TI**: en el listado, Estadísticas y Calificaciones solo ves los casos ya **Resueltos** de todos los técnicos — no ves los que siguen Abiertos, y no puedes registrar ni resolver ninguno.

No vas a ver en el menú "Usuarios", "Cargos", ni ninguno de los catálogos — no es un descuido, tu rol no tiene permiso para abrir esas pantallas.

---

## 6. Preguntas frecuentes

**¿Por qué no veo el botón para editar/borrar algo?**
Porque tu rol no tiene ese permiso (ver la tabla de la sección 2). Pídele a un Administrador que lo haga, o que te explique si debería tener ese acceso.

**Intenté entrar a una pantalla escribiendo la dirección directamente y me salió "Acceso denegado".**
Es intencional — el sistema revisa el permiso en el servidor, no solo escondiendo el botón. Es normal si tu rol no incluye esa acción.

**¿Qué diferencia hay entre "Usuarios" y "Cuentas de acceso"?**
"Usuarios" son las personas de la empresa (con o sin acceso al sistema). "Cuentas de acceso" es específicamente el usuario/contraseña/rol que le permite a alguien iniciar sesión — una persona puede existir sin tener una.

**¿Qué diferencia hay entre el "Cargo" de una persona y su "Rol"?**
El Cargo es su puesto de trabajo en la empresa (ej. Técnico en Sistemas, Auxiliar Administrativo) y es solo informativo/histórico. El Rol es el permiso que tiene su cuenta de acceso dentro de este sistema (Administrador/Técnico en Sistemas/Auditor). Pueden coincidir en el nombre por casualidad, pero son dos cosas completamente distintas.

**Me equivoqué al escribir mi contraseña varias veces y ahora no me deja intentar de nuevo.**
Es un límite de seguridad (máximo de intentos por minuto desde la misma conexión). Espera un minuto y vuelve a intentar.

**Completé un mantenimiento obligatorio pero no me deja, dice que el equipo no tiene usuario asignado con correo válido.**
El acta se envía por correo a la persona que tiene asignado el equipo en ese momento. Si el equipo está "Disponible" (sin nadie asignado) o la persona no tiene correo cargado, primero hay que asignarlo a alguien con correo válido (menú Equipos → hoja de vida del equipo → Asignar) y luego volver a intentar.

**Un equipo quedó "Correo enviado" hace tiempo y todavía no aparece como Realizado, ¿qué hago?**
Significa que todavía no se ha subido el acta firmada que te devolvió el usuario. En cuanto la tengas en PDF, entra a Mantenimientos obligatorios → esa fila → **Subir PDF firmado**.

**Quiero resolver un Caso TI pero el sistema no me deja, dice que la persona no tiene correo válido.**
Igual que en Mantenimientos obligatorios: el correo de "caso resuelto" (con el enlace de calificación) se envía a la persona atendida. Si no tiene correo cargado, agrégalo primero desde menú Usuarios → esa persona → Editar, y luego vuelve a intentar resolver el caso.

**¿Quién puede calificar un Caso TI, y cómo?**
Solo la persona que fue atendida, usando el enlace que le llega por correo cuando el caso se marca como Resuelto — no necesita usuario ni contraseña del sistema para calificar. Esa calificación solo se puede enviar una vez por caso.
