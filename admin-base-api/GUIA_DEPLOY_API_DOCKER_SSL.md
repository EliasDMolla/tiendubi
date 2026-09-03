# Guía práctica: deploy de API + Postgres + SSL (Caddy)

Esta guía resume el proceso que usamos en `admin-base-api` para que lo puedas repetir con otra API.

## 1) Objetivo

Levantar en VPS:
- API .NET en Docker
- Base PostgreSQL en Docker
- Migraciones automáticas al iniciar
- HTTPS automático con Caddy + Let's Encrypt
- Health endpoint para validar uptime

---

## 2) Estructura mínima recomendada

- `Dockerfile` (API)
- `docker-compose.yml` (api + postgres + caddy)
- `.env.production` (secretos y configuración)
- `Caddyfile` (dominio HTTPS)
- Migraciones EF Core versionadas en Git

---

## 3) Requisitos previos

1. Dominio o subdominio apuntando al VPS (registro `A`)
2. Puertos `80` y `443` abiertos en el VPS
3. Docker + Docker Compose instalados
4. API con health endpoint (ej: `/api/health`)

---

## 4) Configuración de la API (.NET)

## 4.1 `Program.cs`: conexión EF con assembly de migraciones

Usar `UseNpgsql` con `MigrationsAssembly`:

```csharp
builder.Services.AddDbContext<Context>(options =>
    options.UseNpgsql(postgresConnectionString, npgsqlOptions =>
    {
        npgsqlOptions.MigrationsAssembly(typeof(Context).Assembly.GetName().Name);
        npgsqlOptions.CommandTimeout(600);
    }));
```

## 4.2 Migraciones al startup (con retry)

Antes de seeders y antes de que arranquen flujos de negocio:

```csharp
await EnsureDatabaseMigratedAsync(app);
```

Y en ese método:
- retry (ej: 12 intentos, 5s)
- `context.Database.Migrate()` si hay migraciones
- fallback opcional `EnsureCreated()` solo si no detecta migraciones en runtime
- logs claros de inicio/error

## 4.3 Worker de background no debe tumbar la app

Si usás `BackgroundService`:
- envolver `ExecuteAsync` con `try/catch`
- loguear errores y continuar
- nunca dejar excepción no controlada que pare el host

---

## 5) Migraciones EF: clave para que no falle en VPS

Asegurate de **NO ignorar** migraciones en `.gitignore`.

### Incorrecto

```gitignore
Migrations/
```

Si eso está, en VPS aparecerá:
- `No migrations were found in assembly ...`
- tablas no creadas (`relation "Users" does not exist`)

### Verificación local

```bash
git ls-files Admin.Entities/Migrations
```

Si da `0`, no están versionadas.

---

## 6) `docker-compose.yml` base (patrón)

Recomendación:
- Postgres con `healthcheck`
- API esperando `service_healthy`
- API interna (`expose`)
- Caddy publica `80/443`

```yaml
services:
  postgres:
    image: postgres:17
    restart: unless-stopped
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: ${POSTGRES_DB}
    volumes:
      - app_postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER} -d ${POSTGRES_DB}"]
      interval: 5s
      timeout: 5s
      retries: 10

  app-webapi:
    build:
      context: .
      dockerfile: Dockerfile
    restart: unless-stopped
    expose:
      - "8081"
    env_file:
      - .env.production
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      DATA_PROTECTION_KEYS_PATH: /app/keys
      ConnectionStrings__PostgresConnection: Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
    volumes:
      - app_webapi_keys:/app/keys
    depends_on:
      postgres:
        condition: service_healthy

  caddy:
    image: caddy:2
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy_data:/data
      - caddy_config:/config
    depends_on:
      - app-webapi

volumes:
  app_postgres_data:
  app_webapi_keys:
  caddy_data:
  caddy_config:
```

---

## 7) `Caddyfile` para SSL automático

```caddy
api.tudominio.com {
    reverse_proxy app-webapi:8081
}
```

Notas:
- con IP sola no hay certificado público válido
- debe resolver DNS al VPS

---

## 8) `.env.production` recomendado

Ejemplo mínimo:

```dotenv
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8081
DATA_PROTECTION_KEYS_PATH=/app/keys

POSTGRES_USER=miusuario
POSTGRES_PASSWORD=PASSWORD_FUERTE
POSTGRES_DB=miapp

ConnectionStrings__PostgresConnection=Host=postgres;Port=5432;Database=miapp;Username=miusuario;Password=PASSWORD_FUERTE
Jwt__Secret=SECRETO_LARGO_MIN_32
AppSettings__FrontendUrl=https://midominio.com
```

Tip: en producción evitá credenciales hardcodeadas dentro de `docker-compose.yml`.

---

## 9) Paso a paso de deploy (VPS)

1. `git pull`
2. `docker compose down -v` (solo si querés resetear datos)
3. `docker compose up -d --build --force-recreate`
4. Revisar logs:
   - `docker compose logs -f app-webapi`
   - `docker compose logs -f caddy`

Esperado en API:
- `Aplicando migraciones...`
- `Migraciones aplicadas correctamente.`
- `Application started.`

---

## 10) Validaciones rápidas

- Health:
  - `https://api.tudominio.com/api/health`
  - `https://api.tudominio.com/api/health/ready`
- DB existe:
  - `docker exec -it <postgres-container> psql -U <user> -l`
- Tablas:
  - `docker exec -it <postgres-container> psql -U <user> -d <db> -c "\dt"`
- Historial migraciones:
  - `docker exec -it <postgres-container> psql -U <user> -d <db> -c "SELECT * FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";"`

---

## 11) Problemas típicos y solución

### `No migrations were found in assembly`
- migraciones no versionadas
- `.gitignore` ignorando `Migrations/`
- build en VPS sin esos archivos

### `relation "Users" does not exist`
- migraciones no aplicadas
- startup consulta tablas antes de migrar

### SSL no emite certificado
- DNS aún no propagó
- puertos 80/443 cerrados
- dominio mal apuntado

### Worker rompe startup
- excepción no controlada en `BackgroundService`
- agregar `try/catch` y logueo

---

## 12) Checklist para repetir en otra API

- [ ] Migraciones EF creadas y committeadas
- [ ] `.gitignore` no bloquea `Migrations/`
- [ ] `Program.cs` con `Migrate()` al arranque + retry
- [ ] Worker tolerante a fallos
- [ ] `docker-compose` con `postgres` + `healthcheck`
- [ ] API con `depends_on: service_healthy`
- [ ] Caddy con dominio real
- [ ] DNS A -> IP VPS
- [ ] Puertos 80/443 abiertos
- [ ] Health endpoint devuelve `200`

---

Si querés, después armamos una versión 100% genérica de esta guía en una carpeta `docs/` al nivel raíz del repo para usarla en todos tus proyectos.