#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(git -C "$SCRIPT_DIR" rev-parse --show-toplevel 2>/dev/null || printf '%s' "$SCRIPT_DIR")"
ENV_FILE="$SCRIPT_DIR/.env.production"
ENV_EXAMPLE="$SCRIPT_DIR/.env.production.vps.example"

get_env_value() {
  local key="$1"
  grep -E "^${key}=" "$ENV_FILE" | tail -n 1 | cut -d '=' -f 2- || true
}

set_env_value() {
  local key="$1"
  local value="$2"
  local temp_file
  temp_file="$(mktemp)"

  awk -v key="$key" -v value="$value" '
    BEGIN { found = 0 }
    index($0, key "=") == 1 { print key "=" value; found = 1; next }
    { print }
    END { if (!found) print key "=" value }
  ' "$ENV_FILE" > "$temp_file"

  mv "$temp_file" "$ENV_FILE"
  chmod 600 "$ENV_FILE"
}

is_placeholder() {
  printf '%s' "$1" | grep -Eqi '(^$|REPLACE|CHANGE_ME|CAMBIAR|YOUR_|example\.com|tudominio\.com|PASSWORD_FUERTE|capturar|ordenapp)'
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || { echo "ERROR: Falta el comando requerido: $1"; exit 1; }
}

generate_secret() {
  if command -v openssl >/dev/null 2>&1; then
    openssl rand -base64 48 | tr -d '\n'
  else
    od -An -N48 -tx1 /dev/urandom | tr -d ' \n'
  fi
}

get_connection_value() {
  local connection="$1"
  local wanted_key="$2"
  local part key value
  local -a parts

  IFS=';' read -ra parts <<< "$connection"
  for part in "${parts[@]}"; do
    key="${part%%=*}"
    value="${part#*=}"
    key="$(printf '%s' "$key" | sed -E 's/^[[:space:]]+|[[:space:]]+$//g')"
    if [[ "${key,,}" == "${wanted_key,,}" ]]; then
      printf '%s' "$value"
      return 0
    fi
  done
}

show_port_usage() {
  echo "El puerto ${HOST_PORT} esta ocupado. Uso detectado:"
  docker ps --format 'table {{.Names}}\t{{.Ports}}' | grep -E "127\.0\.0\.1:${HOST_PORT}->|0\.0\.0\.0:${HOST_PORT}->|:${HOST_PORT}->" || true

  if command -v ss >/dev/null 2>&1; then
    ss -ltnp "sport = :${HOST_PORT}" || true
  elif command -v lsof >/dev/null 2>&1; then
    lsof -iTCP:"${HOST_PORT}" -sTCP:LISTEN || true
  fi
}

get_container_env_value() {
  local container="$1"
  local key="$2"

  docker inspect --format '{{range .Config.Env}}{{println .}}{{end}}' "$container" \
    | grep -E "^${key}=" \
    | tail -n 1 \
    | cut -d '=' -f 2- || true
}

require_command git
require_command docker
require_command curl

echo "[1/8] Preparando el repositorio..."
if [ "${SKIP_GIT_PULL:-false}" != "true" ]; then
  git -C "$REPO_DIR" pull "${DEPLOY_REMOTE:-origin}" "${DEPLOY_BRANCH:-main}"
fi

echo "[2/8] Validando configuracion..."
if [ ! -f "$ENV_FILE" ]; then
  cp "$ENV_EXAMPLE" "$ENV_FILE"
  chmod 600 "$ENV_FILE"
  echo "Se creo $ENV_FILE desde el ejemplo. Completa sus valores y ejecuta nuevamente bash deploy.sh."
  exit 1
fi
chmod 600 "$ENV_FILE"

