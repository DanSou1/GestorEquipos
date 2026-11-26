# Despliegue en el servidor de pruebas (Proxmox)

Runbook técnico del servidor de pruebas donde corre GestorEquipos en Docker. Para la documentación funcional del sistema ver `DOCUMENTACION.md`; para guía de uso por rol ver `INSTRUCTIVO_USUARIO.md`. Este documento es para quien necesite actualizar o diagnosticar el servidor (incluido Claude Code en una sesión futura).

**Nunca pongas contraseñas, tokens ni llaves privadas reales en este archivo** — está versionado en git. Donde haga falta un valor sensible, este documento dice *dónde encontrarlo*, no cuál es.

---

## 1. Topología

```
Internet
   │
   ▼
extranet.exro.co (177.253.96.106) — NAT/firewall de la empresa, fuera de nuestro control
   │
   ├── puerto 80   → otro servidor (Apache/CentOS), no relacionado con este proyecto
   ├── puerto 443  → otro servidor (nginx Ubuntu, página default), no relacionado con este proyecto
   └── puerto 8084 → VM GestorEquipos (192.168.1.10), puerto 8080
                        │
                        ▼
                   Nginx (reverse proxy + TLS)
                        │
                        ▼
                   contenedor "web" (Kestrel, loopback:8081)
                        │
                        ▼
                   contenedor "db" (SQL Server 2025, puerto 1433)
```

**Dato importante:** hoy el único camino público hacia esta VM es `https://extranet.exro.co:8084/`. El dominio sin puerto (`:80`/`:443`) no llega aquí — llega a otros servidores de la empresa. Esto es así porque el NAT de borde solo tiene reenviado ese puerto hacia esta VM; no se puede arreglar desde la VM misma, hace falta que alguien con acceso al router/firewall de la empresa reenvíe los puertos 80/443 reales hacia `192.168.1.10`.

## 2. Acceso a la infraestructura

### 2.1. Proxmox (hipervisor)

- URL: `https://192.168.1.5:8006/`
- Nodo: `data`
- VM: **ID 101, nombre "GestorEquipos"**
- Login: `root@pam` (credenciales del usuario, no documentadas aquí)
- El certificado de Proxmox es autofirmado — el navegador mostrará advertencia de "conexión no privada" la primera vez, hay que aceptarla manualmente (un navegador automatizado no puede saltarse esa pantalla).

**Layout de teclado de la consola:** Centro de datos → Opciones → "Disposición del teclado" debe estar en **English (USA)**, no en Spanish. Con el layout en español, cualquier automatización que escriba en la consola de la VM corrompe símbolos (`-`, `/`, `~`, `&`, mayúsculas) porque Proxmox traduce las teclas como si vinieran de un teclado español. Si alguna vez hay que escribir algo en la consola gráfica (en vez de por SSH) y empieza a salir basura, revisar esto primero.

Alternativa cuando ni el teclado US alcanza (ej. para pegar texto largo sin que Chrome bloquee el `paste`): desde el **Shell del nodo** (Centro de datos → `data` → Consola, distinto de la consola de la VM) se puede usar `qm sendkey 101 <tecla>` para enviar teclas una por una directamente a la VM vía QMP, sin pasar por el lienzo VNC del navegador.

### 2.2. La VM (`192.168.1.10`)

- SO: Ubuntu 26.04.1 LTS
- Usuario: `admin1`
- **Sudo sin contraseña ya configurado** para `admin1` (`/etc/sudoers.d/admin1-nopasswd`) — necesario para instalar paquetes (nginx, certbot) y administrar Docker sin pedir la contraseña cada vez.
- Docker Engine + plugin Compose v2 ya instalados.
- Nginx y certbot ya instalados (`apt`).
- Repo clonado en `~/GestorEquipos` (`git clone https://github.com/DanSou1/GestorEquipos.git` — repo público, no requiere credenciales).

**Acceso SSH:** se generó una llave ed25519 dedicada y su clave pública ya está en `/home/admin1/.ssh/authorized_keys`. La clave **privada** vivió solo en el `scratchpad` temporal de la sesión que la generó (una carpeta que se borra entre sesiones de Claude Code) — **no va a estar disponible en una sesión nueva**. Para una sesión futura, dos opciones:
1. El usuario conserva una copia de esa llave privada en un lugar durable y se la pasa a Claude al inicio.
2. Generar una llave nueva y agregar su clave pública a `~/.ssh/authorized_keys` de `admin1` — con el sudo sin contraseña y el teclado en inglés ya configurados, esto se puede hacer directo por la consola de Proxmox sin mayor fricción (ya no hay que lidiar con el bug de teclado para escribir el `echo ... >> authorized_keys`).

Si no hay llave SSH disponible y hace falta hacerlo todo por la consola gráfica de Proxmox, recordar el punto 2.1 (layout de teclado + `qm sendkey` como respaldo).

