# Deploy de Tiendubi API en VPS

La API se publica en Docker, escucha solamente en `127.0.0.1:5002` y queda expuesta por Nginx con HTTPS. Las migraciones de Entity Framework se aplican automáticamente al iniciar.

## Requisitos

- Ubuntu/Debian con Git, Docker Engine y Docker Compose v2.
- Nginx y Certbot si el script administrará HTTPS.
- DNS `A` de `api.tiendubi.com` apuntando al VPS.
- PostgreSQL externo (por ejemplo Supabase) o un contenedor PostgreSQL ya existente.
- Puertos 80 y 443 abiertos. El 5002 no debe exponerse públicamente.

## Primera instalación

```bash
git clone <URL_DEL_REPOSITORIO> tiendubi
cd tiendubi/admin-base-api
cp .env.production.vps.example .env.production
chmod 600 .env.production
nano .env.production
```

Valores obligatorios que se deben revisar:

- `DEPLOY_CERTBOT_EMAIL`
- `ConnectionStrings__PostgresConnection` si `DEPLOY_DATABASE_MODE=external`
- `POSTGRES_*` si `DEPLOY_DATABASE_MODE=docker`
- `OwnerSecurity__OwnerEmail`
- credenciales de Mercado Pago, R2 y SMTP para las funciones habilitadas

No hace falta inventar manualmente el JWT: si `Jwt__Secret` conserva el placeholder, el script genera uno criptográficamente seguro y lo guarda con permisos `600`.

## Desplegar o actualizar

```bash
bash deploy.sh
```

El script actualiza Git, valida la configuración, crea la red Docker, construye la imagen, levanta el servicio, espera `/api/health` y configura Nginx/Certbot.

Si el checkout ya fue actualizado por CI o manualmente:

```bash
SKIP_GIT_PULL=true bash deploy.sh
```

Para usar Caddy en lugar de Nginx, configurá `DEPLOY_CONFIGURE_NGINX=false` y copiá el `Caddyfile` incluido a la configuración del Caddy instalado en el host.

## Verificación y operación

```bash
curl -f https://api.tiendubi.com/api/health
docker compose --env-file .env.production ps
docker logs -f tiendubi-webapi
```

Las claves de Data Protection y la carpeta `uploads` viven en volúmenes Docker, por lo que sobreviven a la recreación del contenedor. `.env.production` está excluido de Git y del contexto de construcción de Docker.

## Seguridad de datos iniciales

En producción no se crea ni se resetea `admin/admin`. El usuario indicado por `OwnerSecurity__OwnerEmail` se promueve a SuperAdmin únicamente si ya existe. Registralo primero por el flujo normal de la aplicación. La cuenta demo solo se carga si se activa explícitamente `Features__SeedDemoData=true`.
