# Documentación de GestorEquipos (AssetOps)

> Esta guía está escrita para cualquier persona, sin conocimientos de programación. Si en algún punto se usa una palabra técnica, se explica entre paréntesis la primera vez que aparece.

---

## 1. ¿Qué es este sistema, en una frase?

Es un programa para que una empresa controle **todo lo que pasa con sus equipos de cómputo**: qué equipos tiene, quién los está usando, qué periféricos (mouse, teclado, monitor, etc.) tienen, qué licencias de software, qué accesos remotos, cuándo se les hizo mantenimiento, y cuándo un equipo se retira de uso ("se da de baja"). También incluye una pequeña "mesa de ayuda" interna para registrar los casos de soporte TI que atiende el área de sistemas y medir qué tan satisfecha queda la gente con ese soporte.

Se usa desde un navegador (Chrome, Edge, etc.), como cualquier página web, pero solo después de iniciar sesión con un usuario y contraseña.

---

## 2. Quién puede entrar y qué puede hacer

Hay tres tipos de cuenta ("roles"):

| Rol | Qué puede hacer |
|---|---|
| **Administrador** | Todo: crear, editar y borrar equipos, usuarios, periféricos, licencias, catálogos, cuentas de acceso, etc. |
| **Técnico en Sistemas** | El trabajo operativo del día a día: crear equipos/periféricos/usuarios/licencias/mantenimientos, asignar y devolver equipos, dar de baja a RAES, etc. — pero no puede editar ni borrar nada ya creado, no puede agregar entradas nuevas a los catálogos (tipos, áreas/regionales, cargos), y no tiene ningún acceso a "Cuentas de acceso". |
| **Auditor** | Solo puede **ver** equipos, periféricos y accesos remotos (y el Tablero), más la lista de mantenimientos obligatorios ya **completados** (no ve los pendientes ni los que esperan firma). El menú le oculta todo lo demás, porque ni siquiera tiene permiso para abrir esas pantallas. |

Ver el documento **INSTRUCTIVO_USUARIO.md** para el detalle de qué puede hacer cada rol, pantalla por pantalla.

Nadie puede ver ninguna pantalla sin haber iniciado sesión primero (excepto la pantalla de login, obviamente).

**Dos cuentas especiales** se crean solas la primera vez que arranca el sistema, y nunca se pueden borrar:
- **RAES**: una "persona ficticia" a la que se le asignan los equipos que ya se dieron de baja (como una bodega de equipos retirados).
- **Disponible**: otra "persona ficticia" a la que se le asignan los equipos que no tienen un usuario real asignado todavía (como una bodega de equipos libres).

Esto evita tener que inventar reglas especiales para "equipo sin dueño" — siempre hay *alguien* (aunque sea ficticio) a quien está asignado el equipo.

---

## 3. Qué se puede hacer en cada pantalla (módulos)