### 2.3. Credenciales y dónde viven

| Qué | Dónde está |
|---|---|
| `SA_PASSWORD`, `LOCAL_SQL_USER`, `LOCAL_SQL_PASSWORD` | `~/GestorEquipos/.env` en la VM (no versionado) |
| `BOOTSTRAP_ADMIN_EMAIL`/`PASSWORD` | Mismo `.env` — **sin efecto hoy**, porque la base de datos ya tiene datos reales migrados (ver sección 5), y el bootstrap de admin solo actúa la primera vez que la BD no tiene ningún admin |
| `SMTP_FROM_ADDRESS`/`SMTP_APP_PASSWORD` | Mismo `.env` — correo real de `sistemasquimexcolexro@gmail.com`, contraseña de aplicación de Gmail |
| Credenciales reales de `administrador@exro.co` y el resto del personal | Dentro de la base de datos migrada (hashes), no en ningún archivo de config — cada quien usa su contraseña habitual |
| Admin de prueba local (`admin@test.local`) | Solo existía en los datos de prueba previos a la migración de la sección 5; ya no es la cuenta "real" a usar en este servidor |

**Nota sobre `LOCAL_SQL_USER`:** hoy en esta VM vale `sa` (no el login dedicado `gestorequipos_docker` que sugiere `.env.example`/`scripts/create-docker-sql-login.sql`). Funciona, pero significa que la app se conecta a SQL Server como `sa`. Crear el login dedicado y migrar la connection string es una mejora pendiente, no urgente.

## 3. Archivos que existen SOLO en la VM (no están en git)

Estos tres elementos hay que **reconstruirlos a mano** si la VM se recrea desde cero (no vienen del `git clone`):

### 3.1. `~/GestorEquipos/.env`

Ver `.env.example` para la lista de variables. Los valores reales están en la VM, no en el repo.

### 3.2. `~/GestorEquipos/docker-compose.net-override.yml`

Necesario porque el NAT externo reenvía el puerto público `8084` directo al puerto `8080` de la VM (ver sección 1) — sin este archivo, ese tráfico llegaría directo al contenedor `web` por HTTP plano, sin pasar por Nginx/TLS.

```yaml
services:
  web:
    ports: !override
      - "127.0.0.1:8081:8080"
    environment:
      ForwardedHeaders__KnownProxies__0: "172.18.0.1"
```

- `ports: !override` reemplaza el mapeo de puertos (en vez de agregarse a los de `docker-compose.yml`) — el contenedor deja de publicar el 8080 en todas las interfaces y pasa a escuchar solo en loopback:8081, dejando el 8080 real libre para que Nginx lo tome con TLS.
- `ForwardedHeaders__KnownProxies__0` es la IP del gateway del bridge de Docker del proyecto (`docker network inspect gestorequipos_default --format '{{range .IPAM.Config}}{{.Gateway}}{{end}}'` — hoy `172.18.0.1`, pero **puede cambiar** si el proyecto Compose se recrea desde cero; si después de un redeploy completo el login empieza a redirigir a `http://` en vez de `https://`, es casi seguro que esta IP cambió y hay que volver a consultarla). Es necesaria porque Nginx corre en el host y se conecta al contenedor a través del bridge de Docker, no por `127.0.0.1` real — y ASP.NET Core solo confía en el header `X-Forwarded-Proto` si viene de una IP conocida.

**Toda invocación de `docker compose` en esta VM debe incluir los tres archivos:**
```
docker compose -f docker-compose.yml -f docker-compose.prod.yml -f docker-compose.net-override.yml <comando>
```

### 3.3. Configuración de Nginx

`/etc/nginx/sites-available/gestorequipos` (symlink en `/etc/nginx/sites-enabled/gestorequipos`, se borró el `default`):

```nginx
server {
    listen 80;
    server_name extranet.exro.co 192.168.1.10;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl;
    listen 8080 ssl;
    server_name extranet.exro.co 192.168.1.10;

    ssl_certificate /etc/nginx/ssl/selfsigned.crt;
    ssl_certificate_key /etc/nginx/ssl/selfsigned.key;

    location / {
        proxy_pass http://127.0.0.1:8081;
        proxy_set_header Host $http_host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

**Detalle crítico:** `proxy_set_header Host $http_host;` — **no** `$host`. La variable `$host` de Nginx recorta el puerto del encabezado `Host`; como se accede por el puerto no estándar `:8084`, usar `$host` rompe las URLs absolutas que genera la app (por ejemplo, el redirect de "no autenticado" hacia `/Login/Index`), porque terminan apuntando a `http://extranet.exro.co/...` sin el `:8084` — y eso cae en otro servidor completamente distinto (ver sección 1).