APP_NAME="${APP_NAME:-$(get_env_value DEPLOY_APP_NAME)}"
IMAGE_NAME="${IMAGE_NAME:-$(get_env_value DEPLOY_IMAGE_NAME)}"
NETWORK_NAME="${NETWORK_NAME:-$(get_env_value DEPLOY_NETWORK_NAME)}"
HOST_PORT="${HOST_PORT:-$(get_env_value DEPLOY_HOST_PORT)}"
API_DOMAIN="${API_DOMAIN:-$(get_env_value DEPLOY_API_DOMAIN)}"
API_URL="${API_URL:-$(get_env_value DEPLOY_API_URL)}"
FRONTEND_URL="${FRONTEND_URL:-$(get_env_value AppSettings__FrontendUrl)}"
ALLOWED_ORIGINS="${FRONTEND_ALLOWED_ORIGINS:-$(get_env_value Cors__AllowedOrigins)}"
DATABASE_MODE="${DATABASE_MODE:-$(get_env_value DEPLOY_DATABASE_MODE)}"
CONFIGURE_NGINX="${CONFIGURE_NGINX:-$(get_env_value DEPLOY_CONFIGURE_NGINX)}"
ENABLE_HTTPS="${ENABLE_HTTPS:-$(get_env_value DEPLOY_ENABLE_HTTPS)}"
CERTBOT_EMAIL="${CERTBOT_EMAIL:-$(get_env_value DEPLOY_CERTBOT_EMAIL)}"
NGINX_SITE_NAME="${NGINX_SITE_NAME:-$(get_env_value DEPLOY_NGINX_SITE_NAME)}"

if [ -z "$CERTBOT_EMAIL" ]; then
  CERTBOT_EMAIL="$(get_env_value OwnerSecurity__OwnerEmail)"
fi

APP_NAME="${APP_NAME:-tiendubi-webapi}"
IMAGE_NAME="${IMAGE_NAME:-tiendubi-webapi:latest}"
NETWORK_NAME="${NETWORK_NAME:-tiendubi-net}"
HOST_PORT="${HOST_PORT:-5002}"
API_DOMAIN="${API_DOMAIN:-api.tiendubi.com}"
API_URL="${API_URL:-https://api.tiendubi.com}"
FRONTEND_URL="${FRONTEND_URL:-https://tiendubi.com}"
ALLOWED_ORIGINS="${ALLOWED_ORIGINS:-$FRONTEND_URL}"
DATABASE_MODE="${DATABASE_MODE:-docker}"
CONFIGURE_NGINX="${CONFIGURE_NGINX:-true}"
ENABLE_HTTPS="${ENABLE_HTTPS:-true}"
NGINX_SITE_NAME="${NGINX_SITE_NAME:-tiendubi-api}"

if [[ ! "$APP_NAME" =~ ^[a-zA-Z0-9._-]+$ ]] || [[ ! "$NGINX_SITE_NAME" =~ ^[a-zA-Z0-9._-]+$ ]]; then
  echo "ERROR: Los nombres de app y sitio Nginx contienen caracteres no permitidos."
  exit 1
fi
if [[ ! "$HOST_PORT" =~ ^[0-9]+$ ]] || [ "$HOST_PORT" -lt 1 ] || [ "$HOST_PORT" -gt 65535 ]; then
  echo "ERROR: DEPLOY_HOST_PORT debe ser un puerto valido."
  exit 1
fi
if [[ ! "$API_DOMAIN" =~ ^[a-zA-Z0-9.-]+$ ]]; then
  echo "ERROR: DEPLOY_API_DOMAIN no tiene un formato valido."
  exit 1
fi
if is_placeholder "$API_URL" || is_placeholder "$FRONTEND_URL"; then
  echo "ERROR: Configura DEPLOY_API_URL y AppSettings__FrontendUrl."
  exit 1
fi
if [ "$DATABASE_MODE" != "external" ] && [ "$DATABASE_MODE" != "docker" ]; then
  echo "ERROR: DEPLOY_DATABASE_MODE debe ser external o docker."
  exit 1
