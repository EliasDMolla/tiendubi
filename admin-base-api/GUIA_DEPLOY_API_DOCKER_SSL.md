# Deploy de Tiendubi API en VPS

La API se ejecuta en Docker, escucha solamente en `127.0.0.1` y se publica con Nginx y HTTPS. Las migraciones de Entity Framework se aplican automáticamente al iniciar.

## Requisitos

- Ubuntu/Debian con Git y Docker Engine.
- Un contenedor PostgreSQL existente llamado `postgres`.
- Nginx y Certbot si el script administrará HTTPS.
- El DNS `A` de `api.tiendubi.com` apuntando al VPS.
- Puertos 80 y 443 abiertos. El puerto interno de la API no debe exponerse públicamente.

## Primera instalación

```bash
git clone <URL_DEL_REPOSITORIO> tiendubi
cd tiendubi/admin-base-api
cp .env.production.vps.example .env.production
chmod 600 .env.production
nano .env.production
```

Revisá especialmente:

- `DEPLOY_HOST_PORT`: debe ser un puerto libre del VPS.
- `DEPLOY_CERTBOT_EMAIL`.
- `POSTGRES_CONTAINER`, `POSTGRES_DB`, `POSTGRES_USER` y `POSTGRES_PASSWORD`.
- Credenciales de Mercado Pago, R2 y SMTP para las funciones habilitadas.

No hace falta crear manualmente el JWT: si conserva el placeholder, el script genera uno seguro y lo guarda en `.env.production`.

## Desplegar o actualizar

```bash
chmod +x deploy.sh
./deploy.sh
```

El proceso hace `git pull`, detecta el usuario administrador del contenedor PostgreSQL existente, conecta PostgreSQL y la API a la misma red Docker, crea la base y el usuario cuando sea necesario, reconstruye la imagen, recrea el contenedor, espera `/api/health` y configura Nginx/Certbot.

Si la conexión guardada usa `127.0.0.1`, `localhost` o `::1`, el script reconoce que PostgreSQL está en el VPS y la reemplaza por `Host=postgres;Port=5432`, que es la dirección válida entre contenedores.

Para desplegar sin actualizar Git:

```bash
SKIP_GIT_PULL=true ./deploy.sh
```

## Verificación

```bash
curl -f https://api.tiendubi.com/api/health
docker ps --filter name=tiendubi-webapi
docker logs -f tiendubi-webapi
```

Las claves de Data Protection y la carpeta `uploads` se guardan en volúmenes Docker y sobreviven a la recreación del contenedor. `.env.production` está excluido de Git y del contexto de construcción.

## Base externa opcional

Para usar Supabase u otro PostgreSQL administrado:

```env
DEPLOY_DATABASE_MODE=external
ConnectionStrings__PostgresConnection=Host=HOST_REMOTO;Port=5432;Database=postgres;Username=USUARIO;Password=CLAVE;SSL Mode=Require;Trust Server Certificate=true
```

En modo externo, el host no puede ser `127.0.0.1` ni `localhost` porque esas direcciones apuntan al contenedor de la API.