Certificado autofirmado temporal (listen 8080/443 ssl arriba lo usa):
```
sudo openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout /etc/nginx/ssl/selfsigned.key -out /etc/nginx/ssl/selfsigned.crt \
  -subj '/CN=extranet.exro.co'
```

Cuando el NAT de la empresa reenvíe los puertos 80/443 reales hacia esta VM, reemplazar este certificado por uno real:
```
sudo certbot --nginx -d extranet.exro.co
```
(certbot ya está instalado). En ese momento también se puede simplificar el `docker-compose.net-override.yml` si ya no hace falta el puerto 8084/8080 especial.

## 4. Cómo actualizar el servidor después de un cambio de código

Flujo estándar para subir cualquier corrección al servidor de pruebas:

1. **En tu máquina:** confirmar que los tests pasan (`dotnet test GestorEquipos.Tests/GestorEquipos.Tests.csproj`) y hacer commit + push a `main`.
2. **Conectarse a la VM** por SSH (ver sección 2.2 sobre la llave) o, si no hay llave disponible, por la consola de Proxmox (sección 2.1).
3. **Traer el código nuevo:**
   ```
   cd ~/GestorEquipos && git pull
   ```
4. **Reconstruir la imagen de la app** (solo `web`, nunca hace falta tocar `db` para un cambio de código):
   ```
   docker compose -f docker-compose.yml -f docker-compose.prod.yml -f docker-compose.net-override.yml build web
   ```
5. **Recrear el contenedor:**
   ```
   docker compose -f docker-compose.yml -f docker-compose.prod.yml -f docker-compose.net-override.yml up -d web
   ```
6. **Verificar:**
   ```
   docker compose -f docker-compose.yml -f docker-compose.prod.yml -f docker-compose.net-override.yml ps
   docker compose -f docker-compose.yml -f docker-compose.prod.yml -f docker-compose.net-override.yml logs web --tail=40
   ```
   Confirmar que ambos contenedores están `Up`/`healthy` y que no hay excepciones en el arranque. Después, probar manualmente en `https://extranet.exro.co:8084/` (login + alguna pantalla que dependa de la BD).

**Migraciones de base de datos:** no requieren ningún paso manual. `AuthBootstrapper.EnsureAdminAsync` corre `dbContext.Database.MigrateAsync()` cada vez que arranca la app — si el `git pull` trajo una migración nueva de EF Core, se aplica sola al recrear el contenedor `web` en el paso 5.

**Si algo sale mal tras un deploy:** `docker compose ... logs web` primero. Si el problema es de red/redirects raros (vuelve a aparecer el patrón "cae en otro servidor" o "redirige a http sin el puerto"), revisar primero si `172.18.0.1` en `docker-compose.net-override.yml` sigue siendo la IP correcta del bridge de Docker (sección 3.2) — es el tipo de cosa que se rompe silenciosamente si el stack de Compose se recreó desde cero en vez de solo el contenedor `web`.

## 5. Datos de la base de datos

La base de datos del servidor (`AssetOps`, dentro del contenedor `db`) tiene los **datos reales de producción**, migrados desde el SQL Server local del desarrollador (backup/restore nativo de SQL Server) — no son datos de prueba. Incluye las 9 cuentas reales (`administrador@exro.co` y el resto del personal), las personas, equipos, periféricos y casos TI reales.

**Antes de cualquier operación riesgosa sobre la BD** (restore, migración de esquema manual, etc.), conviene sacar un backup rápido:
```
SAPW=$(grep -oP '(?<=^SA_PASSWORD=).*' ~/GestorEquipos/.env)
docker exec gestorequipos-db-1 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$SAPW" \
  -Q "BACKUP DATABASE AssetOps TO DISK = N'/var/opt/mssql/data/AssetOps_backup.bak';"
```
(y copiarlo fuera del contenedor con `docker cp` si se quiere conservar en otro lado).

## 6. Problemas ya resueltos (para no volver a perder tiempo en ellos)

- **Login en bucle infinito de redirects:** el volumen `dataprotection_keys` tenía permisos de `root` en vez del usuario `appuser` (uid 1000) con el que corre el contenedor — rompía la generación del token antiforgery de la vista de Login. Se arregló con:
  ```
  docker run --rm -v gestorequipos_dataprotection_keys:/data busybox chown -R 1000:1000 /data
  ```
- **Login redirige a `http://` sin el puerto `:8084` y cae en un 404 de otro servidor:** ver sección 3.3 (`$host` vs `$http_host`) y sección 3.2 (`ForwardedHeaders__KnownProxies`).
- **No se puede recrear a alguien dado de baja con el mismo correo:** resuelto en el código (`UserAdminService.CreateAccountAsync` reactiva en vez de rechazar si la persona existente está inactiva) — no es un problema de infraestructura, ver el historial de commits.