| Módulo (menú) | Para qué sirve |
|---|---|
| **Tablero / Inicio** | Resumen visual (gráficas) de cuántos equipos y periféricos hay, en qué estado, y cuántos usuarios hay por regional. |
| **Equipos de escritorio** | Registrar un equipo nuevo, ver/editar sus datos, ver su "hoja de vida" completa (historial de a quién se le ha asignado, mantenimientos, licencias, accesos remotos), asignarlo a una persona, y darlo de baja (pasa a RAES). |
| **Tipos de equipo (marca/modelo)** | Catálogo de combinaciones de marca+modelo que luego se elige al registrar un equipo (por ejemplo "Dell / Optiplex 7090"). Evita escribir la marca y el modelo a mano cada vez, y evita errores de escritura. |
| **Periféricos** | Lo mismo que "Equipos de escritorio" pero para mouse, teclado, monitor, etc. Un periférico puede estar o no conectado a un equipo de escritorio, y tiene su propio estado (activo/dado de baja) independiente del equipo al que esté conectado. |
| **Tipos de periférico** | Catálogo de categorías de periférico (mouse, teclado, monitor, etc.). |
| **Mantenimiento de periféricos** | Registrar que a un periférico se le hizo un mantenimiento, usando el catálogo de "Tipos de mantenimiento". |
| **Mantenimiento de equipos** | Registrar que a un equipo de escritorio se le hizo un mantenimiento **Correctivo** (se arregló algo que falló) o **Preventivo** (revisión de rutina). El Preventivo genera automáticamente una lista de chequeo y guarda una "foto" de quién tenía el equipo y qué incluía en ese momento — esa foto no cambia después, aunque luego el equipo se reasigne. También se puede descargar un PDF con el acta del mantenimiento. |
| **Mantenimientos obligatorios** | Control de los dos mantenimientos preventivos obligatorios del año (junio y diciembre) de cada equipo activo. Muestra tres listas: **Pendientes**, **Correo enviado** y **Realizados**. Desde un pendiente se completa el formulario de mantenimiento preventivo y, al guardar, el sistema genera el acta en PDF con el formato oficial de la empresa y la envía por correo al usuario asignado al equipo, pidiéndole que la firme. Cuando alguien del área de sistemas sube de vuelta esa acta ya firmada, el ítem pasa a "Realizado". Si un mes se cierra sin completar el mantenimiento, el equipo sigue apareciendo pendiente ("atrasado") hasta que se haga. |
| **Tipos de mantenimiento** | Catálogo de categorías de mantenimiento, usado solo por el módulo de periféricos. |
| **Licencias** | Registrar qué software (y su clave, si aplica) tiene instalado un equipo, o marcar que no tiene licencia. |
| **Accesos remotos** | Registrar cómo se puede acceder remotamente a un equipo: por un aplicativo específico, o por Escritorio Remoto de Windows (con IP, puerto, usuario y contraseña). Se puede asignar a un equipo o liberar. |
| **Áreas y Regionales** | Catálogo de las áreas de la empresa y las zonas geográficas ("regionales") a las que pertenece cada persona. |
| **Cargos** | Catálogo de puestos de trabajo (ej. "Técnico en Sistemas", "Auxiliar Administrativo" — distinto del Rol de acceso al sistema). Para cada cargo se puede ver la línea de tiempo de quién lo ha tenido y desde cuándo (ej. "Daniel: 2024-01-01 → 2025-01-01, Gabriel: 2025-01-01 → Vigente"). |
| **Usuarios (personas)** | Registrar a las personas de la empresa (nombre, correo, área, regional, cargo), ver su detalle (incluyendo su historial de cargos) y desactivarlas si ya no trabajan ahí. |
| **Cuentas de acceso** | Crear la cuenta de **login** (usuario/contraseña/rol) que le permite a una persona entrar al sistema. Es distinto de "Usuarios (personas)": una persona puede existir en el sistema sin tener una cuenta de acceso para iniciar sesión. |
| **Casos TI** | Registrar un caso de soporte TI atendido a una persona (ej. "se le reinstaló el antivirus"), clasificado por tipo. Al marcarlo como **Resuelto** se le envía automáticamente un correo a la persona atendida con un enlace para que califique el servicio de 1 a 5 estrellas, sin necesidad de iniciar sesión. También incluye una pantalla de **Estadísticas** (gráficas de cuántos casos hay por tipo y a lo largo del tiempo) y otra de **Calificaciones** (promedio de cada técnico y los últimos comentarios recibidos). |
| **Tipos de caso** | Catálogo de categorías de caso de soporte TI (ej. "Hardware", "Software", "Red"), elegido al registrar un Caso TI. |

**Nota:** "Licencias" y "Mantenimiento de equipos" no tienen una pantalla de lista propia en el menú — se crean y se consultan desde dentro de la pantalla de detalle de cada equipo.

---

## 4. Cómo está construido por dentro, en palabras simples

Piensa en la aplicación como un **restaurante**:

1. **El cliente** (la persona en el navegador) pide algo, por ejemplo "quiero ver mis equipos".
2. **El mesero** (carpeta `Controllers/`) recibe el pedido. Hay un mesero por cada tipo de pedido: uno para equipos, uno para periféricos, uno para usuarios, etc.
3. El mesero no cocina nada: va a **la cocina** (carpeta `Services/`), donde está toda la lógica de negocio — las reglas, los cálculos, las validaciones (ej. "no se puede borrar un tipo de equipo si todavía hay equipos usándolo").
4. La cocina saca los ingredientes de **la despensa**: la base de datos real, a la que se llega a través de la carpeta `Models/` (que describe qué forma tienen los datos: un Equipo, un Usuario, una Licencia, etc.) y de un archivo especial (`DbContext`) que sabe "hablar" con la base de datos.
5. El mesero regresa con el plato servido: una pantalla (carpeta `Views/`) que el navegador muestra.
6. Antes de que cualquier pedido llegue al mesero, pasa por **un guardia de seguridad** (`Middleware/`) que revisa, por ejemplo, si la base de datos está funcionando — si no, le avisa amablemente al cliente en vez de mostrar un error feo.

### Tabla de carpetas

| Carpeta | Qué es, en palabras simples |
|---|---|
| `Controllers/` | Los "meseros": reciben el pedido y deciden qué pantalla mostrar después. |
| `Services/` | El "cerebro": las reglas del negocio (qué se puede y no se puede hacer). |
| `Services/Implementations/` | Las "recetas" reales — el código que de verdad hace el trabajo que promete `Services/`. |
| `Services/Auth/` | La "seguridad de la entrada": quién puede iniciar sesión, si la sesión sigue siendo válida. |
| `Services/Diagnostics/` | Un "vigilante" que anota si algo tarda mucho o si la base de datos deja de responder. |
| `Models/` | La lista de qué datos existen (Equipo, Usuario, Licencia...) y su forma. |
| `Models/ViewModels/` | Versiones simplificadas de esos datos, hechas a la medida de cada formulario/pantalla. |
| `Models/DbContext/` | El "traductor" entre el código y la base de datos real. |
| `Views/` | Las pantallas que el usuario ve en el navegador. |
| `Migrations/` | El historial de cada cambio hecho a la base de datos (como planos de remodelación, uno por cada obra). |
| `wwwroot/` | Imágenes, colores/estilos, y pequeños programas que corren en el navegador. |
| `Middleware/` | Los "guardias de seguridad" que revisan cada pedido antes de que llegue a destino. |
| `GestorEquipos.Tests/` | Pruebas automáticas que verifican que la "cocina" siga funcionando correctamente después de un cambio, sin tocar la base de datos real. |

---

## 5. Los "contratos" (una palabra técnica que pediste que se explique)

Dentro de `Services/` hay dos tipos de archivo para cada funcionalidad:

- Un archivo que empieza con **`I`** (ejemplo: `IDesktopService`) — esto es el **contrato** (también llamado "interfaz"). Es como una **descripción de puesto de trabajo**: dice *qué* debe saber hacer ese servicio ("debe poder crear un equipo", "debe poder darlo de baja"), pero no dice *cómo* lo hace.
- Un archivo en `Services/Implementations/` con el mismo nombre sin la `I` (ejemplo: `DesktopService`) — esto es quien realmente **cumple** ese contrato, con el código que hace el trabajo.

¿Para qué sirve separarlo así? Para poder cambiar "cómo se hace el trabajo" sin tener que cambiar a todos los meseros (`Controllers/`) que lo piden — a ellos solo les importa el contrato, no la receta interna. También permite, en las pruebas automáticas, usar una versión de prueba de la "cocina" sin tocar la base de datos real.

En este proyecto hay contratos (y su cumplimiento) para: equipos, tipos de equipo, asignaciones, periféricos, tipos de periférico, asignación de periféricos, mantenimiento de periféricos, accesos remotos, mantenimientos, mantenimientos obligatorios, tipos de mantenimiento, licencias, usuarios/cuentas, áreas/regionales, cargos, casos TI, tipos de caso, envío de correo, y generación de PDF (dos: uno para la hoja de vida del equipo, otro para las actas de mantenimiento).