fi
if [ "$CONFIGURE_NGINX" = "true" ] && [ "$ENABLE_HTTPS" = "true" ]; then
  if is_placeholder "$CERTBOT_EMAIL" || [[ ! "$CERTBOT_EMAIL" =~ ^[^[:space:]@]+@[^[:space:]@]+\.[^[:space:]@]+$ ]]; then
    echo "ERROR: Configura DEPLOY_CERTBOT_EMAIL para solicitar HTTPS."
    exit 1
  fi
fi

PHOTO_UPLOAD_ENABLED="$(get_env_value Features__PhotoUploadEnabled)"
if [ "${PHOTO_UPLOAD_ENABLED,,}" = "true" ]; then
  R2_CONFIG_INVALID=false
  for r2_key in R2__AccountId R2__AccessKeyId R2__SecretAccessKey R2__BucketName; do
    r2_value="$(get_env_value "$r2_key")"
    if is_placeholder "$r2_value"; then
      echo "ERROR: Configura $r2_key en $ENV_FILE."
      R2_CONFIG_INVALID=true
    fi
  done

  if [ "$R2_CONFIG_INVALID" = "true" ]; then
    echo "Cloudflare R2 esta habilitado, pero conserva valores de ejemplo."
    echo "Crea credenciales Object Read & Write para el bucket y reemplaza los valores REPLACE."
    exit 1
  fi
fi

JWT_SECRET="$(get_env_value Jwt__Secret)"
if is_placeholder "$JWT_SECRET" || [ "${#JWT_SECRET}" -lt 32 ]; then
  JWT_SECRET="$(generate_secret)"
  set_env_value "Jwt__Secret" "$JWT_SECRET"
  echo "Se genero un Jwt__Secret seguro."
fi

set_env_value "ASPNETCORE_ENVIRONMENT" "Production"
set_env_value "ASPNETCORE_URLS" "http://+:8081"
set_env_value "PORT" "8081"
set_env_value "DATA_PROTECTION_KEYS_PATH" "/app/keys"
set_env_value "AppSettings__FrontendUrl" "$FRONTEND_URL"
set_env_value "Cors__AllowedOrigins" "$ALLOWED_ORIGINS"
set_env_value "Features__SeedDevelopmentAdmin" "false"
set_env_value "Payment__MonthlyPrice" "24999"
set_env_value "Payment__AnnualPrice" "239990"
set_env_value "Payment__CommissionPercent" "0"
set_env_value "MercadoPagoSettings__CommissionPercentage" "0"

echo "[3/8] Preparando red y base de datos..."
docker network inspect "$NETWORK_NAME" >/dev/null 2>&1 || docker network create "$NETWORK_NAME" >/dev/null

CONNECTION_STRING="$(get_env_value ConnectionStrings__PostgresConnection)"
CONNECTION_HOST="$(get_connection_value "$CONNECTION_STRING" Host)"

# Igual que el deploy de MedCenterOS: una conexion al loopback del VPS debe
# resolverse mediante el contenedor PostgreSQL dentro de la red Docker.
if [ "$DATABASE_MODE" = "external" ] && [[ "${CONNECTION_HOST,,}" =~ ^(127\.0\.0\.1|localhost|::1)$ ]]; then
  echo "Se detecto PostgreSQL local en ${CONNECTION_HOST}. Se usara el contenedor Docker 'postgres'."
  DATABASE_MODE="docker"
  set_env_value "DEPLOY_DATABASE_MODE" "docker"
fi

if [ "$DATABASE_MODE" = "docker" ]; then
  POSTGRES_NAME="${POSTGRES_NAME:-$(get_env_value POSTGRES_CONTAINER)}"
  POSTGRES_DB="${POSTGRES_DB:-$(get_env_value POSTGRES_DB)}"
  POSTGRES_USER="${POSTGRES_USER:-$(get_env_value POSTGRES_USER)}"
  POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-$(get_env_value POSTGRES_PASSWORD)}"
  POSTGRES_ADMIN_USER="${POSTGRES_ADMIN_USER:-$(get_env_value POSTGRES_ADMIN_USER)}"
  POSTGRES_ADMIN_DB="${POSTGRES_ADMIN_DB:-$(get_env_value POSTGRES_ADMIN_DB)}"
  POSTGRES_AUTO_CREATE="${POSTGRES_AUTO_CREATE:-$(get_env_value POSTGRES_AUTO_CREATE)}"
  CONNECTION_DB="$(get_connection_value "$CONNECTION_STRING" Database)"
  CONNECTION_USER="$(get_connection_value "$CONNECTION_STRING" Username)"
  CONNECTION_PASSWORD="$(get_connection_value "$CONNECTION_STRING" Password)"
  POSTGRES_NAME="${POSTGRES_NAME:-postgres}"
  POSTGRES_DB="${POSTGRES_DB:-${CONNECTION_DB:-tiendubi}}"
  POSTGRES_USER="${POSTGRES_USER:-${CONNECTION_USER:-tiendubi}}"
  POSTGRES_ADMIN_USER="${POSTGRES_ADMIN_USER:-postgres}"
  POSTGRES_ADMIN_DB="${POSTGRES_ADMIN_DB:-postgres}"
  POSTGRES_AUTO_CREATE="${POSTGRES_AUTO_CREATE:-true}"

  if is_placeholder "$POSTGRES_PASSWORD" && ! is_placeholder "$CONNECTION_PASSWORD"; then
    POSTGRES_PASSWORD="$CONNECTION_PASSWORD"
  fi

  if is_placeholder "$POSTGRES_PASSWORD" || [[ "$POSTGRES_PASSWORD" == *";"* ]]; then
    echo "ERROR: Configura un POSTGRES_PASSWORD fuerte y sin punto y coma."
    exit 1
  fi
  if ! docker ps -a --format '{{.Names}}' | grep -qx "$POSTGRES_NAME"; then
    echo "ERROR: No existe el contenedor PostgreSQL: $POSTGRES_NAME"
    exit 1
  fi

  CONTAINER_POSTGRES_USER="$(get_container_env_value "$POSTGRES_NAME" POSTGRES_USER)"
  CONTAINER_POSTGRES_DB="$(get_container_env_value "$POSTGRES_NAME" POSTGRES_DB)"
  if [ -n "$CONTAINER_POSTGRES_USER" ]; then
    POSTGRES_ADMIN_USER="$CONTAINER_POSTGRES_USER"
  fi
  if [ -n "$CONTAINER_POSTGRES_DB" ]; then
    POSTGRES_ADMIN_DB="$CONTAINER_POSTGRES_DB"
  elif [ -n "$CONTAINER_POSTGRES_USER" ]; then
    POSTGRES_ADMIN_DB="$CONTAINER_POSTGRES_USER"
  fi

  echo "Usando PostgreSQL: contenedor=${POSTGRES_NAME}, database=${POSTGRES_DB}, user=${POSTGRES_USER}"
  echo "Administracion PostgreSQL: user=${POSTGRES_ADMIN_USER}, database=${POSTGRES_ADMIN_DB}"
  docker start "$POSTGRES_NAME" >/dev/null
  docker network connect "$NETWORK_NAME" "$POSTGRES_NAME" >/dev/null 2>&1 || true
  for attempt in {1..30}; do
    docker exec "$POSTGRES_NAME" pg_isready -U "$POSTGRES_ADMIN_USER" -d "$POSTGRES_ADMIN_DB" >/dev/null 2>&1 && break
    if [ "$attempt" -eq 30 ]; then echo "ERROR: PostgreSQL no quedo listo."; exit 1; fi
    sleep 2
  done

  if [ "$POSTGRES_AUTO_CREATE" = "true" ]; then
    docker exec -i \
      -e "APP_DB_NAME=$POSTGRES_DB" -e "APP_DB_USER=$POSTGRES_USER" \
      -e "APP_DB_PASSWORD=$POSTGRES_PASSWORD" -e "PG_ADMIN_USER=$POSTGRES_ADMIN_USER" \
      -e "PG_ADMIN_DB=$POSTGRES_ADMIN_DB" "$POSTGRES_NAME" sh -ceu '
        exec psql --set ON_ERROR_STOP=1 --username "$PG_ADMIN_USER" --dbname "$PG_ADMIN_DB" \
          --set app_db="$APP_DB_NAME" --set app_user="$APP_DB_USER" --set app_password="$APP_DB_PASSWORD"
      ' <<'SQL'