---

## 6. Piezas externas que usa el sistema ("librerías")

Una librería es un **pedazo de código ya hecho por otra persona/empresa**, que este proyecto usa en vez de reinventarlo. Estas son las que usa, y para qué sirve cada una:

### Del lado del servidor (la parte que no se ve, hecha en C#/.NET)

| Librería | Para qué sirve, en simple |
|---|---|
| **Microsoft.EntityFrameworkCore.SqlServer** | El "traductor" principal entre el código y la base de datos SQL Server. |
| **Microsoft.EntityFrameworkCore.Design** / **.Tools** | Herramientas que permiten generar y aplicar los cambios de estructura de la base de datos (`Migrations/`). |
| **Microsoft.Data.SqlClient** | El "cable" de conexión de red hacia SQL Server. |
| **Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation** | Permite ver los cambios de una pantalla al instante mientras se programa (solo durante desarrollo, no en producción). |
| **PDFsharp-MigraDoc** | La herramienta que construye los documentos PDF (hoja de vida del equipo, actas de mantenimiento). |
| **MailKit** | Envía los correos del módulo de Mantenimientos obligatorios (el acta pidiendo firma) y del módulo de Casos TI (el aviso de caso resuelto con el enlace de calificación), a través de una cuenta de Gmail. |

### Del lado del navegador (lo que se ve y se siente)

| Librería | Para qué sirve, en simple |
|---|---|
| **Bootstrap** | Le da el estilo visual a toda la aplicación: botones, colores, cómo se acomodan las cosas en la pantalla, que se vea bien tanto en computador como en celular. |
| **jQuery** | Una herramienta clásica para hacer que la página reaccione a clics y otras acciones sin tener que recargarla completa. |
| **jQuery Validation** (+ "unobtrusive") | Revisa los formularios *antes* de enviarlos (ej. "este campo es obligatorio") para avisar al usuario de inmediato, sin esperar una respuesta del servidor. |
| **Chart.js** | Dibuja las gráficas del Tablero/Inicio. |

Todas estas librerías del navegador están guardadas directamente dentro del proyecto (carpeta `wwwroot/lib/`) en vez de descargarse de internet cada vez — así la aplicación sigue funcionando igual aunque la fuente original de esa librería desaparezca algún día.

### Para las pruebas automáticas (no es parte de la aplicación que usa la gente)

| Librería | Para qué sirve |
|---|---|
| **xUnit** | El "examinador" que corre las pruebas automáticas y dice cuáles pasaron y cuáles no. |
| **Microsoft.EntityFrameworkCore.InMemory** | Una base de datos falsa, que vive solo en la memoria de la computadora mientras corre la prueba, para no arriesgar la base de datos real. |

---

## 7. Qué hay que preparar antes de que el sistema funcione ("inicialización")

Para que esta aplicación arranque por primera vez en una computadora o servidor nuevo, hay varias cosas que deben configurarse de antemano. Ninguna de ellas viene "ya lista" dentro del código, por seguridad (contraseñas) o porque dependen de cada instalación (dirección del servidor).

1. **Dónde está la base de datos** — el archivo `appsettings.json` necesita la dirección del servidor SQL Server (`ConnectionStrings:DefaultConnection`). Cada computadora/servidor tiene la suya.
2. **Crear las tablas de la base de datos** — se ejecuta un comando (`dotnet ef database update`) que aplica, en orden, todo el historial guardado en `Migrations/`. Si la base de datos está vacía, este paso la deja con todas las tablas necesarias.
3. **El primer administrador** — la primera vez que el sistema arranca contra una base de datos sin ningún administrador, crea uno automáticamente usando un correo y contraseña que se le deben indicar por fuera del código (nunca se escriben directamente en los archivos del proyecto, para que no queden expuestos):
   - En una computadora de desarrollo: con el comando `dotnet user-secrets set "BootstrapAdmin:Email" "..."` (y lo mismo para la contraseña).
   - En el servidor con Docker: en un archivo `.env` (que no se sube nunca al repositorio) con `BOOTSTRAP_ADMIN_EMAIL` y `BOOTSTRAP_ADMIN_PASSWORD`.