SELECT format('CREATE ROLE %I LOGIN', :'app_user')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'app_user')
\gexec
SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L', :'app_user', :'app_password')
\gexec
SELECT format('CREATE DATABASE %I OWNER %I', :'app_db', :'app_user')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'app_db')
\gexec
SQL

    # Asegura permisos del usuario de la app sobre la base (por si la base y/o el
    # rol ya existian de un deploy anterior con otro propietario). Sin esto, la
    # API falla con "permission denied for table __EFMigrationsHistory".
    docker exec -i \
      -e "APP_DB_NAME=$POSTGRES_DB" -e "APP_DB_USER=$POSTGRES_USER" \
      -e "PG_ADMIN_USER=$POSTGRES_ADMIN_USER" "$POSTGRES_NAME" sh -ceu '
        exec psql --set ON_ERROR_STOP=1 --username "$PG_ADMIN_USER" --dbname "$APP_DB_NAME" \
          --set app_db="$APP_DB_NAME" --set app_user="$APP_DB_USER"
      ' <<'SQL'
ALTER DATABASE :"app_db" OWNER TO :"app_user";
ALTER SCHEMA public OWNER TO :"app_user";

-- Entity Framework necesita ser propietario para ejecutar ALTER/DROP en las
-- migraciones, no alcanza solamente con otorgar permisos sobre los datos.
SELECT format(
  'ALTER %s %I.%I OWNER TO %I',
  CASE c.relkind
    WHEN 'S' THEN 'SEQUENCE'
    WHEN 'v' THEN 'VIEW'
    WHEN 'm' THEN 'MATERIALIZED VIEW'
    WHEN 'f' THEN 'FOREIGN TABLE'
    ELSE 'TABLE'
  END,
  n.nspname,
  c.relname,
  :'app_user'
)
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public'
  AND c.relkind IN ('r', 'p', 'S', 'v', 'm', 'f')
  AND pg_get_userbyid(c.relowner) <> :'app_user'
ORDER BY CASE WHEN c.relkind = 'S' THEN 2 ELSE 1 END, c.relname;
\gexec

GRANT ALL ON SCHEMA public TO :"app_user";
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO :"app_user";
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO :"app_user";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO :"app_user";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO :"app_user";
SQL
  fi
  set_env_value "ConnectionStrings__PostgresConnection" "Host=${POSTGRES_NAME};Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
else
  if is_placeholder "$CONNECTION_STRING"; then
    echo "ERROR: Configura ConnectionStrings__PostgresConnection para la base externa."
    exit 1
  fi
fi

echo "[4/8] Deteniendo el contenedor anterior..."
cd "$SCRIPT_DIR"
docker stop "$APP_NAME" >/dev/null 2>&1 || true
docker rm "$APP_NAME" >/dev/null 2>&1 || true

if docker ps --format '{{.Ports}}' | grep -Eq "127\.0\.0\.1:${HOST_PORT}->|0\.0\.0\.0:${HOST_PORT}->|:${HOST_PORT}->"; then
  show_port_usage
  echo "ERROR: Libera el puerto ${HOST_PORT} o configura otro DEPLOY_HOST_PORT en .env.production."
  exit 1
fi
if command -v ss >/dev/null 2>&1 && ss -ltn "sport = :${HOST_PORT}" | grep -q ":${HOST_PORT}"; then
  show_port_usage
  echo "ERROR: Libera el puerto ${HOST_PORT} o configura otro DEPLOY_HOST_PORT en .env.production."
  exit 1
fi

echo "[5/8] Construyendo e iniciando la API..."
docker build --pull -t "$IMAGE_NAME" .
docker run -d \
  --name "$APP_NAME" \
  --restart unless-stopped \
  --network "$NETWORK_NAME" \
  --env-file "$ENV_FILE" \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS=http://+:8081 \
  -e PORT=8081 \
  -e DATA_PROTECTION_KEYS_PATH=/app/keys \
  -v tiendubi_webapi_keys:/app/keys \
  -v tiendubi_webapi_uploads:/app/uploads \
  -p "127.0.0.1:${HOST_PORT}:8081" \
  "$IMAGE_NAME" >/dev/null

echo "Esperando el health check..."
for attempt in {1..45}; do
  if curl --fail --silent "http://127.0.0.1:${HOST_PORT}/api/health" >/dev/null; then
    break
  fi
  if [ "$attempt" -eq 45 ]; then
    echo "ERROR: La API no respondio correctamente."
    docker logs --tail 150 "$APP_NAME" || true
    exit 1
  fi
  sleep 2
done

if [ "$CONFIGURE_NGINX" = "true" ]; then
  echo "[6/8] Configurando Nginx..."
  require_command sudo
  require_command nginx
  [ "$ENABLE_HTTPS" != "true" ] || require_command certbot
  sudo -v

  NGINX_TEMPLATE="$SCRIPT_DIR/nginx/tiendubi-api.conf.example"
  NGINX_SITE_PATH="/etc/nginx/sites-available/$NGINX_SITE_NAME"
  NGINX_ENABLED_PATH="/etc/nginx/sites-enabled/$NGINX_SITE_NAME"

  if sudo test -f "$NGINX_SITE_PATH"; then
    if ! sudo grep -Fq "server_name ${API_DOMAIN};" "$NGINX_SITE_PATH" || \
       ! sudo grep -Fq "proxy_pass http://127.0.0.1:${HOST_PORT};" "$NGINX_SITE_PATH"; then
      echo "ERROR: $NGINX_SITE_PATH ya existe con otro dominio o puerto."
      exit 1
    fi

    if sudo grep -Eq 'client_max_body_size[[:space:]]+[^;]+;' "$NGINX_SITE_PATH"; then
      sudo sed -i -E 's/client_max_body_size[[:space:]]+[^;]+;/client_max_body_size 1100m;/' "$NGINX_SITE_PATH"
    else
      sudo sed -i "/server_name ${API_DOMAIN};/a\\    client_max_body_size 1100m;" "$NGINX_SITE_PATH"
    fi
  else
    sed -e "s/api\.tiendubi\.com/${API_DOMAIN}/g" \
        -e "s/127\.0\.0\.1:5002/127.0.0.1:${HOST_PORT}/g" \
        "$NGINX_TEMPLATE" | sudo tee "$NGINX_SITE_PATH" >/dev/null
  fi

  sudo ln -sfn "$NGINX_SITE_PATH" "$NGINX_ENABLED_PATH"
  sudo nginx -t
  sudo systemctl reload nginx

  if [ "$ENABLE_HTTPS" = "true" ]; then
    sudo certbot --nginx --non-interactive --agree-tos --redirect --keep-until-expiring \
      --cert-name "$API_DOMAIN" --email "$CERTBOT_EMAIL" -d "$API_DOMAIN"
    sudo nginx -t
    sudo systemctl reload nginx
  fi
else
  echo "[6/8] Configuracion automatica de Nginx deshabilitada."
fi

echo "[7/8] Verificando el contenedor..."
docker ps --filter "name=^/${APP_NAME}$"

echo "[8/8] Deploy completado."
echo "API publica: $API_URL"
echo "Health:      $API_URL/api/health"
echo "Logs:        docker logs -f $APP_NAME"