4. **Los roles y las cuentas especiales** — el propio sistema, al arrancar, se asegura de que existan los roles `Administrador`/`Auditor` y las cuentas ficticias RAES/"Disponible" explicadas en la sección 2. Esto no requiere que nadie haga nada manualmente.
5. **Catálogos que no tienen pantalla propia** — `Ram` (tipos de memoria) y `OSVersion` (versiones de sistema operativo) no tienen un formulario de "crear" dentro de la aplicación. Si la base de datos es nueva, alguien con acceso directo a la base de datos debe insertar al menos algunos valores ahí manualmente, o el formulario de "crear equipo" se va a ver con esas listas vacías. (Área, Regional, Tipos de equipo/periférico/mantenimiento y Cargos sí se crean desde la propia aplicación — un Administrador los agrega la primera vez que los necesite.)
6. **La cuenta de correo para Mantenimientos obligatorios y Casos TI** — para que el botón "Completar y enviar al correo" (Mantenimientos obligatorios) y el botón "Resolver" (Casos TI) funcionen, hace falta una cuenta Gmail con verificación en dos pasos activada y una "contraseña de aplicación" generada en `myaccount.google.com/apppasswords`. El correo (no es secreto) va en `appsettings.json` → `Smtp:FromAddress`; la contraseña de aplicación (sí es secreta) se guarda aparte, igual que la del primer administrador: `dotnet user-secrets set "Smtp:Password" "..."` en desarrollo, o `SMTP_APP_PASSWORD` en el `.env` de Docker. Si no se configura, el resto del sistema funciona igual — solo falla al intentar enviar un correo desde esos dos módulos.
7. **Si se despliega con Docker** — además de los puntos anteriores, hace falta:
   - Un archivo `.env` (copiado de `.env.example`) con el usuario/contraseña del SQL Server (`LOCAL_SQL_USER`/`LOCAL_SQL_PASSWORD`).
   - El contenedor espera que el SQL Server ya esté corriendo en la máquina anfitriona (no crea uno propio dentro de Docker).
   - Un espacio de almacenamiento ("volumen") para que las "llaves" que firman las cookies de sesión no se pierdan si el contenedor se reinicia — si se pierden, todas las sesiones activas se invalidarían de golpe.

En resumen: **antes del primer uso**, hace falta decirle al sistema dónde está la base de datos, crear las tablas, darle un correo/contraseña para el primer administrador, y llenar a mano los catálogos básicos (Ram, sistema operativo, áreas, regionales). La cuenta de correo (punto 6) solo es necesaria si se va a usar el módulo de Mantenimientos obligatorios. Después de eso, todo lo demás (otros administradores, equipos, usuarios, etc.) se crea desde la propia aplicación.

---

## 8. Seguridad, en palabras simples

- Nadie entra sin usuario y contraseña, y cada clic dentro del sistema se vuelve a comprobar contra la base de datos (no basta con haber iniciado sesión una vez — si alguien desactiva tu cuenta o te cambia el rol, se te cierra la sesión automáticamente en el siguiente clic).
- Hay un límite de 10 intentos de inicio de sesión por minuto desde la misma conexión, para dificultar que alguien intente adivinar contraseñas a la fuerza.
- Las acciones de borrar algo importante (una cuenta, un catálogo, etc.) exigen que un administrador vuelva a escribir su propia contraseña, como doble confirmación.
- Si la base de datos queda momentáneamente caída, el sistema muestra un mensaje de "intenta de nuevo en un momento" en vez de cerrar la sesión de todos o mostrar un error confuso.
